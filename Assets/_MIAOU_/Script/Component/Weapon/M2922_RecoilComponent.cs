using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Gestion du recul : kickback visuel + mécanique.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_RecoilComponent : M2922_Base
    {
        [Header("=== RECOIL ===")]
        [SerializeField] private float _recoilAmount = 2f;
        [SerializeField] private float _recoilRecoverySpeed = 5f;
        [SerializeField] private float _maxRecoil = 10f;

        [Header("=== VISUAL ===")]
        [SerializeField] private Transform _weaponTransform;
        [SerializeField] private Vector3 _recoilRotation = new Vector3(-5f, 0f, 0f);

        private float _currentRecoil = 0f;
        private Vector3 _targetRotation;

        protected override void Start()
        {
            base.Start();
            if (_weaponTransform == null) _weaponTransform = transform;
        }

        protected override void Update()
        {
            base.Update();
            // Retour au repos
            _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, _recoilRecoverySpeed * Time.deltaTime);

            if (_weaponTransform != null)
                _weaponTransform.localRotation = Quaternion.Lerp(
                    _weaponTransform.localRotation,
                    Quaternion.Euler(_recoilRotation * _currentRecoil),
                    _recoilRecoverySpeed * Time.deltaTime);
        }

        public void ApplyRecoil()
        {
            _currentRecoil = Mathf.Min(_maxRecoil, _currentRecoil + _recoilAmount);
        }

        public void ResetRecoil()
        {
            _currentRecoil = 0f;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Amount", $"{_recoilAmount:F2}"),
                new M2922_GizmoDisplayInfo("Recovery", $"{_recoilRecoverySpeed:F1}"),
                new M2922_GizmoDisplayInfo("Current", $"{_currentRecoil:F2} / {_maxRecoil:F2}"),
            };
        }
#endif
    }
}
