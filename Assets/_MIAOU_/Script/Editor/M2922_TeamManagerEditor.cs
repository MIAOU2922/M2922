using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922.Teams.M2922_TeamManager))]
    public class M2922_TeamManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _teamCountProp;
        private SerializedProperty _teamNamesProp;
        private SerializedProperty _teamColorsProp;
        private SerializedProperty _teamIsActiveProp;
        private SerializedProperty _autoBalanceProp;
        private SerializedProperty _allowFriendlyFireProp;
        private SerializedProperty _friendlyFireDamageMultiplierProp;
        private SerializedProperty _neutralColorProp;
        private SerializedProperty _teamScoresProp;
        private SerializedProperty _teamPlayerCountsProp;
        private SerializedProperty _managerProp;
        private SerializedProperty _debugProp;
        private SerializedProperty _verboseDebugProp;

        private void OnEnable()
        {
            _teamCountProp                  = serializedObject.FindProperty("_teamCount");
            _teamNamesProp                  = serializedObject.FindProperty("_teamNames");
            _teamColorsProp                 = serializedObject.FindProperty("_teamColors");
            _teamIsActiveProp               = serializedObject.FindProperty("_teamIsActive");
            _autoBalanceProp                = serializedObject.FindProperty("_autoBalance");
            _allowFriendlyFireProp          = serializedObject.FindProperty("_allowFriendlyFire");
            _friendlyFireDamageMultiplierProp = serializedObject.FindProperty("_friendlyFireDamageMultiplier");
            _neutralColorProp               = serializedObject.FindProperty("_neutralColor");
            _teamScoresProp                 = serializedObject.FindProperty("_teamScores");
            _teamPlayerCountsProp           = serializedObject.FindProperty("_teamPlayerCounts");
            _managerProp                    = serializedObject.FindProperty("Manager");
            _debugProp                      = serializedObject.FindProperty("DEBUG");
            _verboseDebugProp               = serializedObject.FindProperty("VERBOSE_DEBUG");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            
            serializedObject.Update();

            // === DEBUG ===
            EditorGUILayout.LabelField("=== GLOBAL DEBUG ===", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_debugProp, new GUIContent("DEBUG"));
            EditorGUILayout.PropertyField(_verboseDebugProp, new GUIContent("VERBOSE_DEBUG"));
            GUILayout.Space(6);

            // === MANAGER ===
            EditorGUILayout.LabelField("=== MANAGER REFERENCE ===", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_managerProp, new GUIContent("Manager"));
            GUILayout.Space(6);

            // === TEAM CONFIGURATION ===
            EditorGUILayout.LabelField("=== TEAM CONFIGURATION ===", EditorStyles.boldLabel);
            
            int teamCount = _teamCountProp.intValue;
            int newCount = EditorGUILayout.IntSlider("Team Count", teamCount, 2, 16);
            if (newCount != teamCount)
            {
                _teamCountProp.intValue = newCount;
                ResizeArrays(newCount);
                teamCount = newCount;
            }

            GUILayout.Space(6);

            // Unified team list
            for (int i = 0; i < teamCount; i++)
            {
                EnsureArraySize(_teamNamesProp, teamCount, SerializedPropertyType.String);
                EnsureArraySize(_teamColorsProp, teamCount, SerializedPropertyType.Color);
                EnsureArraySize(_teamIsActiveProp, teamCount, SerializedPropertyType.Boolean);

                EditorGUILayout.BeginHorizontal();

                // Active toggle
                SerializedProperty activeProp = _teamIsActiveProp.GetArrayElementAtIndex(i);
                activeProp.boolValue = EditorGUILayout.Toggle(activeProp.boolValue, GUILayout.Width(18));

                // Color swatch
                SerializedProperty colorProp = _teamColorsProp.GetArrayElementAtIndex(i);
                colorProp.colorValue = EditorGUILayout.ColorField(GUIContent.none, colorProp.colorValue, false, false, false, GUILayout.Width(44));

                // Name field
                SerializedProperty nameProp = _teamNamesProp.GetArrayElementAtIndex(i);
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

                EditorGUILayout.LabelField($"[{i}]", EditorStyles.miniLabel, GUILayout.Width(28));

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // === GAME SETTINGS ===
            EditorGUILayout.LabelField("=== GAME SETTINGS ===", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoBalanceProp, new GUIContent("Auto Balance"));
            EditorGUILayout.PropertyField(_allowFriendlyFireProp, new GUIContent("Allow Friendly Fire"));
            if (_allowFriendlyFireProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Slider(_friendlyFireDamageMultiplierProp, 0f, 1f, new GUIContent("FF Damage Multiplier"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_neutralColorProp, new GUIContent("Neutral Color"));

            serializedObject.ApplyModifiedProperties();
        }

        private void ResizeArrays(int newCount)
        {
            // Default names
            string[] defaults = { "Red","Blue","Green","Yellow","Purple","Orange","Pink","Cyan",
                                   "Lime","Magenta","Teal","Navy","Maroon","Olive","Silver","Gold" };
            Color[] colors = {
                new Color(1f,0.2f,0.2f), new Color(0.2f,0.4f,1f),  new Color(0.2f,0.8f,0.2f), new Color(1f,0.9f,0.2f),
                new Color(0.6f,0.2f,0.8f), new Color(1f,0.6f,0.2f), new Color(1f,0.4f,0.7f),  new Color(0.2f,0.8f,0.8f),
                new Color(0.6f,1f,0.2f),  new Color(0.8f,0.2f,0.6f),new Color(0.2f,0.6f,0.6f),new Color(0.1f,0.1f,0.5f),
                new Color(0.5f,0.1f,0.1f),new Color(0.5f,0.5f,0.1f),new Color(0.75f,0.75f,0.75f),new Color(1f,0.84f,0f)
            };

            int oldCount = _teamNamesProp.arraySize;

            _teamNamesProp.arraySize    = newCount;
            _teamColorsProp.arraySize   = newCount;
            _teamIsActiveProp.arraySize = newCount;

            for (int i = oldCount; i < newCount; i++)
            {
                _teamNamesProp.GetArrayElementAtIndex(i).stringValue   = i < defaults.Length ? defaults[i] : $"Team {i}";
                _teamColorsProp.GetArrayElementAtIndex(i).colorValue   = i < colors.Length ? colors[i] : Color.white;
                _teamIsActiveProp.GetArrayElementAtIndex(i).boolValue  = true;
            }
        }

        private void EnsureArraySize(SerializedProperty prop, int size, SerializedPropertyType type)
        {
            if (prop.arraySize < size)
                prop.arraySize = size;
        }
    }
}
