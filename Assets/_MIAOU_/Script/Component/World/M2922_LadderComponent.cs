using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.World
{
    /// <summary>
    /// Échelle / escalade : zones start/end, vitesse.
    /// </summary>
    [AddComponentMenu("M2922/World/Ladder Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_LadderComponent : M2922_Base
    {
        [Header("=== LADDER ===")]
        [SerializeField] private Transform _startPoint;
        [SerializeField] private Transform _endPoint;
        [SerializeField] private float _climbSpeed = 3f;

        private VRCPlayerApi _currentClimber = null;

        public void StartClimb(VRCPlayerApi player)
        {
            _currentClimber = player;
        }

        public void StopClimb()
        {
            _currentClimber = null;
        }

        public Vector3 GetClimbDirection()
        {
            if (_startPoint == null || _endPoint == null) return Vector3.up;
            return (_endPoint.position - _startPoint.position).normalized;
        }

        public float GetProgress()
        {
            if (_startPoint == null || _endPoint == null || _currentClimber == null) return 0f;
            Vector3 total = _endPoint.position - _startPoint.position;
            Vector3 current = _currentClimber.GetPosition() - _startPoint.position;
            return Vector3.Dot(current, total.normalized) / total.magnitude;
        }

        public bool HasReachedTop() => GetProgress() >= 1f;
        public bool HasReachedBottom() => GetProgress() <= 0f;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string climber = _currentClimber != null ? _currentClimber.displayName : "None";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Speed", $"{_climbSpeed} m/s"),
                new M2922_GizmoDisplayInfo("Climber", climber, _currentClimber != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Progress", $"{GetProgress() * 100f:F0}%"),
            };
        }
#endif
    }
}
