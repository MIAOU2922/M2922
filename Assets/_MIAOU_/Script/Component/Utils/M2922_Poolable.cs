using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Objet compatible avec l'object pooling.
    /// </summary>
    [AddComponentMenu("M2922/Utils/Poolable")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_Poolable : M2922_Base
    {
        [Header("=== POOL ===")]
        [SerializeField] private string _poolKey = "default";
        [SerializeField] private float _autoReturnDelay = -1f; // -1 = manuel

        private M2922_PoolManager _poolManager;
        private float _spawnTime = 0f;

        public string PoolKey => _poolKey;

        protected override void Start()
        {
            base.Start();
            _spawnTime = Time.time;
        }

        protected override void Update()
        {
            base.Update();
            if (_autoReturnDelay > 0f && Time.time - _spawnTime > _autoReturnDelay)
                ReturnToPool();
        }

        public void SetPoolManager(M2922_PoolManager manager)
        {
            _poolManager = manager;
        }

        public void ReturnToPool()
        {
            if (_poolManager != null)
                _poolManager.Return(this);
            else
                Destroy(gameObject);
        }

        public void OnSpawned()
        {
            _spawnTime = Time.time;
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Pool Key", _poolKey, Color.cyan),
                new M2922_GizmoDisplayInfo("Auto Return", _autoReturnDelay > 0 ? $"{_autoReturnDelay}s" : "Manual"),
            };
        }
#endif
    }
}
