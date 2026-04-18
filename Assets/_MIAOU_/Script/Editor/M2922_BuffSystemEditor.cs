using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_BuffSystem))]
    public class M2922_BuffSystemEditor : M2922_BaseEditor
    {
        // ── Preset arrays ─────────────────────────────────────────────────────
        private SerializedProperty _presetTypesProp;
        private SerializedProperty _presetMagnitudesProp;
        private SerializedProperty _presetDurationsProp;
        private SerializedProperty _presetDamageTypesProp;

        // ── Runtime test state ────────────────────────────────────────────────
        private BuffType _testBuffType  = BuffType.MoveSpeed;
        private float    _testMagnitude = 1f;
        private float    _testDuration  = -1f;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _presetsOpen = true;
        private bool _runtimeOpen = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
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

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    Color prevC = GUI.contentColor;
                    GUI.contentColor = buffSystem.HasActiveBuff ? Color.green : Color.gray;
                    EditorGUILayout.LabelField(
                        buffSystem.HasActiveBuff ? "\u25cf Buff(s) actif(s)" : "\u25cb Aucun buff actif",
                        EditorStyles.boldLabel);
                    GUI.contentColor = prevC;

                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Appliquer un buff test", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    _testBuffType  = (BuffType)EditorGUILayout.EnumPopup(_testBuffType);
                    EditorGUILayout.LabelField(GetMagnitudeUnit(_testBuffType),
                        EditorStyles.miniLabel, GUILayout.Width(48));
                    _testMagnitude = EditorGUILayout.FloatField(_testMagnitude, GUILayout.Width(50));
                    EditorGUILayout.LabelField("dur(s)", EditorStyles.miniLabel, GUILayout.Width(38));
                    _testDuration  = EditorGUILayout.FloatField(_testDuration,  GUILayout.Width(50));
                    EditorGUILayout.LabelField(
                        _testDuration < 0f ? "perm" : $"{_testDuration:F0}s",
                        EditorStyles.miniLabel, GUILayout.Width(30));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Apply"))
                        buffSystem.ApplyBuff(_testBuffType, _testMagnitude, _testDuration);
                    if (GUILayout.Button("Remove"))
                        buffSystem.RemoveBuff(_testBuffType);
                    if (GUILayout.Button("Clear All"))
                        buffSystem.ClearAllBuffs();
                    EditorGUILayout.EndHorizontal();
                });
            }

            // =================================================================
            // PRESET BUFFS
            // =================================================================
            _presetsOpen = Section("PRESET BUFFS", _presetsOpen, () =>
            {
                EditorGUILayout.HelpBox(
                    "Ces buffs sont appliqu\u00e9s automatiquement au Start().\n" +
                    "Duration = -1 \u2192 permanent.",
                    MessageType.None);
                GUILayout.Space(4);

                const float W_TYPE  = 105f;
                const float W_MAG   = 45f;
                const float W_UNIT  = 48f;
                const float W_DUR   = 40f;
                const float W_DURLB = 36f;
                const float W_DMG   = 76f;
                const float W_BTN   = 22f;

                // En-t\u00eates
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type",       EditorStyles.miniLabel, GUILayout.Width(W_TYPE));
                EditorGUILayout.LabelField("Magnitude",  EditorStyles.miniLabel, GUILayout.Width(W_MAG));
                EditorGUILayout.LabelField("Unit\u00e9",      EditorStyles.miniLabel, GUILayout.Width(W_UNIT));
                EditorGUILayout.LabelField("Dur\u00e9e (s)",  EditorStyles.miniLabel, GUILayout.Width(W_DUR));
                EditorGUILayout.LabelField("",           EditorStyles.miniLabel, GUILayout.Width(W_DURLB));
                EditorGUILayout.LabelField("DmgType",    EditorStyles.miniLabel, GUILayout.Width(W_DMG));
                EditorGUILayout.EndHorizontal();

                int count = _presetTypesProp.arraySize;
                for (int i = 0; i < count; i++)
                {
                    SerializedProperty typeProp      = _presetTypesProp.GetArrayElementAtIndex(i);
                    SerializedProperty magnitudeProp = _presetMagnitudesProp.arraySize > i ? _presetMagnitudesProp.GetArrayElementAtIndex(i) : null;
                    SerializedProperty durationProp  = _presetDurationsProp.arraySize  > i ? _presetDurationsProp.GetArrayElementAtIndex(i)  : null;
                    SerializedProperty dmgTypeProp   = _presetDamageTypesProp != null && _presetDamageTypesProp.arraySize > i
                        ? _presetDamageTypesProp.GetArrayElementAtIndex(i) : null;

                    BuffType buffType     = (BuffType)typeProp.enumValueIndex;
                    bool     isPassiveDmg = buffType == BuffType.PassiveDamage;

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(W_TYPE));

                    if (magnitudeProp != null)
                    {
                        EditorGUILayout.PropertyField(magnitudeProp, GUIContent.none, GUILayout.Width(W_MAG));
                        EditorGUILayout.LabelField(GetMagnitudeUnit(buffType), EditorStyles.miniLabel, GUILayout.Width(W_UNIT));
                    }
                    else { EditorGUILayout.LabelField("", GUILayout.Width(W_MAG + W_UNIT)); }

                    if (durationProp != null)
                    {
                        EditorGUILayout.PropertyField(durationProp, GUIContent.none, GUILayout.Width(W_DUR));
                        string durLabel = durationProp.floatValue < 0f ? "perm" : $"{durationProp.floatValue:F0}s";
                        EditorGUILayout.LabelField(durLabel, EditorStyles.miniLabel, GUILayout.Width(W_DURLB));
                    }
                    else { EditorGUILayout.LabelField("", GUILayout.Width(W_DUR + W_DURLB)); }

                    if (dmgTypeProp != null && isPassiveDmg)
                        EditorGUILayout.PropertyField(dmgTypeProp, GUIContent.none, GUILayout.Width(W_DMG));
                    else
                        EditorGUILayout.LabelField("\u2014", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(W_DMG));

                    if (GUILayout.Button("\u2715", GUILayout.Width(W_BTN)))
                    {
                        _presetTypesProp.DeleteArrayElementAtIndex(i);
                        if (_presetMagnitudesProp.arraySize  > i) _presetMagnitudesProp.DeleteArrayElementAtIndex(i);
                        if (_presetDurationsProp.arraySize   > i) _presetDurationsProp.DeleteArrayElementAtIndex(i);
                        if (_presetDamageTypesProp != null && _presetDamageTypesProp.arraySize > i)
                            _presetDamageTypesProp.DeleteArrayElementAtIndex(i);
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
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static string GetMagnitudeUnit(BuffType buffType)
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
