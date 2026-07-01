using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Gestionnaire d'object pooling : spawn/despawn optimisé.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PoolManager : M2922_Base
    {
        [Header("=== POOLS ===")]
        [SerializeField] private string[] _poolKeys;
        [SerializeField] private GameObject[] _poolPrefabs;
        [SerializeField] private int[] _poolInitialSizes;
        [SerializeField] private int[] _poolMaxSizes;

        // Udon-compatible : DataList[] au lieu de Queue<GameObject>[]
        private DataList[] _poolQueues;
        private int[] _totalCreated;

        private int PoolCount
        {
            get
            {
                if (_poolKeys == null) return 0;
                return _poolKeys.Length;
            }
        }

        protected override void Start()
        {
            base.Start();

            int count = PoolCount;
            _poolQueues = new DataList[count];
            _totalCreated = new int[count];

            for (int i = 0; i < count; i++)
            {
                _poolQueues[i] = new DataList();
                _totalCreated[i] = 0;

                int initial = (i < _poolInitialSizes.Length) ? _poolInitialSizes[i] : 10;
                for (int j = 0; j < initial; j++)
                {
                    CreateAndEnqueue(i);
                }
            }
        }

        private void CreateAndEnqueue(int poolIndex)
        {
            if (poolIndex >= _poolPrefabs.Length || _poolPrefabs[poolIndex] == null) return;
            GameObject obj = Instantiate(_poolPrefabs[poolIndex]);
            obj.SetActive(false);

            M2922_Poolable poolable = obj.GetComponent<M2922_Poolable>();
            if (poolable != null) poolable.SetPoolManager(this);

            _poolQueues[poolIndex].Add(obj);
            _totalCreated[poolIndex]++;
        }

        private int GetPoolIndex(string key)
        {
            if (_poolKeys == null) return -1;
            for (int i = 0; i < _poolKeys.Length; i++)
                if (_poolKeys[i] == key) return i;
            return -1;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            int count = PoolCount;
            int totalAlive = 0;
            int totalCreated = 0;
            if (_poolQueues != null)
            {
                for (int i = 0; i < _poolQueues.Length; i++)
                {
                    if (_poolQueues[i] != null)
                        totalAlive += _poolQueues[i].Count;
                }
            }
            if (_totalCreated != null)
            {
                for (int i = 0; i < _totalCreated.Length; i++)
                    totalCreated += _totalCreated[i];
            }
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Pools", count.ToString(), Color.cyan),
                new M2922_GizmoDisplayInfo("Alive", totalAlive.ToString(), Color.green),
                new M2922_GizmoDisplayInfo("Created", totalCreated.ToString()),
            };
        }
#endif

        public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
        {
            int index = GetPoolIndex(key);
            if (index < 0 || index >= _poolQueues.Length) return null;

            // Pool vide → créer si pas encore au max
            if (_poolQueues[index].Count == 0)
            {
                int max = (index < _poolMaxSizes.Length) ? _poolMaxSizes[index] : 50;
                if (_totalCreated[index] < max)
                    CreateAndEnqueue(index);
                else
                    return null; // Pool saturé
            }

            // Dequeue: get first, then remove it
            DataToken token = _poolQueues[index][0];
            GameObject obj = (GameObject)token.Reference;
            _poolQueues[index].RemoveAt(0);
            obj.transform.SetPositionAndRotation(position, rotation);

            M2922_Poolable poolable = obj.GetComponent<M2922_Poolable>();
            if (poolable != null) poolable.OnSpawned();
            else obj.SetActive(true);

            return obj;
        }

        public void Return(M2922_Poolable poolable)
        {
            int index = GetPoolIndex(poolable.PoolKey);
            if (index < 0)
            {
                Destroy(poolable.gameObject);
                return;
            }

            poolable.OnDespawned();
            _poolQueues[index].Add(poolable.gameObject);
        }
    }
}
