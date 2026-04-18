using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Entity;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_Entity))]
    public class M2922_EntityEditor : M2922_BaseEditor
    {
        // ── Settings ──────────────────────────────────────────────────────────
        private SerializedProperty _entityIdProp;
        private SerializedProperty _entityNameProp;
        private SerializedProperty _entityTypeProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _runtimeOpen  = false;
        private bool _settingsOpen = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _entityIdProp   = serializedObject.FindProperty("_entityId");
            _entityNameProp = serializedObject.FindProperty("_entityName");
            _entityTypeProp = serializedObject.FindProperty("_entityType");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_Entity entity = (M2922_Entity)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    Color prev = GUI.contentColor;
                    GUI.contentColor = entity.IsActive ? Color.green : Color.red;
                    EditorGUILayout.LabelField(
                        entity.IsActive ? "● Actif" : "● Inactif",
                        EditorStyles.boldLabel);
                    GUI.contentColor = prev;
                });
            }

            // =================================================================
            // ENTITY SETTINGS
            // =================================================================
            _settingsOpen = Section("ENTITY SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_entityIdProp,
                    new GUIContent("Entity ID",
                        "Identifiant unique de l'entité dans le monde."));
                EditorGUILayout.PropertyField(_entityNameProp,
                    new GUIContent("Entity Name"));
                EditorGUILayout.PropertyField(_entityTypeProp,
                    new GUIContent("Entity Type"));
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
