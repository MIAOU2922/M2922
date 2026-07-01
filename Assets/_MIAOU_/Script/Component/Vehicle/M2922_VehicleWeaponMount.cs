using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Vehicle
{
    /// <summary>
    /// Support pour armes montées sur véhicule (tourelles, mitrailleuses).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_VehicleWeaponMount : M2922_Base
    {
        [Header("=== MOUNT ===")]
        [SerializeField] private Transform _mountPoint;
        [SerializeField] private float _rotationSpeed = 30f;
        [SerializeField] private float _pitchMin = -20f;
        [SerializeField] private float _pitchMax = 60f;

        [Header("=== WEAPON ===")]
        [SerializeField] private GameObject _mountedWeapon;

        private float _currentYaw = 0f;
        private float _currentPitch = 0f;

        public void MountWeapon(GameObject weapon)
        {
            if (_mountedWeapon != null) UnmountWeapon();
            _mountedWeapon = weapon;
            if (weapon != null && _mountPoint != null)
                weapon.transform.SetParent(_mountPoint, false);
        }

        public void UnmountWeapon()
        {
            if (_mountedWeapon != null)
            {
                _mountedWeapon.transform.SetParent(null);
                _mountedWeapon = null;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (_mountPoint == null) return;

            float yawInput = Input.GetAxis("Mouse X") * _rotationSpeed * Time.deltaTime;
            float pitchInput = Input.GetAxis("Mouse Y") * _rotationSpeed * Time.deltaTime;

            _currentYaw += yawInput;
            _currentPitch = Mathf.Clamp(_currentPitch - pitchInput, _pitchMin, _pitchMax);

            _mountPoint.localRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string weapon = _mountedWeapon != null ? _mountedWeapon.name : "None";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Weapon", weapon, _mountedWeapon != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Yaw", $"{_currentYaw:F1}°"),
                new M2922_GizmoDisplayInfo("Pitch", $"{_currentPitch:F1}°"),
            };
        }
#endif
    }
}
