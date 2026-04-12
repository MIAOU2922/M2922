using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    public class M2922_HealthSystem : M2922_System
    {
        // === IDamageable (inline) ===
        [Header("=== HEALTH SETTINGS ===")]
        [SerializeField] private float _baseMaxHealth = 100f;
        [SerializeField] private bool _canTakeDamage = true;

        private float _maxHealth;
        private float _health;

        public float Health         => _health;
        public float MaxHealth      => _maxHealth;
        public bool IsAlive         => _health > 0f;
        public bool CanTakeDamage   => _canTakeDamage && IsAlive;

        public void SetMaxHealth(float newMax)
        {
            float ratio = _maxHealth > 0f ? _health / _maxHealth : 1f;
            _maxHealth = Mathf.Max(1f, newMax);
            _health = _maxHealth * ratio;
            this.VerboseLog($"SetMaxHealth: {_maxHealth} | HP: {_health}");
        }

        public void ResetMaxHealth() => SetMaxHealth(_baseMaxHealth);

        public void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            if (!CanTakeDamage) return;
            _health = Mathf.Max(0f, _health - damage);
            this.VerboseLog($"TakeDamage: -{damage} ({damageType}) | HP: {_health}/{_maxHealth}");
            if (_health <= 0f) Die(attackerId);
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _health = Mathf.Min(_maxHealth, _health + amount);
            this.VerboseLog($"Heal: +{amount} | HP: {_health}/{_maxHealth}");
        }

        public void Die(int killerId)
        {
            _health = 0f;
            _canTakeDamage = false;
            this.Log($"Died. KillerID: {killerId}");
            // TODO: EventBus.Publish(EventType.PlayerDied, killerId)
        }

        // === METHODE ===
        protected override void Awake()
        {
            _maxHealth = _baseMaxHealth;
            _health = _maxHealth;
        }

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
            // Offset slot 6x — au-dessus du label Entity (3x)
            string status = IsAlive ? $"HP: {_health:F0}/{_maxHealth:F0}" : "HP: DEAD";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 6f),
                $"[Health] {status} | CanDmg: {_canTakeDamage}"
            );
        }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
        }
#endif
    }
}