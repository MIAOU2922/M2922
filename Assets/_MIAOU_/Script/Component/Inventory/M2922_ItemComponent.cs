using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Inventory
{
    public enum ItemCategory { Weapon, Ammo, Health, Armor, Key, Misc }

    /// <summary>
    /// Objet générique : pickupable, usable, poids, nom.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ItemComponent : M2922_Base
    {
        [Header("=== ITEM ===")]
        [SerializeField] private string _itemName = "Item";
        [SerializeField] private ItemCategory _category = ItemCategory.Misc;
        [SerializeField] private float _weight = 1f;
        [SerializeField] private bool _isStackable = false;
        [SerializeField] private int _maxStack = 1;

        public string ItemName => _itemName;
        public ItemCategory Category => _category;
        public float Weight => _weight;
        public bool IsStackable => _isStackable;
        public int MaxStack => _maxStack;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Name", _itemName, Color.cyan),
                new M2922_GizmoDisplayInfo("Category", _category.ToString()),
                new M2922_GizmoDisplayInfo("Weight", $"{_weight:F1}"),
            };
        }
#endif
    }
}
