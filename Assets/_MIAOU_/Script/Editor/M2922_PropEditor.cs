using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Entity.Prop;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_Prop))]
    public class M2922_PropEditor : M2922_BaseEditor
    {
        // ── Entity ──────────────────────────────────────────────────────────
        private SerializedProperty _entityIdProp;
        private SerializedProperty _entityNameProp;
        private SerializedProperty _entityTypeProp;

        // ── Systems ──────────────────────────────────────────────────────────
        private SerializedProperty _healthSystemProp;
        private SerializedProperty _buffSystemProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _runtimeOpen  = true;
        private bool _entityOpen   = true;
        private bool _systemsOpen  = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _entityIdProp     = serializedObject.FindProperty("_entityId");
            _entityNameProp   = serializedObject.FindProperty("_entityName");
            _entityTypeProp   = serializedObject.FindProperty("_entityType");
            _healthSystemProp = serializedObject.FindProperty("HealthSystem");
            _buffSystemProp   = serializedObject.FindProperty("BuffSystem");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_Prop prop = (M2922_Prop)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    // Is Held
                    Color prev = GUI.contentColor;
                    GUI.contentColor = prop.IsHeld ? Color.cyan : Color.gray;
                    EditorGUILayout.LabelField(
                        prop.IsHeld ? "\u25cf Tenu par un joueur" : "\u25cb Pos\u00e9",
                        EditorStyles.boldLabel);
                    GUI.contentColor = prev;

                    // Health (si pr\u00e9sent)
                    if (prop.HasHealth)
                    {
                        GUILayout.Space(2);
                        float hp    = prop.Health;
                        float maxHp = prop.MaxHealth;
                        float ratio = maxHp > 0f ? hp / maxHp : 0f;

                        Color barColor = ratio > 0.5f ? Color.green
                                       : ratio > 0.25f ? Color.yellow
                                       : Color.red;
                        Color prevC = GUI.color;
                        GUI.color   = barColor;
                        Rect rect   = EditorGUILayout.GetControlRect(false, 18f);
                        EditorGUI.ProgressBar(rect, ratio, $"{hp:F1} / {maxHp:F1} HP");
                        GUI.color   = prevC;

                        EditorGUILayout.LabelField(
                            prop.IsAlive ? "Vivant" : "D\u00e9truit",
                            EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Pas de HealthSystem (invincible)",
                            EditorStyles.miniLabel);
                    }
                });
            }

            // =================================================================
            // ENTITY SETTINGS
            // =================================================================
            _entityOpen = Section("ENTITY SETTINGS", _entityOpen, () =>
            {
                EditorGUILayout.PropertyField(_entityIdProp,   new GUIContent("Entity ID"));
                EditorGUILayout.PropertyField(_entityNameProp, new GUIContent("Entity Name"));
                EditorGUILayout.PropertyField(_entityTypeProp, new GUIContent("Entity Type"));
            });

            // =================================================================
            // SYSTEMS
            // =================================================================
            _systemsOpen = Section("SYSTEMS", _systemsOpen, () =>
            {
                EditorGUILayout.PropertyField(_healthSystemProp,
                    new GUIContent("Health System",
                        "Laissez vide pour un prop invincible."));
                EditorGUILayout.PropertyField(_buffSystemProp,
                    new GUIContent("Buff System",
                        "Optionnel — buffs appliqu\u00e9s \u00e0 ce prop."));

                if (_healthSystemProp.objectReferenceValue == null)
                    EditorGUILayout.LabelField("\u2192 Pas de sant\u00e9 \u2014 prop invincible",
                        EditorStyles.miniLabel);
                if (_buffSystemProp.objectReferenceValue == null)
                    EditorGUILayout.LabelField("\u2192 Pas de buffs",
                        EditorStyles.miniLabel);
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
