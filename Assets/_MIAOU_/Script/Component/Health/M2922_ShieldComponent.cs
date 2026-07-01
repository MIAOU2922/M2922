using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Bouclier absorbant les dégâts avant les HP, avec sa propre régénération.
    /// </summary>
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

        public float CurrentShield => _currentShield;
        public float MaxShield => _maxShield;
        public bool IsActive => _currentShield > 0f;

        protected override void Start()
        {
            base.Start();
            _currentShield = _maxShield;
        }

        protected override void Update()
        {
            base.Update();
            if (Time.time - _lastHitTime > _regenDelay && _currentShield < _maxShield)
            {
                _currentShield = Mathf.Min(_maxShield, _currentShield + _regenPerSecond * Time.deltaTime);
                if (Networking.IsOwner(gameObject)) RequestSerialization();
            }
        }

        /// <summary>Absorbe des dégâts, retourne le surplus non absorbé.</summary>
        public float AbsorbDamage(float amount)
        {
            _lastHitTime = Time.time;
            if (_currentShield <= 0f) return amount;

            float absorbed = Mathf.Min(_currentShield, amount);
            _currentShield -= absorbed;
            if (Networking.IsOwner(gameObject)) RequestSerialization();
            return amount - absorbed;
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
