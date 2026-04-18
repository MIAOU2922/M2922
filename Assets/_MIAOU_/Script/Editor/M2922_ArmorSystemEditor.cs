using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_ArmorSystem))]
    public class M2922_ArmorSystemEditor : M2922_BaseEditor
    {
        private static readonly string[] _damageTypeNames =
            { "Generic", "Bullet", "Explosion", "Melee", "Fire", "Energy", "Fall" };

        // ── Settings ──────────────────────────────────────────────────────────
        private SerializedProperty _baseMaxArmorPointsProp;
        private SerializedProperty _absorptionProp;
        private SerializedProperty _damageTypeResistanceProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _settingsOpen   = true;
        private bool _resistanceOpen = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _baseMaxArmorPointsProp   = serializedObject.FindProperty("_baseMaxArmorPoints");
            _absorptionProp           = serializedObject.FindProperty("_absorption");
            _damageTypeResistanceProp = serializedObject.FindProperty("_damageTypeResistance");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_ArmorSystem armor = (M2922_ArmorSystem)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);

                float ap    = armor.ArmorPoints;
                float maxAp = armor.MaxArmorPoints;
                float ratio = maxAp > 0f ? ap / maxAp : 0f;

                Color barColor = ratio > 0.5f ? new Color(0.3f, 0.6f, 1f)
                               : ratio > 0.25f ? Color.yellow
                               : Color.red;
                Color prev = GUI.color;
                GUI.color  = barColor;
                Rect rect  = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(rect, ratio, $"{ap:F1} / {maxAp:F1} AP");
                GUI.color  = prev;

                EditorGUILayout.LabelField("Has Armor", armor.HasArmor.ToString(), EditorStyles.miniLabel);

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("R\u00e9paration compl\u00e8te")) armor.RepairFull();
                if (GUILayout.Button("+25 AP"))              armor.RepairArmor(25f);
                if (GUILayout.Button("Reset Max"))           armor.ResetMaxArmorPoints();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }

            // =================================================================
            // ARMOR SETTINGS
            // =================================================================
            _settingsOpen = Section("ARMOR SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_baseMaxArmorPointsProp,
                    new GUIContent("Base Max Armor Points",
                        "Points d'armure maximum de base. Modifiable via SetMaxArmorPoints()."));

                EditorGUILayout.PropertyField(_absorptionProp,
                    new GUIContent("Base Absorption (0\u22121)",
                        "Fraction des d\u00e9g\u00e2ts absorb\u00e9s (0\u202f= aucune, 1\u202f= totale).\nModul\u00e9e par les r\u00e9sistances par type."));

                float abs = _absorptionProp.floatValue;
                EditorGUILayout.LabelField(
                    $"\u2192 Absorption de base\u202f: {abs * 100f:F0}%  (avant r\u00e9sistances)",
                    EditorStyles.miniLabel);

                if (Application.isPlaying)
                    EditorGUILayout.LabelField(
                        $"\u2192 Armure effective\u202f: {armor.ArmorPoints:F1}\u202f/\u202f{armor.MaxArmorPoints:F1}",
                        EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(
                        $"\u2192 Spawne avec {_baseMaxArmorPointsProp.floatValue:F0} AP (plein)",
                        EditorStyles.miniLabel);
            });

            // =================================================================
            // RESISTANCES PAR TYPE
            // =================================================================
            _resistanceOpen = Section("RESISTANCES PAR TYPE", _resistanceOpen, () =>
            {
                EditorGUILayout.HelpBox(
                    "Multiplicateur d'absorption par type de d\u00e9g\u00e2t.\n" +
                    "1.0\u202f= normal\u202f|\u202f0.0\u202f= aucune absorption\u202f|\u202f2.0\u202f= double r\u00e9sistance",
                    MessageType.None);
                GUILayout.Space(2);

                if (_damageTypeResistanceProp.arraySize != _damageTypeNames.Length)
                    _damageTypeResistanceProp.arraySize = _damageTypeNames.Length;

                float baseAbsorption = _absorptionProp.floatValue;

                // En-t\u00eates
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type",           EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("Multiplicateur", EditorStyles.miniLabel, GUILayout.MinWidth(60));
                EditorGUILayout.LabelField("Absorption eff.",EditorStyles.miniLabel, GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < _damageTypeNames.Length; i++)
                {
                    SerializedProperty element = _damageTypeResistanceProp.GetArrayElementAtIndex(i);
                    float effectiveAbs = Mathf.Clamp01(baseAbsorption * element.floatValue);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(_damageTypeNames[i], GUILayout.Width(80));
                    EditorGUILayout.PropertyField(element, GUIContent.none, GUILayout.MinWidth(60));
                    EditorGUILayout.LabelField(
                        $"{effectiveAbs * 100f:F0}%",
                        EditorStyles.miniLabel, GUILayout.Width(90));
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(4);
                if (GUILayout.Button("Reset toutes les r\u00e9sistances \u00e0 1.0"))
                {
                    for (int i = 0; i < _damageTypeResistanceProp.arraySize; i++)
                        _damageTypeResistanceProp.GetArrayElementAtIndex(i).floatValue = 1f;
                }
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
