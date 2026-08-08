using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("M2922/Debug/Hitbox Debug Visualizer")]
    public class M2922_HitboxDebugVisualizer : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_HitboxSystem _hitboxSystem;

        [Header("=== PREFABS (obligatoires) ===")]
        [SerializeField] private GameObject _spherePrefab;
        [SerializeField] private GameObject _cubePrefab;
        [SerializeField] private GameObject _capsulePrefab;

        [Header("=== MATERIAUX ===")]
        [SerializeField] private Material _normalMaterial;
        [SerializeField] private Material _critMaterial;

        private GameObject[] _created = new GameObject[0];
        private Collider[] _createdParents = new Collider[0];
        private int _createdCount = 0;

        protected override void Start()
        {
            base.Start();
            if (_hitboxSystem == null)
                _hitboxSystem = GetComponent<M2922_HitboxSystem>();
            Rebuild();
        }

        private void OnDestroy() { ClearAll(); }
        private void OnDisable() { ClearAll(); }

        private void LateUpdate()
        {
            for (int i = 0; i < _createdCount; i++)
            {
                GameObject vis = _created[i];
                Collider parentCol = _createdParents[i];
                if (vis == null || parentCol == null) continue;
                ApplyScale(vis.transform, parentCol);
            }
        }

        public void Rebuild()
        {
            ClearAll();
            if (_hitboxSystem == null) return;

            Collider[] cols = _hitboxSystem.HitboxColliders;
            int count = _hitboxSystem.HitboxCount;
            if (cols == null || count == 0) return;

            _created = new GameObject[count];
            _createdParents = new Collider[count];
            _createdCount = 0;

            for (int i = 0; i < count; i++)
            {
                Collider col = cols[i];
                if (col == null) continue;

                bool crit = col.GetComponent<M2922_DamageMultiplier>() != null;
                Material mat = crit ? _critMaterial : _normalMaterial;
                if (mat == null) continue;

                // Choisir le prefab selon le type de collider
                GameObject prefab = _cubePrefab;
                if (col.GetComponent<SphereCollider>() != null)
                    prefab = _spherePrefab;
                else if (col.GetComponent<CapsuleCollider>() != null)
                    prefab = _capsulePrefab;
                if (prefab == null) continue;

                GameObject vis = Instantiate(prefab);
                vis.name = "[DEBUG_HITBOX] " + i + "_" + col.name;
                vis.transform.SetParent(col.transform, false);
                MeshRenderer mr = vis.GetComponent<MeshRenderer>();
                if (mr != null) mr.material = mat;

                _created[_createdCount] = vis;
                _createdParents[_createdCount] = col;
                _createdCount++;
            }
        }

        private void ClearAll()
        {
            for (int i = 0; i < _createdCount; i++)
            {
                if (_created[i] != null) Destroy(_created[i]);
                _created[i] = null;
                _createdParents[i] = null;
            }
            _createdCount = 0;
        }

        private static void ApplyScale(Transform t, Collider col)
        {
            SphereCollider sp = col.GetComponent<SphereCollider>();
            if (sp != null)
            {
                t.localPosition = sp.center;
                float d = sp.radius * 2f;
                t.localScale = new Vector3(d, d, d);
                return;
            }

            BoxCollider bx = col.GetComponent<BoxCollider>();
            if (bx != null)
            {
                t.localPosition = bx.center;
                t.localScale = bx.size;
                return;
            }

            CapsuleCollider cp = col.GetComponent<CapsuleCollider>();
            if (cp != null)
            {
                t.localPosition = cp.center;
                float d = cp.radius * 2f;
                float h = cp.height / 2f;
                t.localScale = new Vector3(d, h, d);
                if (cp.direction == 0) t.localRotation = Quaternion.Euler(0f, 0f, 90f);
                else if (cp.direction == 2) t.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }
}
