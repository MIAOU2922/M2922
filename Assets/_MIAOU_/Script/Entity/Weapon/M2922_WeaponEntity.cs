using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Combat;
using M2922.Entity;
using EventType = M2922.Core.EventType;

namespace M2922.Entity.Weapon
{
    /// <summary>
    /// Arme en tant qu'entité — version ramassable via VRC_Pickup.
    ///
    /// Ce composant est l'ENTITÉ : il gère l'identité, la prise en main, le lâché et
    /// le retour à l'origine. La logique de tir/munitions/rechargement est déléguée
    /// à M2922_Weapon (sur le même GameObject ou référencé en inspector).
    ///
    ///  ─── POSITION DANS L'ARCHI ────────────────────────────────────────────────
    ///   M2922_Weapon    = système (cannon de char, tourelle, arme tenue…)
    ///   M2922_WeaponEntity = UNE DES utilisations : arme ramassable par un joueur
    ///
    /// ─── PICKUP ───────────────────────────────────────────────────────────────
    ///   OnPickup()       → SetOwner, attribue attackerId à la Weapon, publie Equipped
    ///   OnPickupUseDown() → Fire (semi-auto) + démarre _triggerHeld (auto)
    ///   OnPickupUseUp()  → relâche le trigger
    ///   OnDrop()         → retire attackerId, publie Dropped, démarre timer retour
    ///
    /// ─── RECHARGEMENT MANUEL (ReloadMode.Manual) ─────────────────────────────
    ///   En VR : trigger de la main OPPOSÉE à celle qui tient l'arme
    ///   Sur PC desktop : touche [R]
    ///   → appelle Weapon.TriggerReload()
    ///
    /// ─── RETOUR À L'ORIGINE ───────────────────────────────────────────────────
    ///   _returnDelay >= 0 : après ce délai (sec), l'arme retourne à son origine
    ///   _returnDelay  < 0 : permanente là où elle est posée (ReturnToOrigin() reste public)
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_WeaponEntity : M2922_Entity
    {
        // =====================================================================
        // WEAPON SYSTEM REFERENCE
        // =====================================================================
        [Header("=== WEAPON SYSTEM ===")]
        [Tooltip("Référence au M2922_Weapon. Si vide, cherché automatiquement sur ce GameObject.")]
        [SerializeField] private M2922_Weapon _weapon;

        // =====================================================================
        // PICKUP CONFIG
        // =====================================================================
        [Header("=== PICKUP CONFIG ===")]
        [Tooltip("Temps (sec) avant que l'arme retourne à son origine après avoir été lâchée.\n" +
                 "-1 = l'arme reste là où elle est définitivement (jusqu'à appel de ReturnToOrigin()).")]
        [SerializeField] private float _returnDelay = 10f;

        // =====================================================================
        // RUNTIME
        // =====================================================================
        private bool  _isEquipped;
        private bool  _triggerHeld;
        private bool  _reloadInputHeld;            // évite répétition sur axe continu
        private float _dropTime;
        private bool  _pendingReturn;

        // Origine de spawn (position + rotation au Start)
        [UdonSynced] private Vector3    _originPosition;
        [UdonSynced] private Quaternion _originRotation;

        // =====================================================================
        // LIFECYCLE
        // =====================================================================
        protected override void Awake()
        {
            base.Awake();
            _entityType = EntityType.Weapon;

            if (_weapon == null)
                _weapon = GetComponent<M2922_Weapon>();

            if (_weapon == null)
                M2922_Debug.Error("[WeaponEntity] M2922_Weapon introuvable ! " +
                                  "Assigne-le en inspector ou ajoute-le sur ce GameObject.", this);
        }

        protected override void Start()
        {
            base.Start();

            _originPosition = transform.position;
            _originRotation = transform.rotation;

            if (Networking.IsOwner(gameObject))
                RequestSerialization();
        }

        protected override void Update()
        {
            base.Update();
            if (!_isEquipped) return;
            if (!Networking.IsOwner(gameObject)) return;

            // ── Auto-fire (arme automatique) ──────────────────────────────────
            if (_weapon != null && _weapon.UseProjectile == false && _triggerHeld && _weapon.CanFire)
                _weapon.Fire(null);   // muzzle géré par la Weapon elle-même

            // ── Reload input (ReloadMode.Manual) ─────────────────────────────
            if (_weapon != null && _weapon.CurrentReloadMode == ReloadMode.Manual)
                CheckReloadInput();

            // ── Retour auto (déclenché après lâché dans OnDrop) ──────────────
            if (_pendingReturn && _returnDelay >= 0f
                && Time.time - _dropTime >= _returnDelay)
            {
                _pendingReturn = false;
                ReturnToOrigin();
            }
        }

        // =====================================================================
        // VRCPICKUP CALLBACKS
        // =====================================================================
        public override void OnPickup()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (player == null) return;

            Networking.SetOwner(player, gameObject);

            if (_weapon != null)
                _weapon.SetAttackerId(player.playerId);

            _isEquipped    = true;
            _pendingReturn = false;
            _triggerHeld   = false;

            PublishWeaponEvent(EventType.OnWeaponEquipped);
            this.Log($"[WeaponEntity] {_entityName} pris par {player.displayName}");
        }

