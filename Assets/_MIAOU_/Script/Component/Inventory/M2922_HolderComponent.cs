using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Inventory
{
    public enum SlotType { Primary, Secondary, Tool, Magazine }

    /// <summary>
    /// Slots spécifiques : armes, magazines, outils. Accès rapide.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HolderComponent : M2922_Base
    {
        [Header("=== SLOTS ===")]
        [SerializeField] private Transform _primarySlot;
        [SerializeField] private Transform _secondarySlot;
        [SerializeField] private Transform _toolSlot;

        [Header("=== EQUIPPED ===")]
        [SerializeField] private GameObject _primaryWeapon;
        [SerializeField] private GameObject _secondaryWeapon;

        public GameObject PrimaryWeapon => _primaryWeapon;
        public GameObject SecondaryWeapon => _secondaryWeapon;

        public void EquipPrimary(GameObject weapon)
        {
            if (_primaryWeapon != null) UnequipPrimary();
            _primaryWeapon = weapon;
            if (weapon != null && _primarySlot != null)
                weapon.transform.SetParent(_primarySlot, false);
        }

        public void EquipSecondary(GameObject weapon)
        {
            if (_secondaryWeapon != null) UnequipSecondary();
            _secondaryWeapon = weapon;
            if (weapon != null && _secondarySlot != null)
                weapon.transform.SetParent(_secondarySlot, false);
        }

        public void UnequipPrimary()
        {
            if (_primaryWeapon != null)
            {
                _primaryWeapon.transform.SetParent(null);
                _primaryWeapon = null;
            }
        }

        public void UnequipSecondary()
        {
            if (_secondaryWeapon != null)
            {
                _secondaryWeapon.transform.SetParent(null);
                _secondaryWeapon = null;
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string p = _primaryWeapon != null ? _primaryWeapon.name : "None";
            string s = _secondaryWeapon != null ? _secondaryWeapon.name : "None";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Primary", p, _primaryWeapon != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Secondary", s, _secondaryWeapon != null ? Color.green : Color.gray),
            };
        }
#endif
    }
}
