using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Inventory;

namespace M2922.Component.UI
{
    /// <summary>
    /// Interface utilisateur de l'inventaire.
    /// </summary>
    [AddComponentMenu("M2922/UI/Inventory UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_InventoryUI : M2922_Base
    {
        [Header("=== INVENTORY UI ===")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private UnityEngine.UI.Image[] _slotIcons;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_InventoryComponent _inventory;

        private bool _isOpen = false;

        protected override void Start()
        {
            base.Start();
            if (_inventory == null) _inventory = GetComponent<M2922_InventoryComponent>();
            if (_inventoryPanel != null) _inventoryPanel.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            if (Input.GetKeyDown(_toggleKey))
            {
                _isOpen = !_isOpen;
                if (_inventoryPanel != null) _inventoryPanel.SetActive(_isOpen);
            }

            if (_isOpen) RefreshSlots();
        }

        private void RefreshSlots()
        {
            if (_inventory == null || _slotIcons == null) return;

            for (int i = 0; i < _slotIcons.Length; i++)
            {
                if (_slotIcons[i] == null) continue;
                GameObject item = _inventory.GetItem(i);
                _slotIcons[i].enabled = item != null;
                // TODO: set sprite from item
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            int slots = _slotIcons != null ? _slotIcons.Length : 0;
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Open", _isOpen ? "YES" : "NO", _isOpen ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Slots", slots.ToString()),
                new M2922_GizmoDisplayInfo("Inventory", _inventory != null ? "Linked" : "None", _inventory != null ? Color.green : Color.red),
            };
        }
#endif
    }
}
