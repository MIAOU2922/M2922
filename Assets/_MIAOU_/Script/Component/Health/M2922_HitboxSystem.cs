using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Système de hitboxes d'une entité (joueur, prop, véhicule).
    /// Centralise les Collider qui représentent le corps de l'entité.
    /// Permet au projectile/HitDetector d'identifier à qui appartient un collider touché,
    /// et de savoir si c'est une zone critique (tête, point faible).
    ///
    /// SETUP :
    ///   1. Attacher ce composant sur le GameObject de l'entité.
    ///   2. Créer des GO enfants (Head, Body, Legs…) avec des Collider (Is Trigger).
    ///   3. Optionnel : M2922_DamageMultiplier sur les colliders (tête ×2, jambes ×0.5...).
    ///   4. Assigner les colliders dans _hitboxColliders.
    ///
    /// UTILISATION (HitDetector) :
    ///   M2922_HitboxSystem hitboxSys = col.GetComponentInParent&lt;M2922_HitboxSystem&gt;();
    ///   if (hitboxSys != null && hitboxSys.IsMyCollider(col))
    ///       receiver.ApplyDamage(damage, owner, isCrit);
    /// </summary>
    [AddComponentMenu("M2922/Health/Hitbox System")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HitboxSystem : M2922_Base
    {
        [Header("=== HITBOX CONFIG ===")]
        [Tooltip("Colliders du corps de cette entité. Chaque collider doit être un enfant de ce GameObject.")]
        [SerializeField] private Collider[] _hitboxColliders = new Collider[0];
        [Tooltip("Nom du layer Unity pour les hitboxes.")]
        [SerializeField] private string _hitboxLayerName = "Hitbox";
        [Tooltip("Layer ID résolu automatiquement (ne pas modifier).")]
        [SerializeField] private int _hitboxLayer = 8;

        private int _hitboxCount;

        public int HitboxCount => _hitboxCount;
        public Collider[] HitboxColliders => _hitboxColliders;

        // ============================================================
        // API
        // ============================================================

        /// <summary>True si le collider appartient aux hitboxes de cette entité.</summary>
        public bool IsMyCollider(Collider col)
        {
            for (int i = 0; i < _hitboxCount; i++)
                if (_hitboxColliders[i] == col) return true;
            return false;
        }

        /// <summary>True si le collider a un M2922_DamageMultiplier.</summary>
        public bool HasDamageMultiplier(Collider col)
        {
            M2922_DamageMultiplier dm = col.GetComponent<M2922_DamageMultiplier>();
            return dm != null;
        }

        /// <summary>Multiplicateur de dégâts du collider (1.0 = normal).</summary>
        public float GetDamageMultiplier(Collider col)
        {
            M2922_DamageMultiplier dm = col.GetComponent<M2922_DamageMultiplier>();
            return dm != null ? dm.Multiplier : 1f;
        }

        // ============================================================
        // LIFECYCLE
        // ============================================================

        protected override void Start()
        {
            base.Start();
            _hitboxCount = _hitboxColliders != null ? _hitboxColliders.Length : 0;
            this.Log($"[HitboxSystem] {_hitboxCount} collider(s) enregistrés");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _hitboxCount = _hitboxColliders != null ? _hitboxColliders.Length : 0;

            // Résoudre le layer depuis le nom
            _hitboxLayer = UnityEngine.LayerMask.NameToLayer(_hitboxLayerName);
            if (_hitboxLayer < 0) _hitboxLayer = 8; // fallback

            // Auto-assigner le layer à tous les colliders
            if (_hitboxColliders != null)
            {
                for (int i = 0; i < _hitboxColliders.Length; i++)
                {
                    Collider col = _hitboxColliders[i];
                    if (col != null && col.gameObject.layer != _hitboxLayer)
                    {
                        col.gameObject.layer = _hitboxLayer;
                        UnityEditor.EditorUtility.SetDirty(col.gameObject);
                    }
                }
            }
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            DrawHitboxGizmos(0.15f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            DrawHitboxGizmos(0.5f);
        }

        private void DrawHitboxGizmos(float alpha)
        {
            if (_hitboxColliders == null) return;
            for (int i = 0; i < _hitboxCount; i++)
            {
                Collider col = _hitboxColliders[i];
                if (col == null) continue;

                float dmgMult = GetDamageMultiplier(col);
                bool hasMult = dmgMult != 1f;
                Gizmos.color = hasMult
                    ? new Color(1f, 0.2f, 0.2f, alpha)
                    : new Color(0.2f, 1f, 0.2f, alpha);

                if (col is BoxCollider box)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
                else if (col is CapsuleCollider capsule)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(capsule.center, capsule.radius);
                }
            }
        }

        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            int critCount = 0;
            if (_hitboxColliders != null)
                for (int i = 0; i < _hitboxCount; i++)
                    if (HasDamageMultiplier(_hitboxColliders[i])) critCount++;

            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Hitboxes", $"{_hitboxCount} ({critCount} crit)"),
            };
        }
#endif
    }
}
