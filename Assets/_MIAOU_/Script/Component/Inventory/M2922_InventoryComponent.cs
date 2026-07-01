using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Inventaire : slots, stockage, poids.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Inventory Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_InventoryComponent : M2922_Base
    {
        [Header("=== INVENTORY ===")]
        [SerializeField] private int _maxSlots = 16;
        [SerializeField] private float _maxWeight = 100f;

        [Header("=== ITEMS ===")]
        [SerializeField] private GameObject[] _items;

        private float _currentWeight = 0f;

        public int MaxSlots => _maxSlots;
        public int UsedSlots => _items != null ? _items.Length : 0;
        public float CurrentWeight => _currentWeight;
        public float MaxWeight => _maxWeight;
        public bool IsFull => UsedSlots >= _maxSlots;

        protected override void Start()
        {
            base.Start();
            _items = new GameObject[_maxSlots];
        }

        public bool AddItem(GameObject item)
        {
            if (IsFull) return false;

            M2922_ItemComponent itemComp = item.GetComponent<M2922_ItemComponent>();
            float itemWeight = itemComp != null ? itemComp.Weight : 0f;
            if (_currentWeight + itemWeight > _maxWeight) return false;

            for (int i = 0; i < _maxSlots; i++)
            {
                if (_items[i] == null)
                {
                    _items[i] = item;
                    _currentWeight += itemWeight;
                    return true;
                }
            }
            return false;
        }

        public GameObject RemoveItem(int slot)
        {
            if (slot < 0 || slot >= _maxSlots || _items[slot] == null) return null;

            GameObject item = _items[slot];
            M2922_ItemComponent itemComp = item.GetComponent<M2922_ItemComponent>();
            _currentWeight -= itemComp != null ? itemComp.Weight : 0f;
            _items[slot] = null;
            return item;
        }

        public GameObject GetItem(int slot)
        {
            if (slot < 0 || slot >= _maxSlots) return null;
            return _items[slot];
        }

        public bool HasFreeSlot() => UsedSlots < _maxSlots;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Slots", $"{UsedSlots} / {_maxSlots}"),
                new M2922_GizmoDisplayInfo("Weight", $"{CurrentWeight:F1} / {_maxWeight:F1}"),
            };
        }
#endif
    }
}
