using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Ammo;

namespace M2922.Component.Weapon
{
    public enum ReloadType { Magazine, Heat, Single }

    /// <summary>
    /// Gestion du rechargement : automatique, manuel, heat-based.
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Reload Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ReloadComponent : M2922_Base
    {
        [Header("=== RELOAD CONFIG ===")]
        [SerializeField] private ReloadType _reloadType = ReloadType.Magazine;
        [SerializeField] private float _reloadTime = 2f;
        [SerializeField] private bool _autoReload = true;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_MagazineComponent _magazine;

        private bool _isReloading = false;
        private float _reloadStartTime = 0f;

        public bool IsReloading => _isReloading;

        protected override void Start()
        {
            base.Start();
            if (_magazine == null) _magazine = GetComponent<M2922_MagazineComponent>();
        }

        protected override void Update()
        {
            base.Update();

            if (_isReloading && Time.time - _reloadStartTime > _reloadTime)
                CompleteReload();

            if (_autoReload && _magazine != null && _magazine.IsEmpty && !_isReloading)
                StartReload();
        }

        public void StartReload()
        {
            if (_isReloading) return;
            if (_magazine != null && _magazine.IsFull) return;

            _isReloading = true;
            _reloadStartTime = Time.time;
            this.Log("Reloading...");
        }

        private void CompleteReload()
        {
            _isReloading = false;
            if (_magazine != null) _magazine.Refill();
            this.Log("Reload complete.");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Type", _reloadType.ToString()),
                new M2922_GizmoDisplayInfo("Time", $"{_reloadTime}s"),
                new M2922_GizmoDisplayInfo("Auto", _autoReload ? "ON" : "OFF", _autoReload ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Reloading", _isReloading ? "YES" : "NO", _isReloading ? Color.yellow : Color.gray),
            };
        }
#endif
    }
}
