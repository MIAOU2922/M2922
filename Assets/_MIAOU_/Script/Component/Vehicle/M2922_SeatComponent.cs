using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Vehicle
{
    /// <summary>
    /// Place dans un véhicule : occupation joueur, contrôle input.
    /// </summary>
    [AddComponentMenu("M2922/Vehicle/Seat Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_SeatComponent : M2922_Base
    {
        [Header("=== SEAT ===")]
        [SerializeField] private bool _isDriverSeat = false;
        [SerializeField] private Transform _sitPosition;
        [SerializeField] private KeyCode _enterKey = KeyCode.E;

        private VRCPlayerApi _occupant = null;
        private bool _isOccupied = false;

        public bool IsOccupied => _isOccupied;
        public VRCPlayerApi Occupant => _occupant;
        public bool IsDriverSeat => _isDriverSeat;

        public void Enter(VRCPlayerApi player)
        {
            if (_isOccupied) return;
            _occupant = player;
            _isOccupied = true;
            if (_sitPosition != null)
                player.TeleportTo(_sitPosition.position, _sitPosition.rotation);
            this.Log($"Player {player.displayName} entered seat.");
        }

        public void Exit()
        {
            if (!_isOccupied) return;
            string occName = _occupant != null ? _occupant.displayName : "null";
            this.Log($"Player {occName} exited seat.");
            _occupant = null;
            _isOccupied = false;
        }

        protected override void Update()
        {
            base.Update();

            if (_isOccupied && Input.GetKeyDown(_enterKey))
                Exit();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string occ = _occupant != null ? _occupant.displayName : "Empty";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Driver", _isDriverSeat ? "YES" : "NO", _isDriverSeat ? Color.yellow : Color.gray),
                new M2922_GizmoDisplayInfo("Occupant", occ, _isOccupied ? Color.green : Color.gray),
            };
        }
#endif
    }
}
