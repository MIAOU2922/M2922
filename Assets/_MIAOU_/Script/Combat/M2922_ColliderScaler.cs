using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    /// <summary>
    /// Aligne et scale un collider entre deux références de transform (os A → os B).
    ///
    /// ─── RÔLE ────────────────────────────────────────────────────────────────
    ///   Positionné au milieu de A et B par un ParentConstraint (2 sources, poids 0.5),
    ///   ce script gère en LateUpdate :
    ///     • Rotation  — aligne l'axe du collider dans la direction monde A → B.
    ///     • Longueur  — égale à distance(A, B), convertie en espace local.
    ///     • Rayon     — _baseRadius × facteur de scale de l'avatar (lossyScale de A).
    ///
    /// ─── SETUP SCÈNE ─────────────────────────────────────────────────────────
    ///   Sur un seul GameObject :
    ///     - M2922_ColliderScaler  (ce script)
    ///     - ParentConstraint      (2 sources = os A + os B, poids 0.5 chacun,
    ///                              Position On, Rotation Off recommandé)
    ///     - CapsuleCollider / BoxCollider  (le hitbox)
    ///
    ///   1. Assigner _refA (articulation de départ) et _refB (articulation d'arrivée).
    ///   2. Assigner le collider (_capsuleCollider ou _boxCollider).
    ///   3. Régler _baseRadius : rayon souhaité à l'échelle 1× de l'avatar.
    ///   4. Régler _referenceScale : lossyScale de _refA quand l'avatar est à l'échelle 1×.
    ///      Utiliser le bouton "Capturer" dans l'éditeur pour le remplir automatiquement.
    ///   5. Sur le ParentConstraint, décocher "Rotation" pour laisser ce script
    ///      gérer l'orientation (évite les conflits).
    ///
    /// ─── EXEMPLE ─────────────────────────────────────────────────────────────
    ///   _refA = Shoulder, _refB = Elbow, CapsuleCollider direction=1 (Y)
    ///   → La capsule couvre exactement l'os de l'épaule au coude, à toute taille d'avatar.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_ColliderScaler : M2922_System
    {
        // =====================================================================
        // RÉFÉRENCES DE POINTS A → B
        // =====================================================================
        [Header("=== RÉFÉRENCES (A → B) ===")]
        [Tooltip("Articulation de départ (ex: Shoulder, Hip, Knee…).\n" +
                 "Doit aussi être l'une des sources du ParentConstraint.")]
        [SerializeField] private Transform _refA;

        [Tooltip("Articulation d'arrivée (ex: Elbow, Knee, Ankle…).\n" +
                 "Doit aussi être l'une des sources du ParentConstraint.")]
        [SerializeField] private Transform _refB;

        // =====================================================================
        // COLLIDER (un seul à la fois)
        // =====================================================================
        [Header("=== COLLIDER (un seul) ===")]
        [Tooltip("CapsuleCollider à aligner. Laisser vide si autre type.")]
        [SerializeField] private CapsuleCollider _capsuleCollider;

        [Tooltip("BoxCollider à aligner. Laisser vide si autre type.\n" +
                 "La longueur de l'axe X est utilisée pour l'alignement.")]
        [SerializeField] private BoxCollider _boxCollider;

        // =====================================================================
        // RAYON / ÉPAISSEUR DE BASE
        // =====================================================================
        [Header("=== RAYON BASE ===")]
        [Tooltip("Rayon de la capsule (ou demi-largeur Y/Z du box) à l'échelle 1× de l'avatar.\n" +
                 "Scalé automatiquement selon le lossyScale de _refA.")]
        [SerializeField] private float _baseRadius = 0.05f;

        // =====================================================================
        // SCALE REFERENCE
        // =====================================================================
        [Header("=== SCALE REFERENCE ===")]
        [Tooltip("lossyScale de _refA quand l'avatar est à l'échelle 1×.\n" +
                 "Utiliser le bouton 'Capturer' dans l'éditeur pour le remplir.")]
        [SerializeField] private Vector3 _referenceScale = Vector3.one;

        // =====================================================================
        // RUNTIME — état interne
        // =====================================================================
        private Vector3  _lastDir      = Vector3.up;
        private float    _lastDist     = 0f;
        private float    _lastRadFact  = 1f;

        // Exposé pour l'éditeur
        public Vector3 LastDir     => _lastDir;
        public float   LastDist    => _lastDist;
        public float   LastRadFact => _lastRadFact;

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        protected override void Awake()
        {
            TryAutoFind();
        }

        protected override void Start()
        {
            base.Start();
            Apply();
        }

        // LateUpdate : s'exécute après le ParentConstraint (phase Animation → avant LateUpdate)
        private void LateUpdate()
        {
            Apply();
        }

        // =====================================================================
        // AUTO-FIND
        // =====================================================================

        private void TryAutoFind()
        {
            if (_capsuleCollider == null && _boxCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider>();
                if (_capsuleCollider == null)
                    _boxCollider = GetComponent<BoxCollider>();
            }
        }

        // =====================================================================
        // LOGIQUE PRINCIPALE
        // =====================================================================

        private void Apply()
        {
            if (_refA == null || _refB == null) return;

            // ── Direction et distance monde ──────────────────────────────────
            Vector3 worldVec = _refB.position - _refA.position;
            float   worldDist = worldVec.magnitude;
            if (worldDist < 0.0001f) return;

            Vector3 dir = worldVec / worldDist;
            _lastDir  = dir;
            _lastDist = worldDist;

            // ── Rotation : aligner l'axe du collider sur dir ─────────────────
            //   Quaternion.FromToRotation(axeLocal, dir) donne la rotation monde
            //   qui place axeLocal dans la direction dir.
            Vector3 localAxis = GetColliderAxis();
            transform.rotation = Quaternion.FromToRotation(localAxis, dir);

            // ── Facteur de scale (via lossyScale de refA) ────────────────────
            //   On utilise l'axe X du lossyScale comme scale uniforme de l'avatar.
            float refScaleX  = _referenceScale.x > 0f ? _referenceScale.x : 1f;
            float avatarScale = _refA.lossyScale.x;
            float radFactor  = avatarScale / refScaleX;
            _lastRadFact = radFactor;

            // ── Conversion distance monde → locale ───────────────────────────
            //   Après avoir posé la rotation, la lossyScale le long de l'axe
            //   du collider = avatarScale (scale uniforme supposé).
            //   localLength = worldDist / avatarScale
            float localLength = avatarScale > 0f ? worldDist / avatarScale : worldDist;

            // ── Application sur la CapsuleCollider ───────────────────────────
            if (_capsuleCollider != null)
            {
                _capsuleCollider.height = localLength;
                _capsuleCollider.radius = _baseRadius * radFactor;
                _capsuleCollider.center = Vector3.zero;
            }

            // ── Application sur la BoxCollider ───────────────────────────────
            //   La longueur va sur l'axe local X (LookRotation pointe X vers dir).
            //   Y et Z = rayon × 2.
            if (_boxCollider != null)
            {
                float side = _baseRadius * 2f * radFactor;
                _boxCollider.size   = new Vector3(localLength, side, side);
                _boxCollider.center = Vector3.zero;
            }
        }

        /// <summary>
        /// Retourne l'axe local du collider qui doit pointer de A vers B.
        /// Capsule : direction 0=X, 1=Y, 2=Z.
        /// Box : on utilise X par convention.
        /// </summary>
        private Vector3 GetColliderAxis()
        {
            if (_capsuleCollider != null)
            {
                int d = _capsuleCollider.direction;
                return d == 0 ? Vector3.right
                     : d == 2 ? Vector3.forward
                     : Vector3.up;
            }
            return Vector3.right; // Box → X
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        protected override void OnValidate()
        {
            base.OnValidate();
            TryAutoFind();
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo || _refA == null || _refB == null) return;
            DrawAlignGizmos(0.15f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
            if (_refA != null && _refB != null) DrawAlignGizmos(0.6f);
            DrawInfoLabel();
        }

        private void DrawAlignGizmos(float alpha)
        {
            // Ligne A → B
            Gizmos.color = new Color(0.2f, 0.9f, 1f, alpha);
            Gizmos.DrawLine(_refA.position, _refB.position);

            // Sphères aux extrémités
            float r = _baseRadius * (_referenceScale.x > 0f ? _refA.lossyScale.x / _referenceScale.x : 1f);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, alpha);
            Gizmos.DrawWireSphere(_refA.position, r * 0.4f);
            Gizmos.DrawWireSphere(_refB.position, r * 0.4f);

            // Point milieu
            Gizmos.color = new Color(1f, 1f, 0f, alpha);
            Gizmos.DrawWireSphere((_refA.position + _refB.position) * 0.5f, r * 0.25f);
        }

        private void DrawInfoLabel()
        {
            string info;
            if (_refA == null || _refB == null)
                info = "[Scaler] ⚠ refA ou refB manquant";
            else
            {
                float dist = Vector3.Distance(_refA.position, _refB.position);
                info = $"[Scaler] dist:{dist:F3}m | r:{_baseRadius:F3} | fact:{_lastRadFact:F2}×";
            }
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 6f),
                info
            );
        }

#endif
    }
}
