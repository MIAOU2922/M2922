using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Physics
{
    /// <summary>
    /// Gestion de gravité personnalisée ou override de la gravité Unity.
    /// </summary>
    [AddComponentMenu("M2922/Physics/Gravity Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_GravityComponent : M2922_Base
    {
        [Header("=== GRAVITY ===")]
        [SerializeField] private bool _overrideGravity = false;
        [SerializeField] private Vector3 _gravityDirection = new Vector3(0f, -9.81f, 0f);
        [SerializeField] private float _gravityScale = 1f;

        private Rigidbody _rb;

        public float GravityScale => _gravityScale;

        protected override void Start()
        {
            base.Start();
            _rb = GetComponent<Rigidbody>();
        }

        public Vector3 GetGravity()
        {
            if (_overrideGravity)
                return _gravityDirection.normalized * _gravityDirection.magnitude * _gravityScale;
            return UnityEngine.Physics.gravity * _gravityScale;
        }

        public void SetGravityScale(float scale)
        {
            _gravityScale = scale;
        }

        public void SetOverride(bool enabled, Vector3 direction)
        {
            _overrideGravity = enabled;
            _gravityDirection = direction;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Override", _overrideGravity ? "ON" : "OFF", _overrideGravity ? Color.cyan : Color.gray),
                new M2922_GizmoDisplayInfo("Scale", $"x{_gravityScale:F1}"),
            };
        }
#endif
    }
}
