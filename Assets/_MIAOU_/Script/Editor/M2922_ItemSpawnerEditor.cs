using System.Collections.Generic;
using UdonSharp;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
using VRC.Udon;
using UdonSharpEditor;
using M2922.Component.Health;
using M2922.Component.Inventory;
using M2922.Core;

namespace M2922.Editor
{
    /// <summary>
    /// Outil d'édition : génère les items marqués par M2922_ItemSpawner dans la
    /// scène, puis met à jour les tailles de registres du M2922_Manager
    /// (_inventoryItemMax / _npcMax) selon le contenu réel de la scène.
    /// </summary>
    public static class M2922_ItemSpawnerGenerator
    {
        private const int MIN_REGISTRY = 32;
        private const int REGISTRY_MARGIN = 16;

        /// <summary>
        /// Préfabs sources dont le StackId a été généré pendant la génération —
        /// synchronisés proxy → Udon APRÈS la génération (les assets ne sont pas
        /// dans la scène : SyncAllProxiesToUdon ne les voit pas).
        /// </summary>
        private static readonly HashSet<M2922_InventoryItem> _pendingPrefabSourceSync =
            new HashSet<M2922_InventoryItem>();

        /// <summary>
        /// Ordre IMPORTANT : la GÉNÉRATION (instances, flags, StackId) doit être
        /// terminée AVANT toute opération Udon. Les méthodes de génération ne
        /// font donc AUCUN appel UdonSharpEditorUtility ; tout le proxy → Udon
        /// est fait ici, après.
        /// </summary>
        public static void GenerateAndUpdate(Scene scene)
        {
            _pendingPrefabSourceSync.Clear();

            // 1. GÉNÉRATION pure (instances, _startHidden, StackId, pickup off).
            GenerateAllInScene(scene);

            // 2. UDON : sync proxy → programme sur TOUTE la scène (les instances
            //    générées sont incluses). ⚠ Au start du play mode, l'UdonBehaviour
            //    écrase les champs des proxies C# avec SES bytes sérialisés ; si
            //    ces bytes sont vides (préfabs jamais synchronisés), TOUTES les
            //    variables semblent "écrasées" (None / défauts). UdonSharp ne
            //    pousse proxy → Udon qu'au BUILD : on le fait donc ici, après la
            //    génération.
            SyncAllProxiesToUdon(scene);

            // 3. UDON : sync des préfabs SOURCES touchés (StackId généré) —
            //    pas couverts par l'étape 2.
            SyncPendingPrefabSources();

            UpdateManagerRegistrySizes(scene);
        }

