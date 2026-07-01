using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Vehicle
{
    /// <summary>
    /// Physique de véhicule : voiture, avion, bateau.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_VehicleMovement : M2922_Base
    {
        [Header("=== MOVEMENT ===")]
        [SerializeField] private float _acceleration = 500f;
        [SerializeField] private float _maxSpeed = 30f;
        [SerializeField] private float _turnSpeed = 50f;
        [SerializeField] private float _brakeForce = 1000f;

        [Header("=== WHEELS ===")]
        [SerializeField] private WheelCollider[] _driveWheels;
        [SerializeField] private WheelCollider[] _steerWheels;

        [Header("=== INPUT ===")]
        [SerializeField] private M2922_SeatComponent _driverSeat;

        private Rigidbody _rb;
        private float _throttle;
        private float _steering;
        private bool _braking;

        protected override void Start()
        {
            base.Start();
            _rb = GetComponent<Rigidbody>();
            if (_driverSeat == null) _driverSeat = GetComponent<M2922_SeatComponent>();
        }

        protected override void Update()
        {
            base.Update();

            // Input si occupé
            if (_driverSeat != null && _driverSeat.IsOccupied)
            {
                _throttle = Input.GetAxis("Vertical");
                _steering = Input.GetAxis("Horizontal");
                _braking = Input.GetKey(KeyCode.Space);
            }
            else
            {
                _throttle = 0f;
                _steering = 0f;
                _braking = false;
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Max Speed", $"{_maxSpeed} u/s", Color.cyan),
                new M2922_GizmoDisplayInfo("Throttle", $"{_throttle:F2}"),
                new M2922_GizmoDisplayInfo("Steering", $"{_steering:F2}"),
                new M2922_GizmoDisplayInfo("Brake", _braking ? "ON" : "OFF", _braking ? Color.red : Color.gray),
            };
        }
#endif

        private void FixedUpdate()
        {
            // Moteur
            foreach (WheelCollider wheel in _driveWheels)
            {
                if (wheel == null) continue;
                wheel.motorTorque = _throttle * _acceleration;
                if (_braking) wheel.brakeTorque = _brakeForce;
                else wheel.brakeTorque = 0f;
            }

            // Direction
            foreach (WheelCollider wheel in _steerWheels)
            {
                if (wheel == null) continue;
                wheel.steerAngle = _steering * _turnSpeed;
            }
        }
    }
}
