using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Projectile tiré par une arme. Gère le déplacement, la durée de vie, et les dégâts.
    /// Utilisé avec un système de pooling : désactivé au lieu de détruit.
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Projectile")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Projectile : M2922_Base
    {
        [Header("=== PROJECTILE STATE ===")]
        private float _speed = 50f;
        private float _damage = 10f;
        private int _damageType = 0;
        private float _lifetime = 3f;
        private float _elapsed = 0f;
        private bool _active = false;
        private Vector3 _velocity = Vector3.zero;
        private VRCPlayerApi _owner;

        [Header("=== POOLING ===")]
        private M2922_WeaponFireHandler _pool;

        [Header("=== FILTRAGE ===")]
        [Tooltip("Nom du layer Unity pour les hitboxes.")]
        [SerializeField] private string _hitboxLayerName = "Hitbox";
        [Tooltip("Layer ID (auto-résolu, ne pas modifier).")]
        [SerializeField] public int _hitboxLayer = 8;

        // --- Propriétés ---
        public bool IsActive { get { return _active; } }
        public float Damage { get { return _damage; } }
        public VRCPlayerApi Owner { get { return _owner; } }

        // ===================================================
        // INIT (appelé par le pool)
        // ===================================================

        public void Launch(Vector3 position, Quaternion rotation, float speed, float damage,
            int damageType, float lifetime, VRCPlayerApi owner, M2922_WeaponFireHandler pool)
        {
            transform.SetPositionAndRotation(position, rotation);
            _speed = speed;
            _damage = damage;
            _damageType = damageType;
            _lifetime = lifetime;
            _elapsed = 0f;
            _active = true;
            _owner = owner;
            _pool = pool;
            _velocity = rotation * Vector3.forward * _speed;
            gameObject.SetActive(true);
        }

        public void ReturnToPool()
        {
            _active = false;
            _elapsed = 0f;
            gameObject.SetActive(false);
            if (_pool != null)
                _pool.ReturnProjectile(this);
        }

        // ===================================================
        // LIFECYCLE
        // ===================================================

        protected override void Update()
        {
            if (!_active) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                ReturnToPool();
                return;
            }

            // Déplacement
            Vector3 pos = transform.position + _velocity * Time.deltaTime;
            transform.position = pos;

            // TODO: Raycast / détection de collision
            // CheckCollision();
        }

        // ===================================================
        // COLLISION
        // ===================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!_active) return;

            // Filtrer : ne traiter que les colliders de la layer Hitbox
            if (other.gameObject.layer != _hitboxLayer) return;

            // Multiplicateur de zone (M2922_DamageMultiplier sur le collider)
            float zoneMult = 1f;
            var dmgMult = other.GetComponent<M2922_DamageMultiplier>();
            if (dmgMult != null)
                zoneMult = dmgMult.Multiplier;

            // Chercher un M2922_DamageReceiver sur la cible
            var receiver = other.GetComponent<M2922_DamageReceiver>();
            if (receiver == null)
                receiver = other.GetComponentInParent<M2922_DamageReceiver>();
            if (receiver != null)
            {
                receiver.ApplyTypedDamage(_damage * zoneMult, _damageType, _owner);
            }

            ReturnToPool();
        }

        // ===================================================
        // EDITOR
        // ===================================================

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Résoudre le layer hitbox depuis le nom
            _hitboxLayer = UnityEngine.LayerMask.NameToLayer(_hitboxLayerName);
            if (_hitboxLayer < 0) _hitboxLayer = 8;

            // S'assurer qu'il y a un Collider en trigger
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                col.isTrigger = true;
        }
#endif
    }
}
