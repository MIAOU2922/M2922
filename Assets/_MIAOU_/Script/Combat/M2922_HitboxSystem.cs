using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Combat;

namespace M2922.Combat
{
    /// <summary>
    /// Système de hitboxes d'une entité (joueur, prop, véhicule).
    ///
    /// ─── RÔLE ────────────────────────────────────────────────────────────────
    ///   Centralise la liste des Collider qui représentent le corps de l'entité.
    ///   Permet au projectile d'identifier à qui appartient un collider touché,
    ///   et de savoir si ce collider est une zone critique.
    ///
    /// ─── SETUP SCÈNE ─────────────────────────────────────────────────────────
    ///   1. Attacher ce composant sur le même GO que M2922_PlayerController.
    ///   2. Créer des GO enfants (Head, Body, Legs…) avec des Collider (Is Trigger).
    ///   3. Optionnel : ajouter M2922_CritZone sur les colliders critiques (ex : tête).
    ///   4. Assigner les colliders dans _hitboxColliders, ou utiliser "Auto-Discover"
    ///      dans l'Inspector pour les trouver automatiquement.
    ///
    /// ─── UTILISATION (Projectile) ────────────────────────────────────────────
    ///   M2922_HitboxSystem hitboxSys = col.GetComponentInParent<M2922_HitboxSystem>();
    ///   if (hitboxSys != null && hitboxSys.IsMyCollider(col))
    ///   {
    ///       bool isCrit = hitboxSys.IsCritCollider(col);
    ///       hitboxSys.Owner.TakeDamage(damage, attackerId, damageType);
    ///   }
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_HitboxSystem : M2922_System
    {
        // =====================================================================
        // HITBOX CONFIG
        // =====================================================================
        [Header("=== HITBOX CONFIG ===")]
        [Tooltip("Colliders qui représentent le corps de cette entité.\n" +
                 "Assigner manuellement ou utiliser le bouton Auto-Discover dans l'Inspector.\n" +
                 "Chaque collider doit être un enfant de ce GameObject.")]
        [SerializeField] private Collider[] _hitboxColliders = new Collider[0];

        // =====================================================================
        // RUNTIME
        // =====================================================================
        private int _hitboxCount;

        public int        HitboxCount      => _hitboxCount;
        public Collider[] HitboxColliders  => _hitboxColliders;

        // =====================================================================
        // API
        // =====================================================================

        /// <summary>
        /// Retourne true si <paramref name="col"/> appartient aux hitboxes de cette entité.
        /// </summary>
        public bool IsMyCollider(Collider col)
        {
            for (int i = 0; i < _hitboxCount; i++)
                if (_hitboxColliders[i] == col) return true;
            return false;
        }

        /// <summary>
        /// Retourne true si <paramref name="col"/> est une zone critique
        /// (a un composant M2922_CritZone sur son GameObject).
        /// Stocke le résultat avant de comparer — null-check inline sur UdonSharpBehaviour crash en Udon.
        /// </summary>
        public bool IsCritCollider(Collider col)
        {
            M2922_CritZone _cz = col.GetComponent<M2922_CritZone>();
            return _cz != null ? true : false;
        }

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        protected override void Awake()
        {
            // Awake n'est pas dispatché fiablement dans la chaîne virtuelle UdonSharp.
            // _hitboxCount initialisé dans Start() à la place.
        }

        protected override void Start()
        {
            base.Start();
            _hitboxCount = _hitboxColliders != null ? _hitboxColliders.Length : 0;
            this.VerboseLog($"[HitboxSystem] {_hitboxCount} collider(s) enregistrés");
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        protected override void OnValidate()
        {
            base.OnValidate();
            _hitboxCount = _hitboxColliders != null ? _hitboxColliders.Length : 0;
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;
            DrawHitboxGizmos(0.15f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
            DrawHitboxGizmos(0.5f);
        }

        private void DrawHitboxGizmos(float alpha)
        {
            if (_hitboxColliders == null) return;

            int critCount   = 0;
            int normalCount = 0;

            foreach (var col in _hitboxColliders)
            {
                if (col == null) continue;

                bool isCrit = col.GetComponent<M2922_CritZone>() != null;
                if (isCrit) critCount++; else normalCount++;

                // Rouge pour zone critique, vert pour hitbox normale
                Gizmos.color = isCrit
                    ? new Color(1f, 0.2f, 0.2f, alpha)
                    : new Color(0.2f, 0.9f, 0.2f, alpha);

                Bounds b = col.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 9f),
                $"[Hitbox] {normalCount} normal | {critCount} crit"
            );
        }

        /// <summary>
        /// Remplace le tableau actuel par tous les Collider enfants trouvés (incluant inactifs).
        /// Appelé via le bouton "Auto-Discover" dans M2922_HitboxSystemEditor.
        /// </summary>
        public void AutoDiscover()
        {
            _hitboxColliders = GetComponentsInChildren<Collider>(true);
            _hitboxCount     = _hitboxColliders.Length;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"[M2922_HitboxSystem] Auto-Discover: {_hitboxCount} collider(s) trouvé(s) sur {gameObject.name}");
        }

#endif
    }
}
