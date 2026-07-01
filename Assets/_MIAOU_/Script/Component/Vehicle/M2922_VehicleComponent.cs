using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Vehicle
{
    public enum VehicleState { Parked, Occupied, Moving }

    /// <summary>
    /// Contrôle global du véhicule : état, entrée/sortie.
    /// </summary>
    [AddComponentMenu("M2922/Vehicle/Vehicle Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_VehicleComponent : M2922_Base
    {
        [Header("=== VEHICLE ===")]
        [SerializeField] private VehicleState _state = VehicleState.Parked;
        [SerializeField] private float _maxSpeed = 30f;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_SeatComponent _driverSeat;
        [SerializeField] private M2922_VehicleMovement _movement;
        [SerializeField] private M2922_HealthComponent _health;

        public VehicleState State => _state;
        public float MaxSpeed => _maxSpeed;
        public M2922_SeatComponent DriverSeat => _driverSeat;

        protected override void Start()
        {
            base.Start();
            if (_driverSeat == null) _driverSeat = GetComponentInChildren<M2922_SeatComponent>();
            if (_movement == null) _movement = GetComponent<M2922_VehicleMovement>();
            if (_health == null) _health = GetComponent<M2922_HealthComponent>();
        }

        protected override void Update()
        {
            base.Update();

            if (_health != null && _health.IsDead)
                _state = VehicleState.Parked;

            if (_driverSeat != null && _driverSeat.IsOccupied)
                _state = VehicleState.Occupied;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            Color stateColor = _state == VehicleState.Moving ? Color.green : (_state == VehicleState.Occupied ? Color.yellow : Color.gray);
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("State", _state.ToString(), stateColor),
                new M2922_GizmoDisplayInfo("Max Speed", $"{_maxSpeed} u/s"),
            };
        }
#endif
    }
}
