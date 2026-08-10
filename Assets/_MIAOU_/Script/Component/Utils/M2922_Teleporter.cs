using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;
using M2922.Component.Weapon;

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
        [SerializeField] protected bool _allowPlayers = true;
        [SerializeField] protected bool _allowNPCs = true;
        [SerializeField] protected bool _allowWeapons = true;
        [SerializeField] protected bool _allowProjectiles = false;
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
        [Tooltip("Rayon (mètres) autour d'une entité avec M2922_TeleportRiderMarker. 0 = désactivé.")]
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
        public bool                 AllowProjectiles => _allowProjectiles;

        /// <summary>
        /// Calcule l'origine et la direction de sortie pour un rayon entrant dans ce TP.
        /// Si _alignRotation est actif, la direction est pivotée selon l'orientation
        /// relative entre le TP d'entrée et sa destination (comportement portal).
        /// Sinon, seule la position change, la direction reste identique.
        /// Retourne false si pas de destination.
        /// </summary>
        public bool ComputeExitRay(Vector3 entryPoint, Vector3 entryDir,
            out Vector3 exitPoint, out Vector3 exitDir)
        {
            exitPoint = entryPoint;
            exitDir = entryDir;
            Transform dest = GetDestination();
            if (dest == null) return false;

            // Transformer la position d'entrée en espace local du TP
            Vector3 localPoint = transform.InverseTransformPoint(entryPoint);

            if (_alignRotation)
            {
                // Portal complet : position + direction pivotées
                Vector3 localDir = transform.InverseTransformDirection(entryDir);
                exitPoint = dest.TransformPoint(localPoint);
                exitDir = dest.TransformDirection(localDir);
            }
            else
            {
                // Translation seule : direction conservée
                exitPoint = dest.TransformPoint(localPoint);
                exitDir = entryDir;
            }
            return true;
        }

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

            // ── 2. NPC ──────────────────────────────────────────────────
            M2922_DamageReceiver receiver = other.GetComponentInParent<M2922_DamageReceiver>();
            if (receiver != null)
            {
                if (!_allowNPCs) { this.VerboseLog("[Teleporter] NPC rejeté (filtre)"); return; }
                if (!Networking.IsOwner(receiver.gameObject)) { this.VerboseLog("[Teleporter] NPC rejeté (pas owner)"); return; }
                bool isRider = other.GetComponentInParent<M2922_TeleportRiderMarker>() != null;
                this.VerboseLog($"[Teleporter] NPC détecté → {receiver.name}");
                TryTeleportEntity(receiver.transform, "NPC", isRider);
                return;
            }

            // ── 3. ARME ─────────────────────────────────────────────────
            M2922_Weapon weapon = other.GetComponentInParent<M2922_Weapon>();
            if (weapon != null)
            {
                if (!_allowWeapons) { this.VerboseLog("[Teleporter] Arme rejetée (filtre)"); return; }
                if (!Networking.IsOwner(weapon.gameObject)) { this.VerboseLog("[Teleporter] Arme rejetée (pas owner)"); return; }
                bool isRider = other.GetComponentInParent<M2922_TeleportRiderMarker>() != null;
                this.VerboseLog($"[Teleporter] Arme détectée → {weapon.name}");
                TryTeleportEntity(weapon.transform, "Weapon", isRider);
                return;
            }

            // ── 4. PROJECTILE ───────────────────────────────────────────
            M2922_Projectile projectile = other.GetComponentInParent<M2922_Projectile>();
            if (projectile != null)
            {
                if (!_allowProjectiles) { this.VerboseLog("[Teleporter] Projectile rejeté (filtre)"); return; }
                if (!Networking.IsOwner(projectile.gameObject)) { this.VerboseLog("[Teleporter] Projectile rejeté (pas owner)"); return; }
                bool isRider = other.GetComponentInParent<M2922_TeleportRiderMarker>() != null;
                this.VerboseLog($"[Teleporter] Projectile détecté → {projectile.name}");
                TryTeleportEntity(projectile.transform, "Projectile", isRider);
                return;
            }

            // ── 5. PHYSIQUE GÉNÉRIQUE ───────────────────────────────────
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                if (!_allowPhysicsObjects) { this.VerboseLog("[Teleporter] Physique rejeté (filtre)"); return; }
                if (!Networking.IsOwner(rb.gameObject)) { this.VerboseLog("[Teleporter] Physique rejeté (pas owner)"); return; }
                bool isRider = other.GetComponentInParent<M2922_TeleportRiderMarker>() != null;
                this.VerboseLog($"[Teleporter] Objet physique détecté → {rb.name}");
                TryTeleportEntity(rb.transform, "Physics", isRider);
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

            // Si _alignRotation, pivoter la vélocité interne du projectile
            // pour conserver le mouvement relatif (portal).
            if (_alignRotation && !_resetVelocityOnTeleport)
            {
                M2922_Projectile proj = _pendingEntityTransform.GetComponent<M2922_Projectile>();
                if (proj != null)
                {
                    Quaternion deltaRot = _pendingDestination.rotation * Quaternion.Inverse(transform.rotation);
                    proj.RotateVelocity(deltaRot);
                }
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
                new M2922_GizmoDisplayInfo("Weapons",      _allowWeapons        ? "✔" : "✘", _allowWeapons        ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Projectiles",  _allowProjectiles    ? "✔" : "✘", _allowProjectiles    ? Color.green : Color.gray),
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
