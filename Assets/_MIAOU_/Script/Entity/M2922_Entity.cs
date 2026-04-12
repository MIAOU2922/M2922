using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Entity
{
    /// Classe de base pour toute entité du monde PvP.
    /// Inline le contrat IEntity — UdonSharp ne supporte pas les interfaces au runtime.
    public class M2922_Entity : M2922_Base
    {
        // === ENTITY SETTINGS ===
        [Header("=== ENTITY SETTINGS ===")]
        [SerializeField] protected int _entityId;
        [SerializeField] protected string _entityName = "Unnamed Entity";
        [SerializeField] protected EntityType _entityType = EntityType.None;

        // === IEntity (inline) ===
        public int EntityId              => _entityId;
        public string EntityName         => _entityName;
        public EntityType Type           => _entityType;
        public Transform EntityTransform => transform;
        public bool IsActive             => gameObject.activeInHierarchy;

        public void SetEntityName(string name) { _entityName = name; }

        // === LIFECYCLE ===
        protected override void Start()
        {
            base.Start();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
        }
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 3f),
                $"Entity\nName: {EntityName}\nType: {Type}\nID: {EntityId}"
            );
        }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _gizmoSize * 1f);
        }
#endif
    }
}
