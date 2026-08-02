using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Relay pickup events du parent (VRC Pickup + VRC Object Sync)
    /// vers le M2922_WeaponFireHandler situé sur un enfant.
    /// 
    /// À placer sur le même GameObject que le VRC Pickup.
    /// Sync mode = None (pas de conflit avec VRC Object Sync).
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Pickup Relay")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_PickupRelay : M2922_Base
    {
        [Header("=== TARGET ===")]
        [Tooltip("FireHandler de l'arme (sur un enfant).")]
        [SerializeField] private M2922_WeaponFireHandler _fireHandler;

        protected override void Start()
        {
            base.Start();

            if (_fireHandler == null)
                _fireHandler = GetComponentInChildren<M2922_WeaponFireHandler>();

            if (_fireHandler == null)
                this.Error("[PickupRelay] Aucun M2922_WeaponFireHandler trouvé !");
        }

        public override void OnPickup()
        {
            if (_fireHandler != null) _fireHandler.HandlePickup();
        }

        public override void OnDrop()
        {
            if (_fireHandler != null) _fireHandler.HandleDrop();
        }

        public override void OnPickupUseDown()
        {
            if (_fireHandler != null) _fireHandler.HandlePickupUseDown();
        }

        public override void OnPickupUseUp()
        {
            if (_fireHandler != null) _fireHandler.HandlePickupUseUp();
        }
    }
}
