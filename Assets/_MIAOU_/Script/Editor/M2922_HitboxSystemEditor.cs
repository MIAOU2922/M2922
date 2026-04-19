using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_HitboxSystem))]
    public class M2922_HitboxSystemEditor : M2922_BaseEditor
    {
        // ── Props ──────────────────────────────────────────────────────────────
        private SerializedProperty _hitboxCollidersProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _configOpen  = true;
        private bool _listOpen    = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _hitboxCollidersProp = serializedObject.FindProperty("_hitboxColliders");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_HitboxSystem sys = (M2922_HitboxSystem)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Colliders enregistrés : {sys.HitboxCount}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }

            // =================================================================
            // HITBOX CONFIG
            // =================================================================
            _configOpen = Section("HITBOX CONFIG", _configOpen, () =>
            {
                // Bouton Auto-Discover
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Colliders",
                        "Colliders qui représentent le corps de cette entité.\n" +
                        "Assigner manuellement ou cliquer Auto-Discover."),
                    GUILayout.Width(EditorGUIUtility.labelWidth));

                if (GUILayout.Button("Auto-Discover", GUILayout.Width(120)))
                {
                    Undo.RecordObject(sys, "HitboxSystem Auto-Discover");
                    sys.AutoDiscover();
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(2);

                // Tableau de colliders avec liste dépliable
                _listOpen = Section("Colliders (" + _hitboxCollidersProp.arraySize + ")", _listOpen, () =>
                {
                    for (int i = 0; i < _hitboxCollidersProp.arraySize; i++)
                    {
                        SerializedProperty elem = _hitboxCollidersProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.BeginHorizontal();

                        // Icône crit si le GO a M2922_CritZone
                        Collider col = elem.objectReferenceValue as Collider;
                        bool isCrit  = col != null && col.GetComponent<M2922_CritZone>() != null;

                        Color prev = GUI.color;
                        GUI.color  = isCrit ? new Color(1f, 0.5f, 0.5f) : Color.white;
                        EditorGUILayout.PropertyField(elem, new GUIContent(isCrit ? $"[{i}] CRIT" : $"[{i}]"));
                        GUI.color  = prev;

                        if (GUILayout.Button("×", GUILayout.Width(20)))
                        {
                            _hitboxCollidersProp.DeleteArrayElementAtIndex(i);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    GUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("+ Ajouter un slot"))
                        _hitboxCollidersProp.InsertArrayElementAtIndex(_hitboxCollidersProp.arraySize);
                    if (GUILayout.Button("Vider"))
                    {
                        if (EditorUtility.DisplayDialog("Vider les hitboxes",
                            "Supprimer tous les colliders enregistrés ?", "Oui", "Annuler"))
                            _hitboxCollidersProp.ClearArray();
                    }
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        "Rouge = zone critique (M2922_CritZone présent sur le GO).\n" +
                        "Blanc = hitbox normale.",
                        MessageType.Info);
                });
            });

            // =================================================================
            // VISUAL DEBUG
            // =================================================================
            DrawVisualDebug();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
