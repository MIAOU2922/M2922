using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Ammo;

namespace M2922.Component.Weapon
{
    public enum FireMode { Semi, Auto, Burst }

    /// <summary>
    /// Logique d'arme : fire mode, damage, portée.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_WeaponComponent : M2922_Base
    {
        [Header("=== WEAPON CONFIG ===")]
        [SerializeField] private FireMode _fireMode = FireMode.Auto;
        [SerializeField] private float _baseDamage = 25f;
        [SerializeField] private float _range = 100f;
        [SerializeField] private int _burstCount = 3;
        [SerializeField] private float _burstInterval = 0.05f;

        [Header("=== REFERENCES ===")]
        [SerializeField] private Transform _firePoint;
        [SerializeField] private M2922_FireController _fireController;
        [SerializeField] private M2922_SpreadComponent _spread;
        [SerializeField] private M2922_RecoilComponent _recoil;
        [SerializeField] private M2922_ReloadComponent _reload;
        [SerializeField] private M2922_MagazineComponent _magazine;

        public FireMode CurrentFireMode => _fireMode;
        public float BaseDamage => _baseDamage;
        public float Range => _range;
        public Transform FirePoint => _firePoint;
        public M2922_MagazineComponent Magazine => _magazine;

        protected override void Start()
        {
            base.Start();
            if (_fireController == null) _fireController = GetComponent<M2922_FireController>();
            if (_spread == null) _spread = GetComponent<M2922_SpreadComponent>();
            if (_recoil == null) _recoil = GetComponent<M2922_RecoilComponent>();
            if (_reload == null) _reload = GetComponent<M2922_ReloadComponent>();
            if (_magazine == null) _magazine = GetComponent<M2922_MagazineComponent>();
        }

        public bool CanFire()
        {
            if (_reload != null && _reload.IsReloading) return false;
            if (_magazine != null && !_magazine.HasAmmo()) return false;
            return true;
        }

        public void Fire()
        {
            if (!CanFire()) return;

            if (_magazine != null) _magazine.ConsumeAmmo();
            if (_recoil != null) _recoil.ApplyRecoil();
            // Le FireController gère la cadence et appelle Fire() au bon rythme
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Mode", _fireMode.ToString(), Color.cyan),
                new M2922_GizmoDisplayInfo("Damage", $"{_baseDamage:F1}", Color.red),
                new M2922_GizmoDisplayInfo("Range", $"{_range:F1}m"),
            };
        }
#endif
    }
}
