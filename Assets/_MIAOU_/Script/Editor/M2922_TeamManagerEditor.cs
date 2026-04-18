using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Teams;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_TeamManager))]
    public class M2922_TeamManagerEditor : M2922_BaseEditor
    {
        // ── Team config ───────────────────────────────────────────────────────
        private SerializedProperty _teamCountProp;
        private SerializedProperty _teamNamesProp;
        private SerializedProperty _teamColorsProp;
        private SerializedProperty _teamIsActiveProp;

        // ── Game settings ─────────────────────────────────────────────────────
        private SerializedProperty _autoBalanceProp;
        private SerializedProperty _allowFriendlyFireProp;
        private SerializedProperty _friendlyFireDamageMultiplierProp;
        private SerializedProperty _neutralColorProp;

        // ── Runtime (synced) ──────────────────────────────────────────────────
        private SerializedProperty _teamScoresProp;
        private SerializedProperty _teamPlayerCountsProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _teamsOpen    = true;
        private bool _settingsOpen = true;
        private bool _runtimeOpen  = true;

        // ─────────────────────────────────────────────────────────────────────

        private static readonly string[] _defaultNames  = { "Red","Blue","Green","Yellow","Purple","Orange","Pink","Cyan","Lime","Magenta","Teal","Navy","Maroon","Olive","Silver","Gold" };
        private static readonly Color[]  _defaultColors =
        {
            new Color(1f,0.2f,0.2f), new Color(0.2f,0.4f,1f),   new Color(0.2f,0.8f,0.2f), new Color(1f,0.9f,0.2f),
            new Color(0.6f,0.2f,0.8f), new Color(1f,0.6f,0.2f), new Color(1f,0.4f,0.7f),   new Color(0.2f,0.8f,0.8f),
            new Color(0.6f,1f,0.2f),  new Color(0.8f,0.2f,0.6f),new Color(0.2f,0.6f,0.6f), new Color(0.1f,0.1f,0.5f),
            new Color(0.5f,0.1f,0.1f),new Color(0.5f,0.5f,0.1f),new Color(0.75f,0.75f,0.75f),new Color(1f,0.84f,0f)
        };

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _teamCountProp                    = serializedObject.FindProperty("_teamCount");
            _teamNamesProp                    = serializedObject.FindProperty("_teamNames");
            _teamColorsProp                   = serializedObject.FindProperty("_teamColors");
            _teamIsActiveProp                 = serializedObject.FindProperty("_teamIsActive");
            _autoBalanceProp                  = serializedObject.FindProperty("_autoBalance");
            _allowFriendlyFireProp            = serializedObject.FindProperty("_allowFriendlyFire");
            _friendlyFireDamageMultiplierProp = serializedObject.FindProperty("_friendlyFireDamageMultiplier");
            _neutralColorProp                 = serializedObject.FindProperty("_neutralColor");
            _teamScoresProp                   = serializedObject.FindProperty("_teamScores");
            _teamPlayerCountsProp             = serializedObject.FindProperty("_teamPlayerCounts");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    int count = _teamCountProp.intValue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Équipe", EditorStyles.miniLabel, GUILayout.Width(80));
                    EditorGUILayout.LabelField("Score",  EditorStyles.miniLabel, GUILayout.Width(50));
                    EditorGUILayout.LabelField("Joueurs",EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < count; i++)
                    {
                        Color teamColor = _teamColorsProp != null && _teamColorsProp.arraySize > i
                            ? _teamColorsProp.GetArrayElementAtIndex(i).colorValue : Color.white;
                        string name = _teamNamesProp != null && _teamNamesProp.arraySize > i
                            ? _teamNamesProp.GetArrayElementAtIndex(i).stringValue : $"Team {i}";
                        int score = _teamScoresProp != null && _teamScoresProp.arraySize > i
                            ? _teamScoresProp.GetArrayElementAtIndex(i).intValue : 0;
                        int players = _teamPlayerCountsProp != null && _teamPlayerCountsProp.arraySize > i
                            ? _teamPlayerCountsProp.GetArrayElementAtIndex(i).intValue : 0;

                        Color prev = GUI.color;
                        GUI.color = teamColor;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"■ {name}", GUILayout.Width(80));
                        GUI.color = prev;
                        EditorGUILayout.LabelField($"{score}", EditorStyles.boldLabel, GUILayout.Width(50));
                        EditorGUILayout.LabelField($"{players} joueur(s)", EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                });
            }

            // =================================================================
            // ÉQUIPES
            // =================================================================
            _teamsOpen = Section("ÉQUIPES", _teamsOpen, () =>
            {
                int teamCount = _teamCountProp.intValue;
                int newCount  = EditorGUILayout.IntSlider("Nombre d'équipes", teamCount, 2, 16);
                if (newCount != teamCount)
                {
                    _teamCountProp.intValue = newCount;
                    ResizeArrays(newCount);
                    teamCount = newCount;
                }

                GUILayout.Space(4);

                // En-têtes
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Act.", EditorStyles.miniLabel, GUILayout.Width(28));
                EditorGUILayout.LabelField("Couleur", EditorStyles.miniLabel, GUILayout.Width(52));
                EditorGUILayout.LabelField("Nom", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("ID", EditorStyles.miniLabel, GUILayout.Width(28));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < teamCount; i++)
                {
                    EnsureSize(_teamNamesProp,    teamCount);
                    EnsureSize(_teamColorsProp,   teamCount);
                    EnsureSize(_teamIsActiveProp, teamCount);

                    SerializedProperty activeProp = _teamIsActiveProp.GetArrayElementAtIndex(i);
                    SerializedProperty colorProp  = _teamColorsProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp   = _teamNamesProp.GetArrayElementAtIndex(i);

                    EditorGUILayout.BeginHorizontal();
                    activeProp.boolValue    = EditorGUILayout.Toggle(activeProp.boolValue, GUILayout.Width(28));
                    colorProp.colorValue    = EditorGUILayout.ColorField(GUIContent.none, colorProp.colorValue,
                                                false, false, false, GUILayout.Width(52));
                    nameProp.stringValue    = EditorGUILayout.TextField(nameProp.stringValue);
                    EditorGUILayout.LabelField($"[{i}]", EditorStyles.miniLabel, GUILayout.Width(28));
                    EditorGUILayout.EndHorizontal();
                }
            });

            // =================================================================
            // GAME SETTINGS
            // =================================================================
            _settingsOpen = Section("GAME SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_autoBalanceProp,
                    new GUIContent("Auto Balance", "Équilibre automatiquement le nombre de joueurs par équipe."));
                EditorGUILayout.PropertyField(_allowFriendlyFireProp,
                    new GUIContent("Allow Friendly Fire"));

                EditorGUI.BeginDisabledGroup(!_allowFriendlyFireProp.boolValue);
                EditorGUILayout.Slider(_friendlyFireDamageMultiplierProp, 0f, 1f,
                    new GUIContent("FF Damage Multiplier", "0 = aucun dégât allié / 1 = dégâts normaux."));
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.PropertyField(_neutralColorProp,
                    new GUIContent("Neutral Color", "Couleur pour les joueurs sans équipe."));
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private void ResizeArrays(int newCount)
        {
            int old = _teamNamesProp.arraySize;
            _teamNamesProp.arraySize    = newCount;
            _teamColorsProp.arraySize   = newCount;
            _teamIsActiveProp.arraySize = newCount;

            for (int i = old; i < newCount; i++)
            {
                _teamNamesProp.GetArrayElementAtIndex(i).stringValue  = i < _defaultNames.Length  ? _defaultNames[i]  : $"Team {i}";
                _teamColorsProp.GetArrayElementAtIndex(i).colorValue  = i < _defaultColors.Length ? _defaultColors[i] : Color.white;
                _teamIsActiveProp.GetArrayElementAtIndex(i).boolValue = true;
            }
        }

        private static void EnsureSize(SerializedProperty prop, int size)
        {
            if (prop.arraySize < size) prop.arraySize = size;
        }
    }
}
