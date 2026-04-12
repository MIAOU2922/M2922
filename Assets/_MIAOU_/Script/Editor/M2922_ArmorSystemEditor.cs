using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_ArmorSystem))]
    public class M2922_ArmorSystemEditor : UnityEditor.Editor
    {
        private static readonly string[] _damageTypeNames = { "Generic", "Bullet", "Explosion", "Melee", "Fire", "Energy", "Fall" };

        private SerializedProperty _baseMaxArmorPointsProp;
        private SerializedProperty _absorptionProp;
        private SerializedProperty _damageTypeResistanceProp;

        private bool _resistanceFoldout = true;

        private void OnEnable()
        {
            _baseMaxArmorPointsProp     = serializedObject.FindProperty("_baseMaxArmorPoints");
            _absorptionProp             = serializedObject.FindProperty("_absorption");
            _damageTypeResistanceProp   = serializedObject.FindProperty("_damageTypeResistance");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            serializedObject.Update();

            M2922_ArmorSystem armor = (M2922_ArmorSystem)target;

            // === STATUS (Play mode) ===
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);

                float ap    = armor.ArmorPoints;
                float maxAp = armor.MaxArmorPoints;
                float ratio = maxAp > 0f ? ap / maxAp : 0f;

                Rect rect = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(rect, ratio, $"{ap:F1} / {maxAp:F1} AP");

                EditorGUILayout.LabelField("Has Armor", armor.HasArmor.ToString());

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Repair Full"))   armor.RepairFull();
                if (GUILayout.Button("Repair +25"))    armor.RepairArmor(25f);
                if (GUILayout.Button("Reset Max"))     armor.ResetMaxArmorPoints();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(8);
            }

            // === SETTINGS ===
            EditorGUILayout.PropertyField(_baseMaxArmorPointsProp, new GUIContent("Base Max Armor Points"));
            EditorGUILayout.PropertyField(_absorptionProp,         new GUIContent("Base Absorption (0-1)"));

            GUILayout.Space(4);

            // === RESISTANCE PAR TYPE (tableau nommé) ===
            _resistanceFoldout = EditorGUILayout.Foldout(_resistanceFoldout, "Resistances par type de dégât", true, EditorStyles.foldoutHeader);
            if (_resistanceFoldout)
            {
                // S'assurer que le tableau a la bonne taille
                if (_damageTypeResistanceProp.arraySize != _damageTypeNames.Length)
                    _damageTypeResistanceProp.arraySize = _damageTypeNames.Length;

                EditorGUI.indentLevel++;

                float baseAbsorption = _absorptionProp.floatValue;

                for (int i = 0; i < _damageTypeNames.Length; i++)
                {
                    SerializedProperty element = _damageTypeResistanceProp.GetArrayElementAtIndex(i);
                    float effectiveAbsorption = Mathf.Clamp01(baseAbsorption * element.floatValue);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(element, new GUIContent(_damageTypeNames[i]));
                    EditorGUILayout.LabelField($"→ {effectiveAbsorption * 100f:F0}% abs.", EditorStyles.miniLabel, GUILayout.Width(70));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;

                GUILayout.Space(4);
                if (GUILayout.Button("Reset toutes les résistances à 1.0"))
                {
                    for (int i = 0; i < _damageTypeResistanceProp.arraySize; i++)
                        _damageTypeResistanceProp.GetArrayElementAtIndex(i).floatValue = 1f;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
