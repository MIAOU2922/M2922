using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Projectile
{
    /// <summary>
    /// Effets visuels liés aux projectiles : trails, impacts, beams.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_VFXProjectile : M2922_Base
    {
        [Header("=== VFX ===")]
        [SerializeField] private TrailRenderer _trail;
        [SerializeField] private GameObject _impactEffect;
        [SerializeField] private GameObject _muzzleFlash;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_ProjectileComponent _projectile;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_projectile == null) _projectile = GetComponent<M2922_ProjectileComponent>();
        }

        public void PlayMuzzleFlash()
        {
            if (_muzzleFlash != null)
            {
                GameObject flash = Instantiate(_muzzleFlash, transform.position, transform.rotation);
                Destroy(flash, 0.1f);
            }
        }

        public void PlayImpact(Vector3 position, Vector3 normal)
        {
            if (_impactEffect != null)
            {
                GameObject impact = Instantiate(_impactEffect, position, Quaternion.LookRotation(normal));
                Destroy(impact, 2f);
            }
        }

        public void EnableTrail(bool enable)
        {
            if (_trail != null)
                _trail.emitting = enable;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Trail", _trail != null ? "ON" : "OFF", _trail != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Impact FX", _impactEffect != null ? "ON" : "OFF"),
                new M2922_GizmoDisplayInfo("Muzzle FX", _muzzleFlash != null ? "ON" : "OFF"),
            };
        }
#endif
    }
}
