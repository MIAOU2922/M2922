using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Debug visuel : logs, gizmos runtime, visualisations.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DebugComponent : M2922_Base
    {
        [Header("=== DEBUG ===")]
        [SerializeField] private bool _showDebugInfo = true;
        [SerializeField] private bool _showHitboxes = false;
        [SerializeField] private Color _hitboxColor = Color.green;

        protected override void Start()
        {
            base.Start();
            if (_showDebugInfo)
                this.Log($"[DEBUG] {ScriptName} initialized.");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Debug", _showDebugInfo ? "ON" : "OFF", _showDebugInfo ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Hitboxes", _showHitboxes ? "ON" : "OFF", _showHitboxes ? Color.green : Color.gray),
            };
        }
#endif

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!_showHitboxes) return;

            Gizmos.color = _hitboxColor;
            Collider[] colliders = GetComponents<Collider>();
            foreach (Collider col in colliders)
            {
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
                else if (col is CapsuleCollider capsule)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    // Simplifié : sphere au centre
                    Gizmos.DrawWireSphere(capsule.center, capsule.radius);
                }
            }
        }
#endif
    }
}
