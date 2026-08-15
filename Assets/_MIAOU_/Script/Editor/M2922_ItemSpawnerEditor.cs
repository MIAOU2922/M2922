using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
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

        public static void GenerateAndUpdate(Scene scene)
        {
            GenerateAllInScene(scene);
            UpdateManagerRegistrySizes(scene);
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

                        // Pool : origine (0,0,0), rotation identité, démarre masqué.
                        instance.transform.SetParent(null);
                        instance.transform.position = Vector3.zero;
                        instance.transform.rotation = Quaternion.identity;
                        instance.name = $"{entry.Prefab.name} (Pool {i + 1})";

                        MarkStartHidden(instance);

                        created++;
                    }
                }

                if (spawner.RemoveAfterGenerate)
                    Object.DestroyImmediate(spawner);
            }

            if (created > 0)
            {
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
                "Génère les instances dans la scène AVANT l'upload (les objets réseau " +
                "doivent exister dans la scène). Le bouton sauve la scène après génération.",
                MessageType.Info);

            if (GUILayout.Button("Générer dans la scène (et sauver)"))
            {
                Scene scene = EditorSceneManager.GetActiveScene();
                M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
                EditorSceneManager.SaveScene(scene);
            }

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
