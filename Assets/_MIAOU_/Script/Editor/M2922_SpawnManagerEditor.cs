using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Spawning;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_SpawnManager))]
    public class M2922_SpawnManagerEditor : M2922_BaseEditor
    {
        // ── References ────────────────────────────────────────────────────────
        private SerializedProperty _teamManagerProp;

        // ── Spawn settings ────────────────────────────────────────────────────
        private SerializedProperty _spawnStrategyProp;
        private SerializedProperty _spawnProtectionDurationProp;
        private SerializedProperty _spawnCooldownProp;
        private SerializedProperty _minEnemyDistanceProp;

        // ── Spawn points ──────────────────────────────────────────────────────
        private SerializedProperty _autoDiscoverProp;
        private SerializedProperty _spawnPointsProp;

        // ── Respawn ───────────────────────────────────────────────────────────
        private SerializedProperty _autoRespawnProp;
        private SerializedProperty _respawnDelayProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _refsOpen     = true;
        private bool _settingsOpen = true;
        private bool _pointsOpen   = true;
        private bool _respawnOpen  = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _teamManagerProp             = serializedObject.FindProperty("_teamManager");
            _spawnStrategyProp           = serializedObject.FindProperty("_spawnStrategy");
            _spawnProtectionDurationProp = serializedObject.FindProperty("_spawnProtectionDuration");
            _spawnCooldownProp           = serializedObject.FindProperty("_spawnCooldown");
            _minEnemyDistanceProp        = serializedObject.FindProperty("_minEnemyDistance");
            _autoDiscoverProp            = serializedObject.FindProperty("_autoDiscoverSpawnPoints");
            _spawnPointsProp             = serializedObject.FindProperty("_spawnPoints");
            _autoRespawnProp             = serializedObject.FindProperty("_autoRespawn");
            _respawnDelayProp            = serializedObject.FindProperty("_respawnDelay");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Mode : {(_teamManagerProp.objectReferenceValue != null ? "Team-based" : "FFA")}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Spawn points actifs : {_spawnPointsProp.arraySize}",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }

            // =================================================================
            // RÉFÉRENCES
            // =================================================================
            _refsOpen = Section("RÉFÉRENCES", _refsOpen, () =>
            {
                EditorGUILayout.PropertyField(_teamManagerProp,
                    new GUIContent("Team Manager", "Optionnel — laisser vide pour le mode FFA."));

                bool isFFA = _teamManagerProp.objectReferenceValue == null;
                EditorGUILayout.LabelField(
                    isFFA ? "→ Mode : FFA (pas de TeamManager)" : "→ Mode : Team-based",
                    EditorStyles.miniLabel);
            });

            // =================================================================
            // SPAWN SETTINGS
            // =================================================================
            _settingsOpen = Section("SPAWN SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_spawnStrategyProp,
                    new GUIContent("Stratégie", "Random / Sequential / LeastRecent / Farthest"));
                EditorGUILayout.PropertyField(_spawnProtectionDurationProp,
                    new GUIContent("Protection spawn (s)",
                        "Durée d'invincibilité après spawn."));
                EditorGUILayout.PropertyField(_spawnCooldownProp,
                    new GUIContent("Cooldown spawn (s)",
                        "Délai minimum entre deux spawns au même point."));
                EditorGUILayout.PropertyField(_minEnemyDistanceProp,
                    new GUIContent("Distance min ennemis (m)",
                        "Distance minimum entre spawn et ennemis. 0 = désactivé."));
            });

            // =================================================================
            // SPAWN POINTS
            // =================================================================
            _pointsOpen = Section("SPAWN POINTS", _pointsOpen, () =>
            {
                EditorGUILayout.PropertyField(_autoDiscoverProp,
                    new GUIContent("Auto Discover",
                        "Trouve automatiquement tous les M2922_SpawnPoint dans la scène."));

                if (_autoDiscoverProp.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        $"La liste sera remplie automatiquement au runtime.\n" +
                        $"Points trouvés en scène : {_spawnPointsProp.arraySize}",
                        MessageType.None);

                    if (GUILayout.Button("Refresh maintenant (Edit mode)"))
                        RefreshSpawnPoints();
                }
                else
                {
                    EditorGUILayout.PropertyField(_spawnPointsProp,
                        new GUIContent("Spawn Points"));
                }
            });

            // =================================================================
            // RESPAWN
            // =================================================================
            _respawnOpen = Section("RESPAWN", _respawnOpen, () =>
            {
                EditorGUILayout.PropertyField(_autoRespawnProp,
                    new GUIContent("Auto Respawn"));
                EditorGUI.BeginDisabledGroup(!_autoRespawnProp.boolValue);
                EditorGUILayout.PropertyField(_respawnDelayProp,
                    new GUIContent("Délai respawn (s)"));
                EditorGUI.EndDisabledGroup();
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private void RefreshSpawnPoints()
        {
            M2922_SpawnPoint[] found = Object.FindObjectsOfType<M2922_SpawnPoint>();
            _spawnPointsProp.ClearArray();
            _spawnPointsProp.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++)
                _spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"[M2922_SpawnManager] {found.Length} spawn points trouvés et assignés.");
        }
    }
}
