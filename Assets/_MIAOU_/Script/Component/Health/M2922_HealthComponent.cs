using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Gère les points de vie : HP, max HP, régénération, mort.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HealthComponent : M2922_Base
    {
        [Header("=== HEALTH ===")]
        [SerializeField] private float _maxHP = 100f;
        [UdonSynced] private float _currentHP = 100f;

        [Header("=== REGEN ===")]
        [SerializeField] private bool _enableRegen = false;
        [SerializeField] private float _regenPerSecond = 1f;
        [SerializeField] private float _regenDelay = 3f;
        private float _lastDamageTime = -999f;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public float RegenPerSecond => _regenPerSecond;
        public float HPPercentage => _maxHP > 0f ? _currentHP / _maxHP : 0f;
        public bool IsDead => _currentHP <= 0f;

        protected override void Start()
        {
            base.Start();
            _currentHP = _maxHP;
        }

        protected override void Update()
        {
            base.Update();
            if (_enableRegen && !IsDead && Time.time - _lastDamageTime > _regenDelay)
            {
                _currentHP = Mathf.Min(_maxHP, _currentHP + _regenPerSecond * Time.deltaTime);
            }
        }

        public void TakeDamage(float amount)
        {
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            _lastDamageTime = Time.time;
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public void Heal(float amount)
        {
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public void Revive()
        {
            _currentHP = _maxHP;
            _lastDamageTime = -999f;
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            float pct = _maxHP > 0 ? _currentHP / _maxHP : 0;
            Color hpColor = pct > 0.66f ? Color.green : (pct > 0.33f ? Color.yellow : Color.red);
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("HP", $"{_currentHP:F0} / {_maxHP:F0}", hpColor),
                new M2922_GizmoDisplayInfo("Regen", _enableRegen ? $"{_regenPerSecond}/s" : "OFF"),
            };
        }
#endif
    }
}
