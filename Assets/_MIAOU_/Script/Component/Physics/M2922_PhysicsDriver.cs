using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Physics
{
    /// <summary>
    /// Interaction avec le Rigidbody : forces, impulses, vélocité.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PhysicsDriver : M2922_Base
    {
        [Header("=== PHYSICS ===")]
        [SerializeField] private float _mass = 1f;
        [SerializeField] private float _drag = 0f;

        private Rigidbody _rb;

        protected override void Start()
        {
            base.Start();
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.mass = _mass;
                _rb.drag = _drag;
            }
        }

        public void ApplyForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (_rb != null) _rb.AddForce(force, mode);
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            if (_rb != null) _rb.AddForce(impulse, ForceMode.Impulse);
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (_rb != null) _rb.velocity = velocity;
        }

        public Vector3 GetVelocity()
        {
            return _rb != null ? _rb.velocity : Vector3.zero;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Mass", $"{_mass:F1}"),
                new M2922_GizmoDisplayInfo("Drag", $"{_drag:F2}"),
            };
        }
#endif
    }
}
