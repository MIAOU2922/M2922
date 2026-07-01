using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Gère le déclenchement du tir (input) et la cadence de tir.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_FireController : M2922_Base
    {
        [Header("=== FIRE RATE ===")]
        [SerializeField] private float _fireRate = 0.1f; // secondes entre chaque tir
        [SerializeField] private float _burstFireRate = 0.05f;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_WeaponComponent _weapon;

        private float _lastFireTime = -999f;
        private bool _triggerHeld = false;
        private int _burstShotsFired = 0;
        private bool _isBursting = false;

        public bool TriggerHeld { set => _triggerHeld = value; }

        protected override void Start()
        {
            base.Start();
            if (_weapon == null) _weapon = GetComponent<M2922_WeaponComponent>();
        }

        protected override void Update()
        {
            base.Update();

            if (_weapon == null) return;

            switch (_weapon.CurrentFireMode)
            {
                case FireMode.Semi:
                    if (_triggerHeld && CanFire())
                    {
                        _weapon.Fire();
                        _triggerHeld = false; // semi-auto : une pression = un tir
                    }
                    break;

                case FireMode.Auto:
                    if (_triggerHeld && CanFire())
                        _weapon.Fire();
                    break;

                case FireMode.Burst:
                    if (_triggerHeld && !_isBursting && CanFire())
                        StartBurst();
                    break;
            }
        }

        private bool CanFire()
        {
            float rate = _isBursting ? _burstFireRate : _fireRate;
            return Time.time - _lastFireTime > rate;
        }

        private void StartBurst()
        {
            _isBursting = true;
            _burstShotsFired = 0;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Fire Rate", $"{_fireRate:F2}s", Color.cyan),
                new M2922_GizmoDisplayInfo("Burst Rate", $"{_burstFireRate:F2}s"),
                new M2922_GizmoDisplayInfo("Firing", _triggerHeld ? "YES" : "NO", _triggerHeld ? Color.red : Color.gray),
            };
        }
#endif
    }
}
