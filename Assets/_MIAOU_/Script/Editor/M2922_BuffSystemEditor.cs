using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_BuffSystem))]
    public class M2922_BuffSystemEditor : UnityEditor.Editor
    {
        private SerializedProperty _presetTypesProp;
        private SerializedProperty _presetMagnitudesProp;
        private SerializedProperty _presetDurationsProp;
        private SerializedProperty _presetDamageTypesProp;

        // Ajout d'un buff preset via l'Editor
        private BuffType _newBuffType      = BuffType.MoveSpeed;
        private float    _newMagnitude     = 1f;
        private float    _newDuration      = -1f;

        private bool _presetFoldout = true;
        private bool _runtimeFoldout = true;

        private void OnEnable()
        {
            _presetTypesProp       = serializedObject.FindProperty("_presetTypes");
            _presetMagnitudesProp  = serializedObject.FindProperty("_presetMagnitudes");
            _presetDurationsProp   = serializedObject.FindProperty("_presetDurations");
            _presetDamageTypesProp = serializedObject.FindProperty("_presetDamageTypes");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            serializedObject.Update();

            M2922_BuffSystem buffSystem = (M2922_BuffSystem)target;

            // === STATUS (Play mode) ===
            if (Application.isPlaying)
            {
                _runtimeFoldout = EditorGUILayout.Foldout(_runtimeFoldout, "RUNTIME STATUS", true, EditorStyles.foldoutHeader);
                if (_runtimeFoldout)
                {
                    EditorGUILayout.BeginVertical("box");

                    EditorGUILayout.LabelField("Has Active Buff", buffSystem.HasActiveBuff.ToString());

                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    _newBuffType  = (BuffType)EditorGUILayout.EnumPopup(_newBuffType);
                    _newMagnitude = EditorGUILayout.FloatField(_newMagnitude, GUILayout.Width(50));
                    _newDuration  = EditorGUILayout.FloatField(_newDuration,  GUILayout.Width(50));
                    if (GUILayout.Button("Apply", GUILayout.Width(55)))
                        buffSystem.ApplyBuff(_newBuffType, _newMagnitude, _newDuration);
                    if (GUILayout.Button("Remove", GUILayout.Width(55)))
                        buffSystem.RemoveBuff(_newBuffType);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("", "Type | Magnitude | Duration(-1=perm)", EditorStyles.miniLabel);

                    GUILayout.Space(4);
                    if (GUILayout.Button("Clear All Buffs"))
                        buffSystem.ClearAllBuffs();

                    EditorGUILayout.EndVertical();
                }
                GUILayout.Space(8);
            }

            // === PRESET BUFFS ===
            _presetFoldout = EditorGUILayout.Foldout(_presetFoldout, "PRESET BUFFS", true, EditorStyles.foldoutHeader);
            if (_presetFoldout)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.HelpBox(
                    "Ces buffs sont appliqués automatiquement au Start().\n" +
                    "Duration = -1 → permanent.",
                    MessageType.Info
                );
                GUILayout.Space(4);

                // Largeurs fixes — doivent correspondre exactement entre header et lignes
                const float W_TYPE  = 105f;
                const float W_MAG   = 45f;
                const float W_UNIT  = 48f;
                const float W_DUR   = 38f;
                const float W_DURLB = 38f;
                const float W_DMG   = 76f;
                const float W_BTN   = 22f;

                // En-têtes des colonnes
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type",      EditorStyles.miniLabel, GUILayout.Width(W_TYPE));
                EditorGUILayout.LabelField("Magnitude", EditorStyles.miniLabel, GUILayout.Width(W_MAG));
                EditorGUILayout.LabelField("Unité",     EditorStyles.miniLabel, GUILayout.Width(W_UNIT));
                EditorGUILayout.LabelField("Durée (s)", EditorStyles.miniLabel, GUILayout.Width(W_DUR));
                EditorGUILayout.LabelField("",          EditorStyles.miniLabel, GUILayout.Width(W_DURLB));
                EditorGUILayout.LabelField("DmgType",   EditorStyles.miniLabel, GUILayout.Width(W_DMG));
                EditorGUILayout.EndHorizontal();

                // Synchroniser les tailles des tableaux
                int count = _presetTypesProp.arraySize;

                for (int i = 0; i < count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    SerializedProperty typeProp      = _presetTypesProp.GetArrayElementAtIndex(i);
                    SerializedProperty magnitudeProp = _presetMagnitudesProp.arraySize > i ? _presetMagnitudesProp.GetArrayElementAtIndex(i) : null;
                    SerializedProperty durationProp  = _presetDurationsProp.arraySize  > i ? _presetDurationsProp.GetArrayElementAtIndex(i)  : null;
                    SerializedProperty dmgTypeProp   = _presetDamageTypesProp != null && _presetDamageTypesProp.arraySize > i ? _presetDamageTypesProp.GetArrayElementAtIndex(i) : null;

                    BuffType buffType = (BuffType)typeProp.enumValueIndex;
                    bool isPassiveDamage = buffType == BuffType.PassiveDamage;

                    EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(W_TYPE));

                    if (magnitudeProp != null)
                    {
                        EditorGUILayout.PropertyField(magnitudeProp, GUIContent.none, GUILayout.Width(W_MAG));
                        EditorGUILayout.LabelField(GetMagnitudeUnit(buffType), EditorStyles.miniLabel, GUILayout.Width(W_UNIT));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("", GUILayout.Width(W_MAG + W_UNIT));
                    }

                    if (durationProp != null)
                    {
                        EditorGUILayout.PropertyField(durationProp, GUIContent.none, GUILayout.Width(W_DUR));
                        string durLabel = durationProp.floatValue < 0f ? "perm" : $"{durationProp.floatValue:F0}s";
                        EditorGUILayout.LabelField(durLabel, EditorStyles.miniLabel, GUILayout.Width(W_DURLB));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("", GUILayout.Width(W_DUR + W_DURLB));
                    }

                    if (dmgTypeProp != null && isPassiveDamage)
                        EditorGUILayout.PropertyField(dmgTypeProp, GUIContent.none, GUILayout.Width(W_DMG));
                    else
                        EditorGUILayout.LabelField("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(W_DMG));

                    if (GUILayout.Button("✕", GUILayout.Width(W_BTN)))
                    {
                        _presetTypesProp.DeleteArrayElementAtIndex(i);
                        if (_presetMagnitudesProp.arraySize  > i) _presetMagnitudesProp.DeleteArrayElementAtIndex(i);
                        if (_presetDurationsProp.arraySize   > i) _presetDurationsProp.DeleteArrayElementAtIndex(i);
                        if (_presetDamageTypesProp != null && _presetDamageTypesProp.arraySize > i) _presetDamageTypesProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(4);
                if (GUILayout.Button("+ Ajouter un buff preset"))
                {
                    _presetTypesProp.arraySize++;
                    _presetMagnitudesProp.arraySize  = _presetTypesProp.arraySize;
                    _presetDurationsProp.arraySize   = _presetTypesProp.arraySize;
                    _presetDamageTypesProp.arraySize = _presetTypesProp.arraySize;

                    int last = _presetTypesProp.arraySize - 1;
                    _presetTypesProp.GetArrayElementAtIndex(last).enumValueIndex       = 0;
                    _presetMagnitudesProp.GetArrayElementAtIndex(last).floatValue      = 1f;
                    _presetDurationsProp.GetArrayElementAtIndex(last).floatValue       = -1f;
                    _presetDamageTypesProp.GetArrayElementAtIndex(last).enumValueIndex = 0;
                }

                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string GetMagnitudeUnit(BuffType buffType)
        {
            switch (buffType)
            {
                case BuffType.HealthRegen:   return "HP/s";
                case BuffType.ArmorRegen:    return "AP/s";
                case BuffType.PassiveDamage: return "DMG/s";
                case BuffType.MoveSpeed:     return "x spd";
                case BuffType.JumpHeight:    return "x jump";
                case BuffType.MaxHealth:     return "x HPmax";
                case BuffType.MaxArmor:      return "x APmax";
                default:                     return "";
            }
        }
    }
}
