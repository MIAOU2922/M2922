using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Gère les points de vie : HP, max HP, régénération, mort.
    /// </summary>
    [AddComponentMenu("M2922/Health/Health Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HealthComponent : M2922_Base
    {
        [Header("=== HEALTH ===")]
        [SerializeField] private float _maxHP = 100f;
        [UdonSynced] private float _currentHP = 100f;

        [Header("=== REGEN ===")]
        [Tooltip("Activer la régénération (config inspector).")]
        [SerializeField] private bool _enableRegen = false;
        [SerializeField] private float _regenPerSecond = 1f;
        [SerializeField] private float _regenDelay = 3f;
        private float _lastDamageTime = -999f;
        /// <summary>Contrôle runtime de la regen (désactivée pendant la mort).</summary>
        private bool _regenEnabled = true;

        [Header("=== NETWORK AUTHORITY ===")]
        [SerializeField] private M2922_HitboxSystem _hitboxSystem;

        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public float RegenPerSecond => _regenPerSecond;
        public float HPPercentage => _maxHP > 0f ? _currentHP / _maxHP : 0f;
        public bool IsDead => _currentHP <= 0f;

        protected override void Start()
        {
            base.Start();
            if (_hitboxSystem == null) _hitboxSystem = GetComponent<M2922_HitboxSystem>();
            _currentHP = _maxHP;
        }

        protected override void Update()
        {
            base.Update();
            if (_enableRegen && _regenEnabled && !IsDead && Time.time - _lastDamageTime > _regenDelay)
            {
                _currentHP = Mathf.Min(_maxHP, _currentHP + _regenPerSecond * Time.deltaTime);
            }
        }

        /// <summary>True si ce client est l'autorité réseau pour sync ce composant.</summary>
        private bool ShouldSync()
        {
            if (_hitboxSystem != null)
            {
                bool authority = _hitboxSystem.IsNetworkingAuthority();
                if (VERBOSE_DEBUG)
                    this.Log($"[Health] ShouldSync via HitboxSystem: authority={authority}, boundId={_hitboxSystem.BoundPlayerId}, isOwner={Networking.IsOwner(gameObject)}");
                return authority;
            }
            bool fallback = Networking.IsOwner(gameObject);
            if (VERBOSE_DEBUG)
                this.Log($"[Health] ShouldSync fallback (no HitboxSystem): isOwner={fallback}");
            return fallback;
        }

        public void TakeDamage(float amount)
        {
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            _lastDamageTime = Time.time;
            bool sync = ShouldSync();
            if (sync)
            {
                RequestSerialization();
                if (DEBUG) this.Log($"[Health] TakeDamage({amount:F1}) → HP={_currentHP:F1}, serialization demandée");
            }
            else if (DEBUG)
                this.Log($"[Health] TakeDamage({amount:F1}) → HP={_currentHP:F1}, PAS de serialization (pas autorité)");
        }

        public void Heal(float amount)
        {
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            if (ShouldSync()) RequestSerialization();
        }

        public void Revive()
        {
            _currentHP = _maxHP;
            _lastDamageTime = -999f;
            if (ShouldSync()) RequestSerialization();
        }

        /// <summary>Active/désactive la régénération runtime (utilisé par DeathHandler).</summary>
        public void SetRegenEnabled(bool enabled)
        {
            _regenEnabled = enabled;
        }

        /// <summary>True si la regen est configurée ET active en runtime.</summary>
        public bool IsRegenActive => _enableRegen && _regenEnabled;

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