        public override void OnDrop()
        {
            if (_weapon != null)
                _weapon.SetAttackerId(-1);

            _isEquipped    = false;
            _triggerHeld   = false;
            _reloadInputHeld = false;

            PublishWeaponEvent(EventType.OnWeaponDropped);
            this.Log($"[WeaponEntity] {_entityName} lâché");

            // Démarrer le timer de retour si configuré
            if (_returnDelay >= 0f)
            {
                _dropTime      = Time.time;
                _pendingReturn = true;
            }
            // _returnDelay < 0 → reste là où c'est, _pendingReturn reste false
        }

        public override void OnPickupUseDown()
        {
            if (_weapon == null) return;
            _triggerHeld = true;

            if (_weapon.CanFire)
                _weapon.Fire(null);
            else if (!_weapon.CanFire && !_weapon.IsReloading)
                _weapon.PlayEmptySound();
        }

        public override void OnPickupUseUp()
        {
            _triggerHeld = false;
        }

        // =====================================================================
        // RETURN TO ORIGIN
        // =====================================================================

        /// <summary>
        /// Retourne l'arme à sa position d'origine (spawn).
        /// Appelable même si _returnDelay est -1 (permanent).
        /// </summary>
        public void ReturnToOrigin()
        {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            transform.SetPositionAndRotation(_originPosition, _originRotation);
            this.Log($"[WeaponEntity] {_entityName} retournée à l'origine");
        }

        // =====================================================================
        // RELOAD INPUT CHECK
        // =====================================================================

        /// <summary>
        /// Détecte le trigger de la main opposée (VR) ou la touche [R] (desktop)
        /// pour déclencher le rechargement manuel.
        /// </summary>
        private void CheckReloadInput()
        {
            bool reloadPressed = false;

            // ── VR : trigger de la main opposée ──────────────────────────────
            var pickup = GetComponent<VRC_Pickup>();
            if (pickup != null)
            {
                float oppositeAxis = 0f;
                if (pickup.currentHand == VRC_Pickup.PickupHand.Right)
                    // Arme en main droite → trigger gauche (Primary)
                    oppositeAxis = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");
                else if (pickup.currentHand == VRC_Pickup.PickupHand.Left)
                    // Arme en main gauche → trigger droit (Secondary)
                    oppositeAxis = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");

                reloadPressed = oppositeAxis > 0.9f;
            }

            // ── Desktop : touche [R] ──────────────────────────────────────────
            if (!reloadPressed)
                reloadPressed = Input.GetKeyDown(KeyCode.R);

            // ── Déclenchement unique par pression ────────────────────────────
            if (reloadPressed && !_reloadInputHeld)
            {
                _reloadInputHeld = true;
                if (_weapon != null) _weapon.TriggerReload();
            }
            else if (!reloadPressed && !Input.GetKeyDown(KeyCode.R))
            {
                _reloadInputHeld = false;
            }
        }

        // =====================================================================
        // EVENT PUBLISHING
        // =====================================================================

        private void PublishWeaponEvent(EventType eventType)
        {
            if (_weapon == null) return;
            if (Manager == null || Manager.EventBus == null) return;

            M2922_EventData slot = Manager.EventBus.RentSlot();
            if (slot == null) return;

            slot.WeaponName   = _entityName;
            slot.WeaponType   = (int)_weapon.WeaponType;
            slot.CurrentAmmo  = _weapon.CurrentAmmo;
            slot.ReserveAmmo  = _weapon.ReserveAmmo;

            VRCPlayerApi local = Networking.LocalPlayer;
            if (local != null) slot.AttackerId = local.playerId;

            slot.WeaponObject = gameObject;

            Manager.EventBus.Publish(eventType, slot);
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _entityType = EntityType.Weapon;

            if (_entityName == "Unnamed Entity" || string.IsNullOrEmpty(_entityName))
            {
                if (_weapon != null)
                    _entityName = _weapon.WeaponName;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;

            string retInfo = _returnDelay < 0f ? "Permanente" : $"Retour dans {_returnDelay}s";
            string wepInfo = _weapon != null
                ? $"Weapon: {_weapon.WeaponName} ({_weapon.WeaponType})\n" +
                  $"Reload: {_weapon.CurrentReloadMode}"
                : "(aucun M2922_Weapon)";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 5f),
                $"[WeaponEntity] {_entityName}\n{wepInfo}\n{retInfo}"
            );
        }
#endif
    }
}
