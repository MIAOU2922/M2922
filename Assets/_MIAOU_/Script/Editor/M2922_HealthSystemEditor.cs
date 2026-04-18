using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_HealthSystem))]
    public class M2922_HealthSystemEditor : M2922_BaseEditor
    {
        // ── Settings ──────────────────────────────────────────────────────────
        private SerializedProperty _baseMaxHealthProp;
        private SerializedProperty _canTakeDamageProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _settingsOpen = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _baseMaxHealthProp = serializedObject.FindProperty("_baseMaxHealth");
            _canTakeDamageProp = serializedObject.FindProperty("_canTakeDamage");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_HealthSystem hs = (M2922_HealthSystem)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);

                float health    = hs.Health;
                float maxHealth = hs.MaxHealth;
                float ratio     = maxHealth > 0f ? health / maxHealth : 0f;

                Color barColor = ratio > 0.5f ? Color.green
                               : ratio > 0.25f ? Color.yellow
                               : Color.red;
                Color prev = GUI.color;
                GUI.color  = barColor;
                Rect rect  = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(rect, ratio, $"{health:F1} / {maxHealth:F1} HP");
                GUI.color  = prev;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Is Alive",        hs.IsAlive.ToString(),       EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Can Take Damage", hs.CanTakeDamage.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Soin complet")) hs.Heal(maxHealth);
                if (GUILayout.Button("Kill"))         hs.Die(-1);
                if (GUILayout.Button("-10 HP"))       hs.TakeDamage(10f, -1, DamageType.Generic);
                if (GUILayout.Button("-50 HP"))       hs.TakeDamage(50f, -1, DamageType.Generic);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset Max HP")) hs.ResetMaxHealth();
                if (GUILayout.Button("Max HP ×1.5")) hs.SetMaxHealth(maxHealth * 1.5f);
                if (GUILayout.Button("Max HP ×0.5")) hs.SetMaxHealth(maxHealth * 0.5f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }

            // =================================================================
            // SETTINGS
            // =================================================================
            _settingsOpen = Section("HEALTH SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_baseMaxHealthProp, new GUIContent("Base Max Health",
                    "Points de vie maximum de base. Modifiable en runtime via SetMaxHealth()."));

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _canTakeDamageProp.boolValue
                    ? new Color(0.4f, 1f, 0.4f)
                    : new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.PropertyField(_canTakeDamageProp, new GUIContent("Can Take Damage",
                    "Si décoché, toutes les sources de dégâts sont ignorées (invincible)."));
                GUI.backgroundColor = prevBg;

                if (!_canTakeDamageProp.boolValue)
                    EditorGUILayout.HelpBox("Ce système est invincible : aucun dégât ne sera appliqué.", MessageType.Info);

                if (Application.isPlaying)
                    EditorGUILayout.LabelField(
                        $"→ Health effective : {hs.Health:F1} / {hs.MaxHealth:F1}", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(
                        $"→ Spawne avec {_baseMaxHealthProp.floatValue:F0} HP (plein)", EditorStyles.miniLabel);
            });

            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static bool Section(string title, bool open, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (open)
            {
                GUILayout.Space(2);
                content();
                GUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
            return open;
        }
    }
}
