using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>Type de collider supporté par le Collider Scaler.</summary>
    public enum ColliderScalerType
    {
        Box,
        Capsule
    }

    /// <summary>
    /// Scale et positionne dynamiquement un collider (Box ou Capsule) entre deux points A et B en runtime.
    /// Le collider est placé au milieu des deux points, orienté dans leur direction,
    /// et sa longueur correspond à la distance qui les sépare.
    ///
    /// IMPORTANT : Ajouter manuellement un BoxCollider ou CapsuleCollider sur ce GameObject.
    /// Le type de collider doit correspondre à la propriété "Collider Type".
    /// </summary>
    [AddComponentMenu("M2922/Utils/Collider Scaler")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class M2922_ColliderScaler : M2922_Base
    {

        [Header("=== POINTS ===")]
        [Tooltip("Transform du point A.")]
        public Transform pointA;

        [Tooltip("Offset local appliqué au point A (relatif à la rotation de pointA).")]
        public Vector3 offsetA = Vector3.zero;

        [Tooltip("Transform du point B.")]
        public Transform pointB;

        [Tooltip("Offset local appliqué au point B (relatif à la rotation de pointB).")]
        public Vector3 offsetB = Vector3.zero;

        [Header("=== COLLIDER ===")]
        [Tooltip("Type de collider à utiliser. Doit correspondre au collider présent sur ce GameObject.")]
        public ColliderScalerType colliderType = ColliderScalerType.Box;

        [Tooltip("Épaisseur / rayon du collider (X et Z pour Box, Radius pour Capsule).")]
        public float thickness = 0.1f;

        [Header("=== OFFSET ===")]
        [Tooltip("Offset local supplémentaire appliqué au collider.")]
        public Vector3 localOffset = Vector3.zero;

        [Header("=== UPDATE MODE ===")]
        [Tooltip("Si coché, met à jour en continu dans LateUpdate(). Décocher pour appel manuel.")]
        public bool autoUpdate = true;

        // Privé
        private BoxCollider _boxCollider;
        private CapsuleCollider _capsuleCollider;
        private Vector3 _lastPosA;
        private Vector3 _lastPosB;
        private float _lastThickness;

        protected override void Start()
        {
            base.Start();

            // Récupérer les colliders existants (ajoutés manuellement dans l'éditeur)
            _boxCollider = GetComponent<BoxCollider>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
        }

        public override void PostLateUpdate()
        {
            base.PostLateUpdate();
            if (!autoUpdate) return;

            UpdateCollider();
        }

        /// <summary>
        /// Recalcule la position, rotation et échelle du collider pour relier les deux points.
        /// À appeler manuellement si autoUpdate = false.
        /// </summary>
        public void UpdateCollider()
        {
            if (pointA == null || pointB == null) return;

            Vector3 posA = pointA.position + pointA.TransformDirection(offsetA);
            Vector3 posB = pointB.position + pointB.TransformDirection(offsetB);

            // Vérifier si on a besoin de recalculer
            if (posA == _lastPosA && posB == _lastPosB && Mathf.Approximately(thickness, _lastThickness))
                return;

            _lastPosA = posA;
            _lastPosB = posB;
            _lastThickness = thickness;

            // Milieu et direction
            Vector3 midPoint = (posA + posB) * 0.5f;
            float distance = Vector3.Distance(posA, posB);
            Vector3 direction = (posB - posA).normalized;

            // Si les deux points sont confondus, éviter les divisions par zéro
            if (distance < 0.0001f) return;

            // Appliquer offset local
            if (localOffset != Vector3.zero)
                midPoint += transform.TransformDirection(localOffset);

            // Position
            transform.position = midPoint;

            // Rotation : orienter l'axe forward (Z local) vers la direction B - A
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            // Appliquer au collider
            if (colliderType == ColliderScalerType.Box && _boxCollider != null)
            {
                _boxCollider.center = Vector3.zero;
                _boxCollider.size = new Vector3(thickness, thickness, distance);
            }
            else if (colliderType == ColliderScalerType.Capsule && _capsuleCollider != null)
            {
                _capsuleCollider.center = Vector3.zero;
                _capsuleCollider.radius = thickness * 0.5f;
                _capsuleCollider.height = distance;
                _capsuleCollider.direction = 2; // axe Z = direction forward
            }
        }

        /// <summary>
        /// Définit les points manuellement par Transform.
        /// </summary>
        public void SetPoints(Transform a, Transform b)
        {
            pointA = a;
            pointB = b;
            UpdateCollider();
        }

        /// <summary>
        /// Définit les points manuellement par positions monde.
        /// </summary>
        public void SetPointsWorld(Vector3 posA, Vector3 posB)
        {
            Vector3 midPoint = (posA + posB) * 0.5f;
            float distance = Vector3.Distance(posA, posB);
            Vector3 direction = (posB - posA).normalized;

            if (distance < 0.0001f) return;

            transform.position = midPoint;
            transform.rotation = Quaternion.LookRotation(direction);

            if (colliderType == ColliderScalerType.Box && _boxCollider != null)
            {
                _boxCollider.center = Vector3.zero;
                _boxCollider.size = new Vector3(thickness, thickness, distance);
            }
            else if (colliderType == ColliderScalerType.Capsule && _capsuleCollider != null)
            {
                _capsuleCollider.center = Vector3.zero;
                _capsuleCollider.radius = thickness * 0.5f;
                _capsuleCollider.height = distance;
                _capsuleCollider.direction = 2;
            }

            _lastPosA = Vector3.negativeInfinity;
            _lastPosB = Vector3.negativeInfinity;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        /// <summary>
        /// [Editor] Gère automatiquement l'ajout/suppression du bon type de collider.
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            _boxCollider = GetComponent<BoxCollider>();
            _capsuleCollider = GetComponent<CapsuleCollider>();

            // Supprimer le type non voulu
            if (colliderType == ColliderScalerType.Box && _capsuleCollider != null)
            {
                DestroyImmediate(_capsuleCollider);
                _capsuleCollider = null;
            }
            else if (colliderType == ColliderScalerType.Capsule && _boxCollider != null)
            {
                DestroyImmediate(_boxCollider);
                _boxCollider = null;
            }

            // Ajouter si absent
            if (colliderType == ColliderScalerType.Box && _boxCollider == null)
                _boxCollider = gameObject.AddComponent<BoxCollider>();
            else if (colliderType == ColliderScalerType.Capsule && _capsuleCollider == null)
                _capsuleCollider = gameObject.AddComponent<CapsuleCollider>();

            // Forcer le recalcul
            _lastPosA = Vector3.negativeInfinity;
            _lastPosB = Vector3.negativeInfinity;
            _lastThickness = -1f;

            UpdateCollider();
        }

        /// <summary>
        /// [Editor] Dessine une ligne entre les deux points et met à jour le collider en live.
        /// </summary>
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            // Mise à jour live quand on déplace les points dans la scène
            if (!Application.isPlaying)
            {
                if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider>();
                if (_capsuleCollider == null) _capsuleCollider = GetComponent<CapsuleCollider>();
                UpdateCollider();
            }

            if (pointA == null || pointB == null) return;

            Vector3 rawPosA = pointA.position;
            Vector3 rawPosB = pointB.position;
            Vector3 posA = rawPosA + pointA.TransformDirection(offsetA);
            Vector3 posB = rawPosB + pointB.TransformDirection(offsetB);
            Vector3 mid = (posA + posB) * 0.5f;

            // Ligne pointillée entre les transforms bruts et les points offsettés
            Gizmos.color = Color.gray;
            if (offsetA != Vector3.zero) Gizmos.DrawLine(rawPosA, posA);
            if (offsetB != Vector3.zero) Gizmos.DrawLine(rawPosB, posB);

            // Ligne entre A et B (points offsettés)
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(posA, posB);

            // Sphères aux extrémités
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(posA, thickness * 0.5f);
            Gizmos.DrawWireSphere(posB, thickness * 0.5f);

            // Sphère au milieu
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(mid, thickness * 0.25f);

            // Direction forward (Z)
            Vector3 dir = (posB - posA).normalized;
            if (dir != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(mid, dir * 0.3f);
            }
        }
#endif
    }
}
