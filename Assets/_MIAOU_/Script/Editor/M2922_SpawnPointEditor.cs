using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Spawning;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_SpawnPoint))]
    public class M2922_SpawnPointEditor : M2922_BaseEditor
    {
        // ── Settings ──────────────────────────────────────────────────────────
        private SerializedProperty _teamIndexProp;
        private SerializedProperty _priorityProp;
        private SerializedProperty _isActiveProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _runtimeOpen  = true;
        private bool _settingsOpen = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _teamIndexProp = serializedObject.FindProperty("_teamIndex");
            _priorityProp  = serializedObject.FindProperty("_priority");
            _isActiveProp  = serializedObject.FindProperty("_isActive");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_SpawnPoint sp = (M2922_SpawnPoint)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    Color prev = GUI.contentColor;
                    GUI.contentColor = sp.IsActive ? Color.green : Color.red;
                    EditorGUILayout.LabelField(
                        sp.IsActive ? "● Actif" : "● Inactif",
                        EditorStyles.boldLabel);
                    GUI.contentColor = prev;

                    int team = sp.TeamIndex;
                    EditorGUILayout.LabelField(
                        team < 0 ? "Mode : FFA (all teams)" : $"Équipe : {team}",
                        EditorStyles.miniLabel);

                    EditorGUILayout.LabelField(
                        $"Temps depuis dernier spawn : {sp.GetTimeSinceLastSpawn():F1} s",
                        EditorStyles.miniLabel);

                    GUILayout.Space(4);
                    if (GUILayout.Button("RecordSpawn (test)"))
                        sp.RecordSpawn();
                });
            }

            // =================================================================
            // SPAWN SETTINGS
            // =================================================================
            _settingsOpen = Section("SPAWN SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_teamIndexProp,
                    new GUIContent("Team Index",
                        "-1 = disponible pour tous (FFA)\n" +
                        "≥ 0 = réservé à cette équipe."));
                EditorGUILayout.PropertyField(_priorityProp,
                    new GUIContent("Priority",
                        "Priorité relative lors de la sélection du spawn point.\n" +
                        "Plus haute = préféré."));
                EditorGUILayout.PropertyField(_isActiveProp,
                    new GUIContent("Is Active",
                        "Désactivez pour exclure ce point du pool de sélection."));
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
