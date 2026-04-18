using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_Projectile))]
    public class M2922_ProjectileEditor : M2922_BaseEditor
    {
        // ── Damage ────────────────────────────────────────────────────────────
        private SerializedProperty _damageTypesProp;
        private SerializedProperty _baseDamageAmountsProp;
        private SerializedProperty _damageMultiplierProp;
        private SerializedProperty _critMultiplierProp;

        // ── Settings ──────────────────────────────────────────────────────────
        private SerializedProperty _lifetimeProp;
        private SerializedProperty _explosionRadiusProp;
        private SerializedProperty _hitLayersProp;

        // ── VFX / SFX ─────────────────────────────────────────────────────────
        private SerializedProperty _trailEffectProp;
        private SerializedProperty _impactEffectProp;
        private SerializedProperty _impactAudioProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _damageOpen   = true;
        private bool _settingsOpen = true;
        private bool _vfxOpen      = false;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _damageTypesProp       = serializedObject.FindProperty("_damageTypes");
            _baseDamageAmountsProp = serializedObject.FindProperty("_baseDamageAmounts");
            _damageMultiplierProp  = serializedObject.FindProperty("_damageMultiplier");
            _critMultiplierProp    = serializedObject.FindProperty("_critMultiplier");
            _lifetimeProp          = serializedObject.FindProperty("_lifetime");
            _explosionRadiusProp   = serializedObject.FindProperty("_explosionRadius");
            _hitLayersProp         = serializedObject.FindProperty("_hitLayers");
            _trailEffectProp       = serializedObject.FindProperty("_trailEffect");
            _impactEffectProp      = serializedObject.FindProperty("_impactEffect");
            _impactAudioProp       = serializedObject.FindProperty("_impactAudio");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_Projectile proj = (M2922_Projectile)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);

                // Active indicator
                EditorGUILayout.BeginHorizontal();
                Color prevColor = GUI.contentColor;
                GUI.contentColor = proj.IsActive ? Color.green : Color.gray;
                EditorGUILayout.LabelField(proj.IsActive ? "● ACTIF" : "○ inactif", EditorStyles.boldLabel);
                GUI.contentColor = prevColor;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Lancer (standalone)"))
                    proj.InitializeStandalone(proj.transform.position, proj.transform.forward, 10f, -1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(8);
            }

            // Avertissement pool : GO doit être inactif à la base
            if (!Application.isPlaying && proj.gameObject.activeSelf && proj.transform.parent != null)
            {
                EditorGUILayout.HelpBox(
                    "Les projectiles de pool doivent être desactivés par défaut (GameObject.SetActive(false)).",
                    MessageType.Warning);
            }

            // =================================================================
            // DAMAGE
            // =================================================================
            _damageOpen = Section("DAMAGE", _damageOpen, () =>
            {
                int typeCount = _damageTypesProp.arraySize;
                int amtCount  = _baseDamageAmountsProp.arraySize;
                int rows      = Mathf.Max(typeCount, amtCount);

                if (typeCount != amtCount)
                    EditorGUILayout.HelpBox(
                        "_damageTypes et _baseDamageAmounts doivent avoir la même longueur !",
                        MessageType.Warning);

                int newRows = Mathf.Max(0, EditorGUILayout.IntField("Entries", rows));
                if (newRows != rows)
                {
                    _damageTypesProp.arraySize       = newRows;
                    _baseDamageAmountsProp.arraySize  = newRows;
                }

                for (int i = 0; i < newRows; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(
                        _damageTypesProp.GetArrayElementAtIndex(i),
                        GUIContent.none, GUILayout.Width(100));
                    EditorGUILayout.PropertyField(
                        _baseDamageAmountsProp.GetArrayElementAtIndex(i),
                        GUIContent.none);
                    EditorGUILayout.LabelField("dmg", EditorStyles.miniLabel, GUILayout.Width(28));
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(4);
                EditorGUILayout.PropertyField(_damageMultiplierProp, new GUIContent("Damage Multiplier"));
                EditorGUILayout.PropertyField(_critMultiplierProp,   new GUIContent("Crit Multiplier (tag 'Crit')"));

                bool hasExplosion = _explosionRadiusProp != null && _explosionRadiusProp.floatValue > 0f;
                if (hasExplosion)
                    EditorGUILayout.HelpBox(
                        "Explosion : le crit n'est pas applicable (pas de tag dans OverlapSphere).",
                        MessageType.Info);
            });

            // =================================================================
            // SETTINGS
            // =================================================================
            _settingsOpen = Section("PROJECTILE SETTINGS", _settingsOpen, () =>
            {
                EditorGUILayout.PropertyField(_lifetimeProp,         new GUIContent("Lifetime (s)"));
                EditorGUILayout.PropertyField(_explosionRadiusProp,  new GUIContent("Explosion Radius (m, 0 = ponctuel)"));
                EditorGUILayout.PropertyField(_hitLayersProp,        new GUIContent("Hit Layers"));

                float radius = _explosionRadiusProp.floatValue;
                if (radius > 0f)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        $"Explosion AoE : rayon {radius:F2}m.\n" +
                        "Cibles via Physics.OverlapSphere, falloff linéaire centre→bord.",
                        MessageType.None);
                }
            });

            // =================================================================
            // VFX / SFX
            // =================================================================
            _vfxOpen = Section("VFX / SFX", _vfxOpen, () =>
            {
                EditorGUILayout.PropertyField(_trailEffectProp,  new GUIContent("Trail Effect"));
                EditorGUILayout.PropertyField(_impactEffectProp, new GUIContent("Impact Effect"));
                EditorGUILayout.PropertyField(_impactAudioProp,  new GUIContent("Impact Sound"));
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // SCENE GUI — explosion radius disc
        // =====================================================================

        private void OnSceneGUI()
        {
            M2922_Projectile proj = (M2922_Projectile)target;
            float radius = _explosionRadiusProp != null ? _explosionRadiusProp.floatValue : 0f;
            if (radius <= 0f) return;

            Handles.color = new Color(1f, 0.4f, 0.1f, 0.3f);
            Handles.DrawSolidDisc(proj.transform.position, Vector3.up, radius);
            Handles.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Handles.DrawWireDisc(proj.transform.position, Vector3.up,    radius);
            Handles.DrawWireDisc(proj.transform.position, Vector3.right,  radius);
            Handles.DrawWireDisc(proj.transform.position, Vector3.forward, radius);

            Handles.Label(
                proj.transform.position + Vector3.up * (radius + 0.1f),
                $"Explosion {radius:F1}m");
        }

    }
}
