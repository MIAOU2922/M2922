using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Projectile
{
    /// <summary>
    /// Logique projectile/balle : vitesse, durée de vie, mouvement.
    /// </summary>
    [AddComponentMenu("M2922/Projectile/Projectile Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ProjectileComponent : M2922_Base
    {
        [Header("=== PROJECTILE ===")]
        [SerializeField] private float _speed = 50f;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private bool _useGravity = false;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_DamageSource _damageSource;
        [SerializeField] private M2922_HitDetector _hitDetector;

        private float _spawnTime = 0f;
        private Vector3 _velocity;

        protected override void Start()
        {
            base.Start();
            _spawnTime = Time.time;
            _velocity = transform.forward * _speed;
        }

        protected override void AutoDetectReferences()
        {
            if (_damageSource == null) _damageSource = GetComponent<M2922_DamageSource>();
            if (_hitDetector == null) _hitDetector = GetComponent<M2922_HitDetector>();
        }

        protected override void Update()
        {
            base.Update();

            // Durée de vie
            if (Time.time - _spawnTime > _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Mouvement
            if (_useGravity)
                _velocity += UnityEngine.Physics.gravity * Time.deltaTime;

            transform.position += _velocity * Time.deltaTime;
        }

        public void Launch(Vector3 direction, float speedOverride = -1f)
        {
            float speed = speedOverride > 0f ? speedOverride : _speed;
            _velocity = direction.normalized * speed;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Speed", $"{_speed:F1} u/s"),
                new M2922_GizmoDisplayInfo("Lifetime", $"{_lifetime}s"),
                new M2922_GizmoDisplayInfo("Gravity", _useGravity ? "ON" : "OFF"),
            };
        }
#endif
    }
}
