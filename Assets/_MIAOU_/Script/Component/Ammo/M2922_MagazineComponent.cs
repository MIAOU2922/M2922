using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Ammo
{
    /// <summary>
    /// Chargeur : capacité, munitions actuelles, type.
    /// </summary>
    [AddComponentMenu("M2922/Ammo/Magazine Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_MagazineComponent : M2922_Base
    {
        [Header("=== MAGAZINE ===")]
        [SerializeField] private int _capacity = 30;
        [UdonSynced] private int _currentAmmo = 30;
        [SerializeField] private M2922_AmmoTypeComponent _ammoType;

        public int Capacity => _capacity;
        public int CurrentAmmo => _currentAmmo;
        public bool IsEmpty => _currentAmmo <= 0;
        public bool IsFull => _currentAmmo >= _capacity;
        public M2922_AmmoTypeComponent AmmoType => _ammoType;

        public bool HasAmmo(int amount = 1)
        {
            return _currentAmmo >= amount;
        }

        public void ConsumeAmmo(int amount = 1)
        {
            _currentAmmo = Mathf.Max(0, _currentAmmo - amount);
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public void Refill(int amount = -1)
        {
            _currentAmmo = amount < 0 ? _capacity : Mathf.Min(_capacity, _currentAmmo + amount);
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public void SetAmmoType(M2922_AmmoTypeComponent type)
        {
            _ammoType = type;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            Color ammoColor = IsEmpty ? Color.red : (IsFull ? Color.green : Color.yellow);
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Ammo", $"{_currentAmmo} / {_capacity}", ammoColor),
                new M2922_GizmoDisplayInfo("Type", _ammoType != null ? _ammoType.AmmoName : "None"),
            };
        }
#endif
    }
}
