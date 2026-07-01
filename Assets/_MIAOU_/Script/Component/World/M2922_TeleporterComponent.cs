using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.World
{
    /// <summary>
    /// Téléporteur : point A → point B.
    /// </summary>
    [AddComponentMenu("M2922/World/Teleporter Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_TeleporterComponent : M2922_Base
    {
        [Header("=== TELEPORT ===")]
        [SerializeField] private Transform _destination;
        [SerializeField] private bool _preserveVelocity = false;
        [SerializeField] private float _cooldown = 1f;
        [SerializeField] private Rigidbody _targetRigidbody;

        private float _lastTeleportTime = -999f;

        public void Teleport(VRCPlayerApi player)
        {
            if (_destination == null) return;
            if (Time.time - _lastTeleportTime < _cooldown) return;

            _lastTeleportTime = Time.time;

            Vector3 vel = Vector3.zero;
            if (_preserveVelocity && _targetRigidbody != null)
                vel = _targetRigidbody.velocity;

            player.TeleportTo(_destination.position, _destination.rotation);

            if (_preserveVelocity && _targetRigidbody != null)
                _targetRigidbody.velocity = vel;

            this.Log($"Teleported to {_destination.name}");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string dest = _destination != null ? _destination.name : "NONE";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Dest", dest, _destination != null ? Color.green : Color.red),
                new M2922_GizmoDisplayInfo("Cooldown", $"{_cooldown}s"),
                new M2922_GizmoDisplayInfo("Velocity", _preserveVelocity ? "Keep" : "Reset"),
            };
        }
#endif
    }
}
