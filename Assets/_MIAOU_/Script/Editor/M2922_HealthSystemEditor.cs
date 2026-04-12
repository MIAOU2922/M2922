using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_HealthSystem))]
    public class M2922_HealthSystemEditor : UnityEditor.Editor
    {
        private SerializedProperty _maxHealthProp;
        private SerializedProperty _canTakeDamageProp;

        private void OnEnable()
        {
            _maxHealthProp      = serializedObject.FindProperty("_maxHealth");
            _canTakeDamageProp  = serializedObject.FindProperty("_canTakeDamage");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            serializedObject.Update();

            // === STATUS (Play mode) ===
            M2922_HealthSystem hs = (M2922_HealthSystem)target;
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);

                float health    = hs.Health;
                float maxHealth = hs.MaxHealth;
                float ratio     = maxHealth > 0f ? health / maxHealth : 0f;

                Rect rect = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(rect, ratio, $"{health:F1} / {maxHealth:F1} HP");

                EditorGUILayout.LabelField("Is Alive",        hs.IsAlive.ToString());
                EditorGUILayout.LabelField("Can Take Damage", hs.CanTakeDamage.ToString());

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Heal Full"))    hs.Heal(maxHealth);
                if (GUILayout.Button("Kill"))         hs.Die(-1);
                if (GUILayout.Button("Deal 10 dmg")) hs.TakeDamage(10f, -1, DamageType.Generic);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(8);
            }

            // === SETTINGS ===
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
