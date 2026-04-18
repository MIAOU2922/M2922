using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Entity;

namespace M2922.World
{
    /// <summary>
    /// Mode d'activation du téléporteur.
    /// </summary>
    public enum TeleporterActivation
    {
        /// <summary>Déclenché automatiquement quand quelque chose entre dans la zone trigger.</summary>
        TriggerZone  = 0,

        /// <summary>Déclenché par un clic joueur. Nécessite un composant VRC_Interactable sur le GO.</summary>
        Interaction  = 1,

        /// <summary>Les deux modes sont actifs simultanément.</summary>
        Both         = 2,
    }

    /// <summary>
    /// Filtre sur ce que le téléporteur peut déplacer.
    /// </summary>
    public enum TeleporterFilter
    {
        /// <summary>Téléporte uniquement les joueurs VRC.</summary>
        PlayersOnly   = 0,

        /// <summary>Téléporte uniquement les M2922_Entity (props, armes ramassables…).</summary>
        EntitiesOnly  = 1,

        /// <summary>Téléporte joueurs ET entités M2922.</summary>
        All           = 2,
    }

    /// <summary>
    /// Téléporteur universel.
    ///
    /// ─── MODES D'ACTIVATION ──────────────────────────────────────────────────
    ///   TriggerZone  → OnPlayerTriggerEnter / OnTriggerEnter (collider Is Trigger requis)
    ///   Interaction  → Interact() (VRC_Interactable requis sur ce GameObject)
    ///   Both         → les deux actifs en même temps
    ///
    /// ─── FILTRE ──────────────────────────────────────────────────────────────
    ///   PlayersOnly  → seulement les joueurs VRC
    ///   EntitiesOnly → seulement les M2922_Entity (props, armes…)
    ///   All          → joueurs + entités
    ///
    /// ─── ACTIVATION EXTERNE ──────────────────────────────────────────────────
    ///   Enable() / Disable() / SetEnabled(bool) → depuis un autre script
    ///   TeleportLocalPlayer()                   → depuis un bouton UI ou script
    ///
    /// ─── NOTES SETUP ─────────────────────────────────────────────────────────
    ///   • TriggerZone : le collider de ce GO doit être en "Is Trigger", layer "MirrorReflection"
    ///     ou utiliser un child dédié en layer Player pour OnPlayerTriggerEnter.
    ///   • Interaction : ajouter VRC_Interactable sur ce GO, régler le texte dans ce composant.
    ///   • OneShot     : désactive automatiquement après la première utilisation.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Teleporter : M2922_System
    {
        // =====================================================================
        // DESTINATION
        // =====================================================================
        [Header("=== DESTINATION ===")]
        [Tooltip("Transform de l'arrivée. Position ET rotation sont utilisées (si _alignRotation = true).")]
        [SerializeField] private Transform _destination;

        // =====================================================================
        // ACTIVATION
        // =====================================================================
        [Header("=== ACTIVATION ===")]
        [Tooltip("TriggerZone : déclenché automatiquement à l'entrée dans la zone.\n" +
                 "Interaction : déclenché par un clic joueur (VRC_Interactable requis).\n" +
                 "Both : les deux modes simultanément.")]
        [SerializeField] private TeleporterActivation _activationMode = TeleporterActivation.TriggerZone;

        [Tooltip("Active ou désactive ce téléporteur. Peut être changé à chaud par un script externe.")]
        [SerializeField] private bool _isEnabled = true;

        // =====================================================================
        // FILTRE
        // =====================================================================
        [Header("=== FILTER ===")]
        [Tooltip("PlayersOnly  : seulement les joueurs.\n" +
                 "EntitiesOnly : seulement les M2922_Entity (props, armes…).\n" +
                 "All          : joueurs + entités.")]
        [SerializeField] private TeleporterFilter _filter = TeleporterFilter.PlayersOnly;

        // =====================================================================
        // OPTIONS
        // =====================================================================
        [Header("=== OPTIONS ===")]
        [Tooltip("Délai en secondes entre l'activation et la téléportation effective (0 = instantané).")]
        [SerializeField] private float _teleportDelay = 0f;

        [Tooltip("Durée minimale (secondes) entre deux téléportations successives.\n" +
                 "Évite les téléportations en rafale si le joueur reste dans la zone.")]
        [SerializeField] private float _cooldown = 1f;

        [Tooltip("Si coché : la rotation du joueur / de l'entité est alignée sur celle de la destination.\n" +
                 "Si décoché : la rotation courante est conservée.")]
        [SerializeField] private bool _alignRotation = true;

        [Tooltip("Si coché : le téléporteur se désactive automatiquement après sa première utilisation.")]
        [SerializeField] private bool _oneShot = false;

        // =====================================================================
        // VFX / SFX
        // =====================================================================
        [Header("=== VFX / SFX ===")]
        [Tooltip("Effet joué au point de départ (sur ce GameObject ou un enfant).")]
        [SerializeField] private ParticleSystem _departEffect;

        [Tooltip("Effet joué au point d'arrivée (à placer près de la destination).")]
        [SerializeField] private ParticleSystem _arrivalEffect;

        [Tooltip("Son joué au moment du départ.")]
        [SerializeField] private AudioSource _teleportAudio;

        // =====================================================================
        // RUNTIME
        // =====================================================================
        private float _lastTeleportTime = -999f;
        private VRCPlayerApi _pendingPlayer;
        private Transform    _pendingEntityTransform;

        // =====================================================================
        // PROPERTIES
        // =====================================================================
        public bool               IsEnabled      => _isEnabled;
        public TeleporterActivation ActivationMode => _activationMode;
        public TeleporterFilter   Filter         => _filter;
        public bool               HasDestination => _destination != null;

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        /// <summary>Active le téléporteur.</summary>
        public void Enable()  { _isEnabled = true;  this.Log("[Teleporter] Activé"); }

        /// <summary>Désactive le téléporteur.</summary>
        public void Disable() { _isEnabled = false; this.Log("[Teleporter] Désactivé"); }

        /// <summary>Active ou désactive le téléporteur selon la valeur passée.</summary>
        public void SetEnabled(bool value)
        {
            _isEnabled = value;
            this.Log($"[Teleporter] Enabled → {value}");
        }

        /// <summary>
        /// Téléporte le joueur local immédiatement, quel que soit le mode d'activation.
        /// Respecte le cooldown, le délai et le oneShot.
        /// Appelable depuis un bouton UI ou un script externe.
        /// </summary>
        public void TeleportLocalPlayer()
        {
            if (!_isEnabled) return;
            TryTeleportPlayer(Networking.LocalPlayer);
        }

        // =====================================================================
        // VRC CALLBACKS — TRIGGER ZONE (joueur)
        // =====================================================================

        /// <summary>Callback VRC — joueur entre dans le trigger (layer Player).</summary>
        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!_isEnabled) return;
            if (_activationMode == TeleporterActivation.Interaction) return;
            if (_filter == TeleporterFilter.EntitiesOnly) return;
            if (!Utilities.IsValid(player)) return;
            // N'agit que pour le joueur local (chaque client se téléporte lui-même)
            if (player.playerId != Networking.LocalPlayer.playerId) return;

