using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Bouclier absorbant les dégâts avant les HP, avec sa propre régénération.
    /// </summary>
    [AddComponentMenu("M2922/Health/Shield Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ShieldComponent : M2922_Base
    {
        [Header("=== SHIELD ===")]
        [SerializeField] private float _maxShield = 50f;
        [UdonSynced] private float _currentShield = 50f;

        [Header("=== REGEN ===")]
        [SerializeField] private float _regenPerSecond = 5f;
        [SerializeField] private float _regenDelay = 5f;
        private float _lastHitTime = -999f;
        /// <summary>Contrôle runtime de la regen (désactivée pendant la mort).</summary>
        private bool _regenEnabled = true;

        [Header("=== NETWORK AUTHORITY ===")]
        [SerializeField] private M2922_HitboxSystem _hitboxSystem;

        public float CurrentShield => _currentShield;
        public float MaxShield => _maxShield;
        public bool IsActive => _currentShield > 0f;

        protected override void Start()
        {
            base.Start();
            if (_hitboxSystem == null) _hitboxSystem = GetComponent<M2922_HitboxSystem>();
            _currentShield = _maxShield;
        }

        protected override void Update()
        {
            base.Update();
            if (_regenEnabled && Time.time - _lastHitTime > _regenDelay && _currentShield < _maxShield)
            {
                _currentShield = Mathf.Min(_maxShield, _currentShield + _regenPerSecond * Time.deltaTime);
                if (ShouldSync()) RequestSerialization();
            }
        }

        /// <summary>True si ce client est l'autorité réseau pour sync ce composant.</summary>
        private bool ShouldSync()
        {
            if (_hitboxSystem != null)
            {
                bool authority = _hitboxSystem.IsNetworkingAuthority();
                return authority;
            }
            return Networking.IsOwner(gameObject); // fallback
        }

        /// <summary>Absorbe des dégâts, retourne le surplus non absorbé.</summary>
        public float AbsorbDamage(float amount)
        {
            _lastHitTime = Time.time;
            if (_currentShield <= 0f) return amount;

            float absorbed = Mathf.Min(_currentShield, amount);
            _currentShield -= absorbed;
            if (ShouldSync())
            {
                RequestSerialization();
                if (DEBUG) this.Log($"[Shield] AbsorbDamage({amount:F1}) → Shield={_currentShield:F1}, serialization demandée");
            }
            else if (DEBUG)
                this.Log($"[Shield] AbsorbDamage({amount:F1}) → Shield={_currentShield:F1}, PAS de serialization");
            return amount - absorbed;
        }

        /// <summary>Restaure le bouclier au maximum (respawn).</summary>
        /// <summary>Active/désactive la régénération runtime (utilisé par DeathHandler).</summary>
        public void SetRegenEnabled(bool enabled)
        {
            _regenEnabled = enabled;
        }

        /// <summary>True si la regen shield est active en runtime.</summary>
        public bool IsRegenActive => _regenEnabled;

        public void Revive()
        {
            _currentShield = _maxShield;
            _lastHitTime = -999f;
            if (ShouldSync()) RequestSerialization();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            float pct = _maxShield > 0 ? _currentShield / _maxShield : 0;
            Color color = pct > 0.5f ? Color.cyan : (pct > 0 ? Color.yellow : Color.red);
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Shield", $"{_currentShield:F0} / {_maxShield:F0}", color),
                new M2922_GizmoDisplayInfo("Regen", $"{_regenPerSecond}/s"),
            };
        }
#endif
    }
}
