using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Projectile
{
    public enum DetectionMode { Raycast, Collision }

    /// <summary>
    /// Détection de collision d'un projectile : raycast ou trigger.
    /// </summary>
    [AddComponentMenu("M2922/Projectile/Hit Detector")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HitDetector : M2922_Base
    {
        [Header("=== DETECTION ===")]
        [SerializeField] private DetectionMode _mode = DetectionMode.Raycast;
        [SerializeField] private LayerMask _hitLayers = ~0;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_DamageSource _damageSource;

        private Vector3 _lastPosition;

        protected override void Start()
        {
            base.Start();
            _lastPosition = transform.position;
        }

        protected override void AutoDetectReferences()
        {
            if (_damageSource == null) _damageSource = GetComponent<M2922_DamageSource>();
        }

        protected override void Update()
        {
            base.Update();

            if (_mode == DetectionMode.Raycast)
            {
                Vector3 direction = transform.position - _lastPosition;
                float distance = direction.magnitude;

                if (distance > 0.001f)
                {
                    RaycastHit hit;
                    if (UnityEngine.Physics.Raycast(_lastPosition, direction.normalized, out hit, distance, _hitLayers))
                    {
                        OnHit(hit.collider, hit.point, hit.normal);
                    }
                }
            }

            _lastPosition = transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_mode == DetectionMode.Collision)
                OnHit(other, transform.position, Vector3.up);
        }

        private void OnHit(Collider col, Vector3 point, Vector3 normal)
        {
            this.Log($"Hit: {col.name} at {point}");

            // Cherche le HitboxSystem en remontant la hiérarchie
            M2922_HitboxSystem hitboxSys = col.GetComponentInParent<M2922_HitboxSystem>();
            if (hitboxSys == null)
            {
                // Fallback : cherche directement un DamageReceiver
                M2922_DamageReceiver directReceiver = col.GetComponent<M2922_DamageReceiver>();
                if (directReceiver != null && _damageSource != null)
                {
                    directReceiver.ApplyDamage(_damageSource.GetDamage(), _damageSource.Owner, false);
                    Destroy(gameObject);
                }
                return;
            }

            // Vérifie que le collider touché appartient bien à cette entité
            if (!hitboxSys.IsMyCollider(col)) return;

            // Récupère le DamageReceiver sur le même GameObject que le HitboxSystem
            M2922_DamageReceiver receiver = hitboxSys.GetComponent<M2922_DamageReceiver>();
            if (receiver != null && _damageSource != null)
            {
                float dmgMult = hitboxSys.GetDamageMultiplier(col);
                float damage = _damageSource.GetDamage() * dmgMult;
                bool isCrit = dmgMult > 1f;

                receiver.ApplyDamage(damage, _damageSource.Owner, isCrit);
            }

            Destroy(gameObject);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Mode", _mode.ToString()),
                new M2922_GizmoDisplayInfo("Damage", _damageSource != null ? $"{_damageSource.GetDamage():F1}" : "None", Color.red),
            };
        }
#endif
    }
}