            TryTeleportPlayer(player);
        }

        // =====================================================================
        // VRC CALLBACKS — TRIGGER ZONE (entités / rigidbodies)
        // =====================================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!_isEnabled) return;
            if (_activationMode == TeleporterActivation.Interaction) return;
            if (_filter == TeleporterFilter.PlayersOnly) return;
            if (!Utilities.IsValid(other)) return;

            // Cherche une M2922_Entity dans la hiérarchie du collider
            M2922_Entity entity = other.GetComponentInParent<M2922_Entity>();
            if (entity == null) return;

            // N'agit que si le client local est propriétaire de l'entité
            if (!Networking.IsOwner(entity.gameObject)) return;

            TryTeleportEntity(entity.transform);
        }

        // =====================================================================
        // VRC CALLBACK — INTERACTION
        // =====================================================================

        /// <summary>Callback VRC — joueur clique sur cet objet (VRC_Interactable requis).</summary>
        public override void Interact()
        {
            if (!_isEnabled) return;
            if (_activationMode == TeleporterActivation.TriggerZone) return;
            if (_filter == TeleporterFilter.EntitiesOnly) return;

            TryTeleportPlayer(Networking.LocalPlayer);
        }

        // =====================================================================
        // TELEPORT INTERNALS
        // =====================================================================

        private void TryTeleportPlayer(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return;
            if (_destination == null)
            {
                this.Log("[Teleporter] Destination non assignée !");
                return;
            }
            if (Time.time - _lastTeleportTime < _cooldown) return;

            _lastTeleportTime  = Time.time;
            _pendingPlayer     = player;

            PlayDepartEffect();
            PlayTeleportSound();

            if (_teleportDelay <= 0f)
                _DoTeleportPlayer();
            else
                SendCustomEventDelayedSeconds(nameof(_DoTeleportPlayer), _teleportDelay);
        }

        private void TryTeleportEntity(Transform entityTransform)
        {
            if (_destination == null)
            {
                this.Log("[Teleporter] Destination non assignée !");
                return;
            }
            if (Time.time - _lastTeleportTime < _cooldown) return;

            _lastTeleportTime       = Time.time;
            _pendingEntityTransform = entityTransform;

            PlayDepartEffect();
            PlayTeleportSound();

            if (_teleportDelay <= 0f)
                _DoTeleportEntity();
            else
                SendCustomEventDelayedSeconds(nameof(_DoTeleportEntity), _teleportDelay);
        }

        /// <summary>Interne — effectue la téléportation du joueur. Public requis par SendCustomEventDelayedSeconds.</summary>
        public void _DoTeleportPlayer()
        {
            if (!Utilities.IsValid(_pendingPlayer)) { _pendingPlayer = null; return; }
            if (_destination == null)               { _pendingPlayer = null; return; }

            Quaternion targetRot = _alignRotation
                ? _destination.rotation
                : _pendingPlayer.GetRotation();

            _pendingPlayer.TeleportTo(_destination.position, targetRot);
            this.VerboseLog($"[Teleporter] Joueur téléporté → {_destination.name}");
            _pendingPlayer = null;

            PlayArrivalEffect();
            if (_oneShot) Disable();
        }

        /// <summary>Interne — effectue la téléportation de l'entité. Public requis par SendCustomEventDelayedSeconds.</summary>
        public void _DoTeleportEntity()
        {
            if (_pendingEntityTransform == null) return;
            if (_destination == null) { _pendingEntityTransform = null; return; }

            Quaternion targetRot = _alignRotation
                ? _destination.rotation
                : _pendingEntityTransform.rotation;

            _pendingEntityTransform.SetPositionAndRotation(_destination.position, targetRot);
            this.VerboseLog($"[Teleporter] Entité téléportée → {_destination.name}");
            _pendingEntityTransform = null;

            PlayArrivalEffect();
            if (_oneShot) Disable();
        }

        // =====================================================================
        // VFX / SFX
        // =====================================================================

        private void PlayDepartEffect()  { if (_departEffect  != null) _departEffect.Play(); }
        private void PlayArrivalEffect() { if (_arrivalEffect != null) _arrivalEffect.Play(); }
        private void PlayTeleportSound()
        {
            if (_teleportAudio != null && _teleportAudio.clip != null)
                _teleportAudio.PlayOneShot(_teleportAudio.clip);
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _cooldown      = Mathf.Max(0f, _cooldown);
            _teleportDelay = Mathf.Max(0f, _teleportDelay);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo || _destination == null) return;

            Gizmos.color = _isEnabled ? new Color(0f, 1f, 1f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position, _destination.position);
            Gizmos.DrawSphere(_destination.position, _gizmoSize * 0.3f);
        }
#endif
    }
}