        /// <summary>
        /// Pousse les valeurs des proxies C# vers leur UdonBehaviour pour TOUS les
        /// UdonSharpBehaviours de la scène (équivalent de ce que fait UdonSharp au
        /// build). Sans ça, le runtime écrase les champs au play mode avec des bytes
        /// vides → perte apparente de toutes les variables des préfabs.
        /// </summary>
        public static void SyncAllProxiesToUdon(Scene scene)
        {
            int synced = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UdonSharpBehaviour[] behaviours = root.GetComponentsInChildren<UdonSharpBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    UdonSharpBehaviour behaviour = behaviours[i];
                    if (behaviour == null) continue;

                    try
                    {
                        if (UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour) != null)
                        {
                            UdonSharpEditorUtility.CopyProxyToUdon(behaviour, ProxySerializationPolicy.All);
                            synced++;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ItemSpawner] Sync proxy → Udon impossible sur '{behaviour.name}' ({behaviour.GetType().Name}) : {e.Message}");
                    }
                }
            }

            if (synced > 0)
                Debug.Log($"[ItemSpawner] {synced} comportement(s) UdonSharp synchronisé(s) proxy → Udon.");
        }

        /// <summary>
        /// Sync proxy → programme Udon des PRÉFABS SOURCES dont le StackId a été
        /// généré pendant GenerateAllInScene. Appelé APRÈS la génération.
        /// </summary>
        private static void SyncPendingPrefabSources()
        {
            foreach (M2922_InventoryItem source in _pendingPrefabSourceSync)
            {
                if (source == null) continue;

                // Garde : le backing UdonBehaviour d'un asset non initialisé
                // peut être null → CopyProxyToUdon planterait.
                if (source.GetComponent<UdonBehaviour>() != null &&
                    UdonSharpEditorUtility.GetBackingUdonBehaviour(source) != null)
                    UdonSharpEditorUtility.CopyProxyToUdon(source);
            }

            _pendingPrefabSourceSync.Clear();
        }

        /// <summary>
        /// Instancie tous les M2922_ItemSpawner de la scène, puis supprime les marqueurs.
        /// </summary>
        public static void GenerateAllInScene(Scene scene)
        {
            List<M2922_ItemSpawner> spawners = FindAll<M2922_ItemSpawner>(scene);
            if (spawners.Count == 0) return;

            int created = 0;
            for (int s = 0; s < spawners.Count; s++)
            {
                M2922_ItemSpawner spawner = spawners[s];
                if (spawner == null || spawner.Entries == null) continue;

                for (int e = 0; e < spawner.Entries.Length; e++)
                {
                    M2922_ItemSpawnEntry entry = spawner.Entries[e];
                    if (entry == null || entry.Prefab == null) continue;

                    for (int i = 0; i < entry.Count; i++)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab);
                        if (instance == null) continue;

                        // Pool : parenté au marqueur M2922_ItemSpawner, position
                        // locale (0,0,0), rotation identité, démarre masqué.
                        instance.transform.SetParent(spawner.transform, false);
                        instance.transform.localPosition = Vector3.zero;
                        instance.transform.localRotation = Quaternion.identity;
                        instance.name = $"{entry.Prefab.name} (Pool {i + 1})";

                        MarkStartHidden(instance);

                        created++;
                    }
                }

                if (spawner.RemoveAfterGenerate)
                {
                    // Détache les instances poolées AVANT de supprimer le
                    // marqueur (sinon DestroyImmediate détruirait ses enfants).
                    while (spawner.transform.childCount > 0)
                        spawner.transform.GetChild(0).SetParent(null);
                    Object.DestroyImmediate(spawner);
                }
            }

            if (created > 0)
            {
                // En play mode / build, Unity gère lui-même l'état de la scène
                // (les instances de play mode sont retirées au retour en édition).
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[ItemSpawner] {created} instance(s) ajoutée(s) au pool.");
            }
        }

        /// <summary>
        /// Marque l'item de l'instance générée pour qu'il démarre masqué :
        /// 1. M2922_InventoryItem._startHidden = true (le Start le masque au runtime) ;
        /// 2. désactive IMMÉDIATEMENT le sous-arbre du pickup dans la scène —
        ///    les items du pool sont "rangés" (masqués) comme dans un inventaire,
        ///    pas 500 objets actifs empilés en (0,0,0) au chargement du monde.
        /// ⚠ Ne jamais désactiver le GameObject qui porte le M2922_InventoryItem
        /// (son Start ne tournerait pas → item non enregistré et non spawnable).
        /// </summary>
        private static void MarkStartHidden(GameObject instance)
        {
            M2922_InventoryItem item = instance.GetComponentInChildren<M2922_InventoryItem>(true);
            if (item == null)
            {
                Debug.LogWarning($"[ItemSpawner] Aucun M2922_InventoryItem trouvé sur '{instance.name}' : l'item ne démarrera pas masqué.");
                return;
            }

            // S'assure que le StackId existe sur le préfab source (partagé par
            // toutes les instances), sinon le stacking ne fonctionnerait pas.
            EnsurePrefabStackId(item);

            SerializedObject iso = new SerializedObject(item);
            SerializedProperty prop = iso.FindProperty("_startHidden");
            if (prop != null)
            {
                prop.boolValue = true;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ⚠ UDON : AUCUNE opération Udon ici. La sync proxy → programme des
            // instances générées est faite APRÈS toute la génération, par
            // SyncAllProxiesToUdon(scene) dans GenerateAndUpdate (ordre garanti :
            // génération AVANT Udon).

            // Désactive le sous-arbre du pickup DÈS LA GÉNÉRATION (état "rangé").
            // Si l'item est posé À PLAT sur le pickup (même GameObject), on ne
            // désactive rien : le flag _startHidden le masquera au Start.
            VRCPickup pickup = item.Pickup;
            if (pickup == null)
                pickup = item.GetComponentInChildren<VRCPickup>(true);

            if (pickup != null && pickup.gameObject != item.gameObject)
                pickup.gameObject.SetActive(false);
        }

        /// <summary>Génère un StackId sur le préfab source si absent (partagé par les instances).</summary>
        private static void EnsurePrefabStackId(M2922_InventoryItem item)
        {
            if (!string.IsNullOrEmpty(item.StackId)) return;

            M2922_InventoryItem source = PrefabUtility.GetCorrespondingObjectFromSource(item);
            if (source == null) source = item;

            SerializedObject so = new SerializedObject(source);
            SerializedProperty p = so.FindProperty("StackId");
            if (p != null && string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = System.Guid.NewGuid().ToString("N");
                so.ApplyModifiedPropertiesWithoutUndo();

                // ⚠ UDON : pas de sync ici (génération AVANT Udon). Le source est
                // enregistré pour être synchronisé après la génération, dans
                // SyncPendingPrefabSources (appelé par GenerateAndUpdate).
                _pendingPrefabSourceSync.Add(source);

                EditorUtility.SetDirty(source);
            }
        }

        /// <summary>
        /// Ajuste _inventoryItemMax et _npcMax du Manager selon le nombre réel
        /// d'items/NPCs actifs présents dans la scène (avec une marge de sécurité).
        /// </summary>
        public static void UpdateManagerRegistrySizes(Scene scene)
        {
            M2922_Manager manager = FindManager(scene);
            if (manager == null)
            {
                Debug.LogWarning("[ItemSpawner] Aucun M2922_Manager trouvé dans la scène : tailles de registres non mises à jour.");
                return;
            }

            int itemCount = CountActive<M2922_InventoryItem>(scene);
            int npcCount = CountActiveNpcs(scene);

            int itemMax = Mathf.Max(MIN_REGISTRY, itemCount + REGISTRY_MARGIN);
            int npcMax = Mathf.Max(MIN_REGISTRY, npcCount + REGISTRY_MARGIN);

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty itemMaxProp = so.FindProperty("_inventoryItemMax");
            SerializedProperty npcMaxProp = so.FindProperty("_npcMax");
            if (itemMaxProp != null) itemMaxProp.intValue = itemMax;
            if (npcMaxProp != null) npcMaxProp.intValue = npcMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            Debug.Log($"[ItemSpawner] Registres Manager : {itemCount} items → max {itemMax}, {npcCount} NPCs → max {npcMax}.");
        }

        private static M2922_Manager FindManager(Scene scene)
        {
            List<M2922_Manager> managers = FindAll<M2922_Manager>(scene);
            if (managers.Count > 0) return managers[0];

            GameObject go = GameObject.Find("Manager");
            if (go != null) return go.GetComponent<M2922_Manager>();
            return null;
        }

        private static int CountActiveNpcs(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                M2922_DamageReceiver[] receivers = root.GetComponentsInChildren<M2922_DamageReceiver>(true);
                for (int i = 0; i < receivers.Length; i++)
                {
                    M2922_DamageReceiver receiver = receivers[i];
                    if (receiver == null || !receiver.gameObject.activeInHierarchy) continue;
                    if (receiver.HitboxSystem != null && !receiver.HitboxSystem.IsPlayer) count++;
                }
            }
            return count;
        }

        private static List<T> FindAll<T>(Scene scene) where T : UnityEngine.Component
        {
            List<T> result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] comps = root.GetComponentsInChildren<T>(true);
                result.AddRange(comps);
            }
            return result;
        }

        private static int CountActive<T>(Scene scene) where T : UnityEngine.Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] comps = root.GetComponentsInChildren<T>(true);
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i] != null && comps[i].gameObject.activeInHierarchy) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Menu M2922 : génération manuelle du pool d'items dans la scène active.
    /// </summary>
    public static class M2922_ItemSpawnerMenu
    {
        [MenuItem("M2922/Inventory/Generate Item Pool (Active Scene)", priority = 30)]
        public static void GeneratePool()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[ItemSpawner] Aucune scène active : génération du pool impossible.");
                return;
            }

            M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    /// <summary>Génération automatique au moment du build (par scène traitée).</summary>
    public static class M2922_InventoryBuildProcessor
    {
        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
        }
    }

    /// <summary>
    /// Duplication automatique à l'ENTRÉE EN PLAY MODE (ClientSim / Build & Test).
    /// Les instances créées en play mode sont retirées automatiquement au retour
    /// en mode édition (Unity restaure la scène) → la hiérarchie éditeur reste propre.
    /// </summary>
    public static class M2922_ItemSpawnerPlayModeHook
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
        }
    }

    /// <summary>
    /// Inspector custom du marqueur M2922_ItemSpawner.
    /// Le tableau Entries est affiché comme une liste style CounterUI
    /// (colonnes # / Prefab / Count + boutons +/-) au lieu de
    /// "Element 0 → Prefab / Count" du draw default.
    /// </summary>
    [CustomEditor(typeof(M2922_ItemSpawner))]
    public class M2922_ItemSpawnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _propEntries;
        private SerializedProperty _propRemoveAfterGenerate;

        private ReorderableList _entriesList;

        private void OnEnable()
        {
            _propEntries = serializedObject.FindProperty("Entries");
            _propRemoveAfterGenerate = serializedObject.FindProperty("RemoveAfterGenerate");

            BuildEntriesList();
        }

        private void BuildEntriesList()
        {
            _entriesList = new ReorderableList(
                serializedObject,
                _propEntries,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true)
            {
                drawHeaderCallback = DrawEntriesHeader,
                drawElementCallback = DrawEntriesElement,
                onAddCallback = OnAddEntry,
                onRemoveCallback = OnRemoveEntry,
                onReorderCallbackWithDetails = OnReorderEntries,
                elementHeight = EditorGUIUtility.singleLineHeight + 2f
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Lazy init : l'inspector peut être construit sans OnEnable().
            if (_propEntries == null)
            {
                _propEntries = serializedObject.FindProperty("Entries");
                _propRemoveAfterGenerate = serializedObject.FindProperty("RemoveAfterGenerate");
            }
            if (_entriesList == null || _entriesList.serializedProperty != _propEntries)
                BuildEntriesList();

            // Référence de script (lecture seule).
            SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
            if (scriptProp != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(scriptProp, true);
                EditorGUI.EndDisabledGroup();
            }

            // ---- ENTRIES (liste custom style CounterUI) ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== ENTRIES ===", EditorStyles.boldLabel);
            _entriesList.DoLayoutList();

            EditorGUILayout.PropertyField(_propRemoveAfterGenerate, true);

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "DUPLICATION AUTOMATIQUE à la compilation : les préfabs sont instanciés " +
                "à l'entrée en Play Mode (ClientSim / Build & Test) et au build/upload " +
                "(PostProcessScene). La scène reste PROPRE en édition : aucune instance " +
                "de pool dans la hiérarchie.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        // =============================================
        //  REORDERABLE LIST CALLBACKS
        // =============================================

        /// <summary>Header : "# | Prefab | Count" (réserve la place des boutons +/-).</summary>
        private void DrawEntriesHeader(Rect rect)
        {
            const float countW = 55f;
            const float buttonsW = 45f;

            float usable = rect.width - buttonsW;

            EditorGUI.LabelField(new Rect(rect.x, rect.y, 30, rect.height), "#", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + 30, rect.y, usable - 30 - countW - 5, rect.height), "Prefab", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + usable - countW, rect.y, countW, rect.height), "Count", EditorStyles.miniLabel);
        }

        /// <summary>Ligne : index | Prefab | Count.</summary>
        private void DrawEntriesElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty entry = _propEntries.GetArrayElementAtIndex(index);
            SerializedProperty prefabProp = entry.FindPropertyRelative("Prefab");
            SerializedProperty countProp = entry.FindPropertyRelative("Count");

            const float countW = 55f;

            EditorGUI.LabelField(new Rect(rect.x, rect.y, 25, rect.height), index.ToString());
            EditorGUI.PropertyField(new Rect(rect.x + 30, rect.y, rect.width - 30 - countW - 5, rect.height), prefabProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + rect.width - countW, rect.y, countW, rect.height), countProp, GUIContent.none);
        }

        private void OnAddEntry(ReorderableList list)
        {
            if (_propEntries == null) return;

            int idx = _propEntries.arraySize;
            _propEntries.arraySize++;

            SerializedProperty entry = _propEntries.GetArrayElementAtIndex(idx);
            entry.FindPropertyRelative("Prefab").objectReferenceValue = null;
            entry.FindPropertyRelative("Count").intValue = 1;
        }

        private void OnRemoveEntry(ReorderableList list)
        {
            if (_propEntries == null) return;

            int idx = list.index;
            if (idx >= 0 && idx < _propEntries.arraySize)
                _propEntries.DeleteArrayElementAtIndex(idx);
        }

        private void OnReorderEntries(ReorderableList list, int oldIndex, int newIndex)
        {
            if (_propEntries == null) return;
            _propEntries.MoveArrayElement(oldIndex, newIndex);
        }
    }
}
