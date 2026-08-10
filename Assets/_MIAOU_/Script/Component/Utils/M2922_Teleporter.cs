using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Mode d'activation du téléporteur.
    /// </summary>
    public enum TeleporterActivation
    {
        TriggerZone  = 0,
        Interaction  = 1,
        Both         = 2,
    }

    /// <summary>
    /// Téléporteur de base — une seule destination, léger.
    /// Pour les fonctionnalités avancées (multi-destinations, immunité),
    /// utilisez M2922_AdvancedTeleporter.
    /// </summary>
    [AddComponentMenu("M2922/Utils/Teleporter")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Teleporter : M2922_Base
    {
        // =====================================================================
        // DESTINATION
        // =====================================================================
        [Header("=== DESTINATION ===")]
        [Tooltip("Transform de l'arrivée.")]
        [SerializeField] protected Transform _destination;

        // =====================================================================
        // ACTIVATION
        // =====================================================================
        [Header("=== ACTIVATION ===")]
        [Tooltip("TriggerZone / Interaction / Both")]
        [SerializeField] protected TeleporterActivation _activationMode = TeleporterActivation.TriggerZone;

        [Tooltip("Active ou désactive ce téléporteur.")]
        [SerializeField] protected bool _isEnabled = true;

        // =====================================================================
        // FILTRES PAR TYPE
        // =====================================================================
        [Header("=== FILTRES PAR TYPE ===")]
        [Tooltip("Joueurs (OnPlayerTriggerEnter + Interact).")]
        [SerializeField] protected bool _allowPlayers = true;
        [Tooltip("Entités avec M2922_TeleportMarker (NPC).")]
        [SerializeField] protected bool _allowNPCs = true;
        [Tooltip("Entités avec M2922_TeleportMarker (Vehicle).")]
        [SerializeField] protected bool _allowVehicles = true;
        [Tooltip("Entités avec M2922_TeleportMarker (Object).")]
        [SerializeField] protected bool _allowObjects = true;
        [Tooltip("Rigidbodies SANS M2922_TeleportMarker (catch-all).")]
        [SerializeField] protected bool _allowPhysicsObjects = true;

        // =====================================================================
        // OPTIONS
        // =====================================================================
        [Header("=== OPTIONS ===")]
        [SerializeField] protected float _teleportDelay = 0f;
        [SerializeField] protected float _cooldown = 1f;
        [SerializeField] protected bool  _alignRotation = true;
        [SerializeField] protected bool  _oneShot = false;
        [SerializeField] protected bool  _preserveOffset = false;
        [Tooltip("Si coché : reset la vélocité du Rigidbody de l'entité après téléportation.\n" +
                 "Évite la dérive des objets physiques (cube qui tombe en boucle).")]
        [SerializeField] protected bool  _resetVelocityOnTeleport = false;
        [Tooltip("Rayon (mètres) autour d'une entité avec M2922_TeleportMarker. 0 = désactivé.")]
        [SerializeField] protected float _riderTeleportRadius = 3f;

        // =====================================================================
        // VFX / SFX
        // =====================================================================
        [Header("=== VFX / SFX ===")]
        [SerializeField] protected ParticleSystem _departEffect;
        [SerializeField] protected ParticleSystem _arrivalEffect;
        [SerializeField] protected AudioSource    _departAudio;
        [SerializeField] protected AudioSource    _arrivalAudio;

        // =====================================================================
        // RUNTIME
        // =====================================================================
        protected float         _lastPlayerTeleportTime = -999f;
        protected float         _lastEntityTeleportTime = -999f;
        protected VRCPlayerApi  _pendingPlayer;
        protected Transform     _pendingEntityTransform;
        protected string        _pendingEntityType;
        protected VRC_Pickup[]  _pendingPickups;
        protected const int     MAX_PICKUPS = 2;
        protected Vector3       _pendingOffset;
        protected Transform     _pendingDestination;
        protected string        _pendingDestName;

        // =====================================================================
        // PROPERTIES
        // =====================================================================
        public bool                 IsEnabled      => _isEnabled;
        public TeleporterActivation ActivationMode => _activationMode;
        public bool                 HasDestination => GetDestination() != null;


        // =====================================================================
        // PUBLIC API
        // =====================================================================

        public void Enable()  { _isEnabled = true;  this.Log("[Teleporter] Activé"); }
        public void Disable() { _isEnabled = false; this.Log("[Teleporter] Désactivé"); }
        public void SetEnabled(bool value) { _isEnabled = value; this.Log($"[Teleporter] Enabled → {value}"); }

        public void TeleportLocalPlayer()
        {
            if (!_isEnabled) return;
            this.VerboseLog("[Teleporter] Appel externe TeleportLocalPlayer()");
            TryTeleportPlayer(Networking.LocalPlayer);
        }

        /// <summary>Change la destination à chaud (utilisable en runtime).</summary>
        public void SetDestination(Transform newDest)
        {
            _destination = newDest;
            this.Log($"[Teleporter] Destination → {(newDest != null ? newDest.name : "NULL")}");
        }

        // =====================================================================
        // VRC CALLBACKS
        // =====================================================================

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!_isEnabled) return;
            if (!_allowPlayers) { this.VerboseLog("[Teleporter] Joueur rejeté (filtre)"); return; }
            if (_activationMode == TeleporterActivation.Interaction) return;
            if (!Utilities.IsValid(player)) return;
            if (player.playerId != Networking.LocalPlayer.playerId) return;

            CaptureHeldPickups();
            this.VerboseLog($"[Teleporter] Joueur local détecté → tp programmé (delay={_teleportDelay}s)");
            TryTeleportPlayer(player);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isEnabled) return;
            if (_activationMode == TeleporterActivation.Interaction) return;
            if (!Utilities.IsValid(other)) return;

            // ── 0. PICKUP TENU ──────────────────────────────────────────
            VRC_Pickup pickup = other.GetComponentInParent<VRC_Pickup>();
            if (pickup != null)
            {
                VRC_Pickup left  = Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left);
                VRC_Pickup right = Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
                if (pickup == left || pickup == right) { this.VerboseLog("[Teleporter] Pickup tenu ignoré"); return; }
            }

            // ── 1. EXCLURE HITBOXES JOUEUR ──────────────────────────────
            M2922_HitboxSystem hitbox = other.GetComponentInParent<M2922_HitboxSystem>();
            if (hitbox != null && hitbox.BoundPlayerId >= 0) return;

            // ── 2. MARKER → téléportation typée ─────────────────────────
            M2922_TeleportMarker marker = other.GetComponentInParent<M2922_TeleportMarker>();
            if (marker != null)
            {
                switch (marker.EntityType)
                {
                    case TeleportEntityType.NPC:
                        if (!_allowNPCs) { this.VerboseLog("[Teleporter] NPC rejeté (filtre)"); return; }
                        break;
                    case TeleportEntityType.Vehicle:
                        if (!_allowVehicles) { this.VerboseLog("[Teleporter] Vehicle rejeté (filtre)"); return; }
                        break;
                    case TeleportEntityType.Physics:
                        if (!_allowPhysicsObjects) { this.VerboseLog("[Teleporter] Physique rejeté (filtre)"); return; }
                        break;
                    case TeleportEntityType.Object:
                        if (!_allowObjects) { this.VerboseLog("[Teleporter] Object rejeté (filtre)"); return; }
                        break;
                }
                if (!Networking.IsOwner(marker.gameObject)) { this.VerboseLog("[Teleporter] Entité rejetée (pas owner)"); return; }
                this.VerboseLog($"[Teleporter] {marker.EntityType} détecté → {marker.name}");
                TryTeleportEntity(marker.transform, marker.EntityType.ToString(), isRiderCapable: true);
                return;
            }

            // ── 3. PHYSIQUE GÉNÉRIQUE (sans marker) ────────────────────
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                if (!_allowPhysicsObjects) { this.VerboseLog("[Teleporter] Physique rejeté (filtre)"); return; }
                if (!Networking.IsOwner(rb.gameObject)) { this.VerboseLog("[Teleporter] Physique rejeté (pas owner)"); return; }
                this.VerboseLog($"[Teleporter] Objet physique générique détecté → {rb.name}");
                TryTeleportEntity(rb.transform, "Physics", isRiderCapable: false);
            }
        }

        public override void Interact()
        {
            if (!_isEnabled) return;
            if (!_allowPlayers) { this.VerboseLog("[Teleporter] Interact rejeté (filtre)"); return; }
            if (_activationMode == TeleporterActivation.TriggerZone) return;

            CaptureHeldPickups();
            this.VerboseLog("[Teleporter] Interact → tp joueur local");
            TryTeleportPlayer(Networking.LocalPlayer);
        }

        // =====================================================================
        // TELEPORT INTERNALS
        // =====================================================================

        /// <summary>Résout la destination. Virtual — override pour multi-dest.</summary>
        protected virtual Transform GetDestination() { return _destination; }

        protected void TryTeleportPlayer(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return;
            Transform dest = GetDestination();
            if (dest == null) { this.Log("[Teleporter] Destination non assignée !"); return; }
            if (Time.time - _lastPlayerTeleportTime < _cooldown) { this.VerboseLog("[Teleporter] Joueur rejeté (cooldown)"); return; }

            _lastPlayerTeleportTime = Time.time;
            _pendingPlayer          = player;
            _pendingDestination     = dest;
            _pendingDestName        = dest.name;
            _pendingOffset          = _preserveOffset ? (player.GetPosition() - transform.position) : Vector3.zero;

            PlayDepartEffect();
            PlayDepartSound();

            if (_teleportDelay <= 0f) _DoTeleportPlayer();
            else SendCustomEventDelayedSeconds(nameof(_DoTeleportPlayer), _teleportDelay);
        }

        protected void TryTeleportEntity(Transform entityTransform, string entityType, bool isRiderCapable)
        {
            Transform dest = GetDestination();
            if (dest == null) { this.Log("[Teleporter] Destination non assignée !"); return; }
            if (Time.time - _lastEntityTeleportTime < _cooldown) { this.VerboseLog($"[Teleporter] {entityType} rejeté (cooldown)"); return; }

            _lastEntityTeleportTime = Time.time;
            _pendingEntityTransform = entityTransform;
            _pendingEntityType      = entityType;
            _pendingDestination     = dest;
            _pendingDestName        = dest.name;
            _pendingOffset          = _preserveOffset ? (entityTransform.position - transform.position) : Vector3.zero;

            if (isRiderCapable && _riderTeleportRadius > 0f)
            {
                VRCPlayerApi local = Networking.LocalPlayer;
                if (local != null && local.IsValid())
                {
                    float dist = Vector3.Distance(local.GetPosition(), entityTransform.position);
                    if (dist < _riderTeleportRadius)
                    {
                        _pendingPlayer = local;
                        _lastPlayerTeleportTime = Time.time;
                        this.VerboseLog($"[Teleporter] Rider détecté: {local.displayName} à {dist:F1}m de {entityType}");
                    }
                }
            }

            this.VerboseLog($"[Teleporter] {entityType} → tp programmé (delay={_teleportDelay}s)");
            PlayDepartEffect();
            PlayDepartSound();

            if (_teleportDelay <= 0f) _DoTeleportEntity();
            else SendCustomEventDelayedSeconds(nameof(_DoTeleportEntity), _teleportDelay);
        }

        public void _DoTeleportPlayer()
        {
            if (!Utilities.IsValid(_pendingPlayer)) { ClearPendingState(); return; }
            if (_pendingDestination == null)        { ClearPendingState(); return; }

            Quaternion targetRot = _alignRotation ? _pendingDestination.rotation : _pendingPlayer.GetRotation();
            Vector3 arrivalPos = _pendingDestination.position + _pendingOffset;

            int pickupCount = 0;
            if (_pendingPickups != null)
            {
                for (int i = 0; i < _pendingPickups.Length; i++)
                    if (_pendingPickups[i] != null)
                        _pendingPickups[i].transform.SetPositionAndRotation(arrivalPos, targetRot);
                pickupCount = _pendingPickups.Length;
                _pendingPickups = null;
            }

            _pendingPlayer.TeleportTo(arrivalPos, targetRot);
            string offsetInfo = _preserveOffset && _pendingOffset.magnitude > 0.01f ? $" (offset={_pendingOffset.magnitude:F1}m)" : "";
            string pickupInfo = pickupCount > 0 ? $" +{pickupCount} pickup(s)" : "";
            this.Log($"[Teleporter] Joueur{pickupInfo} téléporté → {_pendingDestName}{offsetInfo}");
            _pendingPlayer = null;
            _pendingDestination = null;

            AfterTeleport();
            PlayArrivalSound();
            PlayArrivalEffect();
            DisableIfOneShot();
        }

        public void _DoTeleportEntity()
        {
            if (_pendingEntityTransform == null) { ClearPendingState(); return; }
            if (_pendingDestination == null)     { _pendingEntityTransform = null; _pendingPlayer = null; return; }

            // Accorder l'immunité AVANT de déplacer l'entité.
            // Si MovePosition déclenche OnTriggerEnter sur la destination,
            // l'immunité est déjà active → pas de ping-pong dans la même frame.
            AfterTeleport();

            Quaternion targetRot = _alignRotation ? _pendingDestination.rotation : _pendingEntityTransform.rotation;
            Vector3 arrivalPos = _pendingDestination.position + _pendingOffset;

            Rigidbody rb = _pendingEntityTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.MovePosition(arrivalPos);
                rb.MoveRotation(targetRot);
                if (_resetVelocityOnTeleport)
                    rb.velocity = Vector3.zero;
                // Passer en ContinuousDynamic pour éviter que l'objet
                // ne traverse les colliders à grande vitesse (tunneling)
                if (rb.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            else
            {
                _pendingEntityTransform.SetPositionAndRotation(arrivalPos, targetRot);
            }

            _pendingEntityTransform = null;

            if (_pendingPlayer != null && Utilities.IsValid(_pendingPlayer))
            {
                Quaternion playerRot = _alignRotation ? _pendingDestination.rotation : _pendingPlayer.GetRotation();
                _pendingPlayer.TeleportTo(arrivalPos, playerRot);
                this.Log($"[Teleporter] Joueur embarqué + {_pendingEntityType} téléportés → {_pendingDestName}");
                _pendingPlayer = null;
            }
            else
            {
                this.Log($"[Teleporter] {_pendingEntityType} téléporté → {_pendingDestName}");
            }

            _pendingDestination = null;
            PlayArrivalSound();
            PlayArrivalEffect();
            DisableIfOneShot();
        }

        /// <summary>Hook appelé après chaque téléportation. Override pour immunité.</summary>
        protected virtual void AfterTeleport() { }

        private void ClearPendingState()
        {
            _pendingPlayer = null;
            _pendingPickups = null;
            _pendingDestination = null;
            _pendingEntityTransform = null;
        }

        // =====================================================================
        // VFX / SFX
        // =====================================================================

        protected void PlayDepartEffect()  { if (_departEffect  != null) _departEffect.Play(); }
        protected void PlayArrivalEffect() { if (_arrivalEffect != null) _arrivalEffect.Play(); }
        protected void DisableIfOneShot()  { if (_oneShot) { this.VerboseLog("[Teleporter] OneShot"); Disable(); } }
        protected void PlayDepartSound()   { if (_departAudio != null && _departAudio.clip != null) _departAudio.PlayOneShot(_departAudio.clip); }
        protected void PlayArrivalSound()  { if (_arrivalAudio != null && _arrivalAudio.clip != null) _arrivalAudio.PlayOneShot(_arrivalAudio.clip); }

        // =====================================================================
        // PICKUP UTILS
        // =====================================================================

        protected void CaptureHeldPickups()
        {
            VRC_Pickup left  = Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left);
            VRC_Pickup right = Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
            int count = 0;
            if (left  != null) count++;
            if (right != null) count++;
            if (count == 0) { _pendingPickups = null; return; }
            _pendingPickups = new VRC_Pickup[count];
            int idx = 0;
            if (left  != null) _pendingPickups[idx++] = left;
            if (right != null) _pendingPickups[idx++] = right;
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _cooldown            = Mathf.Max(0f, _cooldown);
            _teleportDelay       = Mathf.Max(0f, _teleportDelay);
            _riderTeleportRadius = Mathf.Max(0f, _riderTeleportRadius);
        }

        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string dest  = _destination != null ? _destination.name : "AUCUNE";
            string state = _isEnabled ? "ON" : "OFF";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Destination", dest,  _destination != null ? Color.cyan : Color.red),
                new M2922_GizmoDisplayInfo("État",        state, _isEnabled ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Mode",        _activationMode.ToString()),
                new M2922_GizmoDisplayInfo("Players",      _allowPlayers        ? "✔" : "✘", _allowPlayers        ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("NPCs",         _allowNPCs           ? "✔" : "✘", _allowNPCs           ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Vehicles",     _allowVehicles       ? "✔" : "✘", _allowVehicles       ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Objects",      _allowObjects        ? "✔" : "✘", _allowObjects        ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Physics Objs", _allowPhysicsObjects ? "✔" : "✘", _allowPhysicsObjects ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Rider Radius", _riderTeleportRadius > 0f ? $"{_riderTeleportRadius}m" : "OFF"),
                new M2922_GizmoDisplayInfo("Preserve Offset", _preserveOffset ? "ON" : "OFF", _preserveOffset ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Reset Velocity", _resetVelocityOnTeleport ? "ON" : "OFF", _resetVelocityOnTeleport ? Color.green : Color.gray),
            };
        }

        protected override void OnDrawGizmos()        { base.OnDrawGizmos(); DrawDestinationGizmos(0.25f, 0.08f); }
        protected override void OnDrawGizmosSelected() { base.OnDrawGizmosSelected(); DrawDestinationGizmos(0.8f, 0.1f); }

        protected virtual void DrawDestinationGizmos(float alpha, float sphereScale)
        {
            if (_destination == null) return;
            Gizmos.color = _isEnabled ? new Color(0f, 1f, 1f, alpha) : new Color(0.5f, 0.5f, 0.5f, alpha * 0.6f);
            float sphereSize = Mathf.Max(0.05f, transform.localScale.magnitude * sphereScale);
            Gizmos.DrawLine(transform.position, _destination.position);
            Gizmos.DrawSphere(_destination.position, sphereSize);
        }
#endif
    }
}
