using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Gestion de la dispersion du tir (spread / précision).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_SpreadComponent : M2922_Base
    {
        [Header("=== SPREAD ===")]
        [SerializeField] private float _baseSpread = 0.5f;
        [SerializeField] private float _maxSpread = 5f;
        [SerializeField] private float _spreadPerShot = 0.3f;
        [SerializeField] private float _spreadRecoveryPerSecond = 2f;

        private float _currentSpread = 0f;

        public float CurrentSpread => _baseSpread + _currentSpread;

        protected override void Update()
        {
            base.Update();
            // Récupération de la dispersion
            _currentSpread = Mathf.Max(0f, _currentSpread - _spreadRecoveryPerSecond * Time.deltaTime);
        }

        /// <summary>Ajoute de la dispersion après un tir.</summary>
        public void AddSpread()
        {
            _currentSpread = Mathf.Min(_maxSpread, _currentSpread + _spreadPerShot);
        }

        /// <summary>Retourne une direction avec dispersion appliquée.</summary>
        public Vector3 ApplySpread(Vector3 direction)
        {
            float spread = CurrentSpread;
            Vector3 randomOffset = Random.insideUnitSphere * spread * 0.01f;
            return (direction + randomOffset).normalized;
        }

        public void ResetSpread()
        {
            _currentSpread = 0f;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Base", $"{_baseSpread:F2}°"),
                new M2922_GizmoDisplayInfo("Max", $"{_maxSpread:F2}°"),
                new M2922_GizmoDisplayInfo("Current", $"{CurrentSpread:F2}°"),
            };
        }
#endif
    }
}
