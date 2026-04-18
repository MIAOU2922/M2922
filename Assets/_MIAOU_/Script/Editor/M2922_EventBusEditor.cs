using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Core;
using EventType = M2922.Core.EventType;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_EventBus))]
    public class M2922_EventBusEditor : M2922_BaseEditor
    {
        // ── Settings ──────────────────────────────────────────────────────────
        private SerializedProperty _maxListenersProp;
        private SerializedProperty _eventTypeCountProp;
        private SerializedProperty _maxEventTypeValueProp;
        private SerializedProperty _poolProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _settingsOpen = true;
        private bool _poolOpen     = false;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _maxListenersProp      = serializedObject.FindProperty("maxListenersPerEvent");
            _eventTypeCountProp    = serializedObject.FindProperty("_eventTypeCount");
            _maxEventTypeValueProp = serializedObject.FindProperty("_maxEventTypeValue");
            _poolProp              = serializedObject.FindProperty("_pool");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            // =================================================================
            // EVENT BUS SETTINGS
            // =================================================================
            _settingsOpen = Section("EVENT BUS SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_maxListenersProp,
                    new GUIContent("Max Listeners / Event",
                        "Nombre maximum d'abonnés par type d'événement."));

                GUILayout.Space(4);

                int configured = _eventTypeCountProp.intValue;
                int actual     = System.Enum.GetValues(typeof(EventType)).Length;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Types d'événements configurés", GUILayout.Width(220));
                EditorGUILayout.LabelField(configured.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Types dans l'enum EventType", GUILayout.Width(220));
                EditorGUILayout.LabelField(actual.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (configured != actual)
                {
                    EditorGUILayout.HelpBox(
                        $"Désynchronisé ! Configuré : {configured} | Enum : {actual}\n" +
                        "Cliquez sur Synchroniser pour corriger.",
                        MessageType.Warning);

                    if (GUILayout.Button("Synchroniser le count"))
                    {
                        _eventTypeCountProp.intValue = actual;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                        Debug.Log($"[M2922 EventBus] Event count : {configured} → {actual}");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("→ Synchronisé ✓", EditorStyles.miniLabel);
                }

                GUILayout.Space(4);
                EditorGUILayout.PropertyField(_maxEventTypeValueProp,
                    new GUIContent("Max EventType Value + 1",
                        "Calculé automatiquement via OnValidate."));
            });

            // =================================================================
            // EVENT DATA POOL
            // =================================================================
            _poolOpen = Section("EVENT DATA POOL", _poolOpen, () =>
            {
                EditorGUILayout.HelpBox(
                    "Slots M2922_EventData — enfants du GameObject EventBus.\n" +
                    "Laisser vide : auto-découverts au Start().",
                    MessageType.None);
                EditorGUILayout.PropertyField(_poolProp, new GUIContent("Pool"));
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
