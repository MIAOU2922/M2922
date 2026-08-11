using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Contrôle la visibilité des meshes debug hitbox.
    /// - ShowAll   : toujours visibles (debug global)
    /// - HideLocal : cachés si le GameObject appartient au joueur local (soi-même)
    /// - HideAll   : toujours cachés
    /// </summary>
    public enum HitboxDebugVisibility
    {
        ShowAll,
        HideLocal,
        HideAll
    }

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
        [Tooltip("Matériau pour le collider de proximité (rocket only).")]
        [SerializeField] private Material _proximityMaterial;

        [Header("=== VISIBILITY ===")]
        [Tooltip("ShowAll = toujours visible | HideLocal = caché si le GO nous appartient | HideAll = toujours caché")]
        [SerializeField] private HitboxDebugVisibility _visibilityMode = HitboxDebugVisibility.ShowAll;

        [Header("=== PERFORMANCE ===")]
        [Tooltip("Override du FrameSkipCount de base (50). 30 = debug vis ~2×/sec.")]
        [SerializeField] private int _debugVisFrameSkip = 30;
        protected override int FrameSkipCount => _debugVisFrameSkip;
        [Tooltip("Désactiver complètement ce composant en runtime (production).")]
        [SerializeField] private bool _runtimeDisabled = false;

        /// <summary>Mode de visibilité des meshes debug. Modifiable runtime.</summary>
        public HitboxDebugVisibility VisibilityMode
        {
            get => _visibilityMode;
            set
            {
                _visibilityMode = value;
                if (_visibilityMode == HitboxDebugVisibility.HideAll)
                    ClearAll(); // détruit TOUT pour 0 perf
                else
                {
                    if (_createdCount == 0) Rebuild();
                    else ApplyVisibility();
                }
            }
        }

        /// <summary>Désactive/active le visualiseur en runtime (perf).</summary>
        public bool RuntimeDisabled
        {
            get => _runtimeDisabled;
            set
            {
                _runtimeDisabled = value;
                if (_runtimeDisabled) ClearAll();
                else if (_createdCount == 0) Rebuild();
            }
        }

        private GameObject[] _created = new GameObject[0];
        private Collider[] _createdParents = new Collider[0];
        private int _createdCount = 0;

        // Mesh debug pour le collider de proximité (séparé car pas dans _hitboxColliders)
        private GameObject _proximityVis;
        private Collider _proximityVisParent;

        protected override void Start()
        {
            base.Start();
            if (_hitboxSystem == null)
                _hitboxSystem = GetComponent<M2922_HitboxSystem>();

            // Rebuild immédiat (sauf si HideAll : rien à afficher)
            if (_visibilityMode != HitboxDebugVisibility.HideAll)
                Rebuild();

            // Filet de sécurité : si les prefabs n'étaient pas encore assignés
            // (scène chargée dynamiquement), on réessaie dans 2 frames.
            if (_createdCount == 0 && _visibilityMode != HitboxDebugVisibility.HideAll)
                SendCustomEventDelayedFrames("_DelayedRebuild", 2);
        }

        /// <summary>Rebuild différé (filet de sécurité).</summary>
        public void _DelayedRebuild()
        {
            if (_createdCount == 0 && _visibilityMode != HitboxDebugVisibility.HideAll)
                Rebuild();
        }

        private void OnDestroy() { ClearAll(); }
        private void OnDisable() { ClearAll(); }

        /// <summary>
        /// PERFORMANCE: LateUpdate tourne à fréquence réduite (_updateEveryNFrames).\n        /// En production, mettre _runtimeDisabled=true ou _visibilityMode=HideAll.\n        /// </summary>
        protected override void LateUpdate()
        {
            // ── Désactivation complète runtime (production) ──
            if (_runtimeDisabled) return;

            // ── HideAll : meshes détruits, rien à faire ──
            if (_createdCount == 0) return;

            // ── FRAME-SKIP (M2922_Base.ShouldUpdate) ──
            if (!ShouldUpdate()) return;

            // ── Visibilité : cache le résultat (GetOwner est un appel réseau) ──
            bool shouldShow = ComputeShouldShow();
            if (!shouldShow) return; // early-out si tout caché

            for (int i = 0; i < _createdCount; i++)
            {
                GameObject vis = _created[i];
                Collider parentCol = _createdParents[i];
                if (vis == null || parentCol == null) continue;
                ApplyScale(vis.transform, parentCol);

                MeshRenderer mr = vis.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled != shouldShow)
                    mr.enabled = shouldShow;
            }

            // Proximity collider debug mesh
            if (_proximityVis != null && _proximityVisParent != null)
            {
                ApplyScale(_proximityVis.transform, _proximityVisParent);
                MeshRenderer mr = _proximityVis.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled != shouldShow)
                    mr.enabled = shouldShow;
            }
        }

        public void Rebuild()
        {
            ClearAll();
            if (_hitboxSystem == null)
            {
                Debug.LogWarning("[HitboxDebugVisualizer] _hitboxSystem est null, Rebuild annulé.", this);
                return;
            }

            Collider[] cols = _hitboxSystem.HitboxColliders;
            int count = _hitboxSystem.HitboxCount;
            if (cols == null || count == 0)
            {
                Debug.LogWarning($"[HitboxDebugVisualizer] Aucun collider dans HitboxSystem (cols={cols != null}, count={count}), Rebuild annulé.", this);
                return;
            }

            // Vérification rapide des prefabs obligatoires
            if (_cubePrefab == null)
            {
                Debug.LogError("[HitboxDebugVisualizer] _cubePrefab n'est pas assigné ! Les objets debug ne peuvent pas être créés.", this);
                return;
            }

            _created = new GameObject[count];
            _createdParents = new Collider[count];
            _createdCount = 0;

            for (int i = 0; i < count; i++)
            {
                Collider col = cols[i];
                if (col == null) continue;

                bool crit = col.GetComponent<M2922_DamageMultiplier>() != null;
                Material mat = crit ? _critMaterial : _normalMaterial;
                if (mat == null)
                {
                    Debug.LogWarning($"[HitboxDebugVisualizer] Matériel manquant (crit={crit}), hitbox {i} ignorée.", this);
                    continue;
                }

                // Choisir le prefab selon le type de collider
                GameObject prefab = _cubePrefab;
                if (col.GetComponent<SphereCollider>() != null)
                    prefab = _spherePrefab;
                else if (col.GetComponent<CapsuleCollider>() != null)
                    prefab = _capsulePrefab;
                if (prefab == null)
                {
                    Debug.LogWarning($"[HitboxDebugVisualizer] Prefab manquant pour le type de collider {col.GetType().Name}, hitbox {i} ignorée.", this);
                    continue;
                }

                GameObject vis = Instantiate(prefab);
                vis.name = "[DEBUG_HITBOX] " + i + "_" + col.name;
                vis.transform.SetParent(col.transform, false);
                MeshRenderer mr = vis.GetComponent<MeshRenderer>();
                if (mr != null) mr.material = mat;

                _created[_createdCount] = vis;
                _createdParents[_createdCount] = col;
                _createdCount++;
            }

            // --- COLLIDER DE PROXIMITÉ ---
            Collider proxCol = _hitboxSystem.ProximityCollider;
            if (proxCol != null && _proximityMaterial != null)
            {
                GameObject prefab = _cubePrefab;
                if (proxCol.GetComponent<SphereCollider>() != null)
                    prefab = _spherePrefab;
                else if (proxCol.GetComponent<CapsuleCollider>() != null)
                    prefab = _capsulePrefab;

                if (prefab != null)
                {
                    _proximityVis = Instantiate(prefab);
                    _proximityVis.name = "[DEBUG_PROXIMITY] " + proxCol.name;
                    _proximityVis.transform.SetParent(proxCol.transform, false);
                    _proximityVisParent = proxCol;
                    MeshRenderer mr = _proximityVis.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material = _proximityMaterial;
                }
            }

            // Appliquer la visibilité APRÈS avoir TOUT créé (hitboxes + proximity)
            ApplyVisibility();

            Debug.Log($"[HitboxDebugVisualizer] {_createdCount}/{count} hitbox(es) visualisées + proximity={_proximityVis != null}.", this);
        }

        /// <summary>
        /// Calcule si les meshes doivent être visibles selon le mode actuel
        /// et l'ownership réseau du GameObject.
        /// </summary>
        private bool ComputeShouldShow()
        {
            switch (_visibilityMode)
            {
                case HitboxDebugVisibility.ShowAll:
                    return true;
                case HitboxDebugVisibility.HideLocal:
                    // Caché si le GO appartient au joueur local (soi-même)
                    VRCPlayerApi owner = Networking.GetOwner(gameObject);
                    return owner == null || !owner.isLocal;
                case HitboxDebugVisibility.HideAll:
                default:
                    return false;
            }
        }

        /// <summary>
        /// Applique la visibilité à tous les meshes créés.
        /// HideAll → détruit TOUT (0 perf). ShowAll/HideLocal → toggle mr.enabled.
        /// </summary>
        private void ApplyVisibility()
        {
            // HideAll : on détruit tout pour 0 coût CPU/mémoire
            if (_visibilityMode == HitboxDebugVisibility.HideAll)
            {
                ClearAll();
                return;
            }

            bool shouldShow = ComputeShouldShow();
            for (int i = 0; i < _createdCount; i++)
            {
                GameObject vis = _created[i];
                if (vis == null) continue;
                MeshRenderer mr = vis.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = shouldShow;
            }

            if (_proximityVis != null)
            {
                MeshRenderer mr = _proximityVis.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = shouldShow;
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

            if (_proximityVis != null) Destroy(_proximityVis);
            _proximityVis = null;
            _proximityVisParent = null;
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
