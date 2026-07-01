using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Inventory;

namespace M2922.Component.Player
{
    /// <summary>
    /// Inventaire spécifique au joueur.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PlayerInventory : M2922_Base
    {
        [Header("=== INVENTORY ===")]
        [SerializeField] private M2922_InventoryComponent _inventory;
        [SerializeField] private M2922_HolderComponent _holder;

        [Header("=== QUICK SLOTS ===")]
        [SerializeField] private GameObject _quickSlot1;
        [SerializeField] private GameObject _quickSlot2;
        [SerializeField] private GameObject _quickSlot3;

        public M2922_InventoryComponent Inventory => _inventory;
        public M2922_HolderComponent Holder => _holder;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_inventory == null) _inventory = GetComponent<M2922_InventoryComponent>();
            if (_holder == null) _holder = GetComponent<M2922_HolderComponent>();
        }

        public void EquipQuickSlot(int slot)
        {
            GameObject item = null;
            switch (slot)
            {
                case 1: item = _quickSlot1; break;
                case 2: item = _quickSlot2; break;
                case 3: item = _quickSlot3; break;
            }

            if (item != null && _holder != null)
                _holder.EquipPrimary(item);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            int count = 0;
            if (_quickSlot1 != null) count++;
            if (_quickSlot2 != null) count++;
            if (_quickSlot3 != null) count++;
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Quick Slots", $"{count} / 3 filled"),
            };
        }
#endif
    }
}
