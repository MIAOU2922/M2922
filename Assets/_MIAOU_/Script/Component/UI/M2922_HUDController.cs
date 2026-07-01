using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;
using M2922.Component.Ammo;
using M2922.Component.Player;

namespace M2922.Component.UI
{
    /// <summary>
    /// Contrôleur HUD : vie, munitions, hitmarker, etc.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HUDController : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_HealthComponent _health;
        [SerializeField] private M2922_MagazineComponent _magazine;
        [SerializeField] private M2922_PlayerStats _stats;

        [Header("=== HUD ELEMENTS ===")]
        [SerializeField] private UnityEngine.UI.Text _hpText;
        [SerializeField] private UnityEngine.UI.Text _ammoText;
        [SerializeField] private UnityEngine.UI.Text _scoreText;
        [SerializeField] private UnityEngine.UI.Image _hitmarker;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_health == null) _health = GetComponent<M2922_HealthComponent>();
            if (_magazine == null) _magazine = GetComponent<M2922_MagazineComponent>();
            if (_stats == null) _stats = GetComponent<M2922_PlayerStats>();
        }

        protected override void Update()
        {
            base.Update();
            UpdateHP();
            UpdateAmmo();
            UpdateScore();
        }

        private void UpdateHP()
        {
            if (_hpText != null && _health != null)
                _hpText.text = $"HP: {_health.CurrentHP:F0} / {_health.MaxHP:F0}";
        }

        private void UpdateAmmo()
        {
            if (_ammoText != null && _magazine != null)
                _ammoText.text = $"{_magazine.CurrentAmmo} / {_magazine.Capacity}";
        }

        private void UpdateScore()
        {
            if (_scoreText != null && _stats != null)
                _scoreText.text = $"Score: {_stats.Score}";
        }

        public void ShowHitmarker()
        {
            if (_hitmarker != null)
            {
                _hitmarker.enabled = true;
                SendCustomEventDelayedSeconds(nameof(HideHitmarker), 0.2f);
            }
        }

        public void HideHitmarker()
        {
            if (_hitmarker != null) _hitmarker.enabled = false;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Health", _health != null ? "Linked" : "None", _health != null ? Color.green : Color.red),
                new M2922_GizmoDisplayInfo("Magazine", _magazine != null ? "Linked" : "None", _magazine != null ? Color.green : Color.red),
                new M2922_GizmoDisplayInfo("Stats", _stats != null ? "Linked" : "None", _stats != null ? Color.green : Color.red),
            };
        }
#endif
    }
}
