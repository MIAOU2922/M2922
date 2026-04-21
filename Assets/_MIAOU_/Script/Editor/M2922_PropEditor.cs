using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Entity.Prop;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_Prop))]
    public class M2922_PropEditor : M2922_BaseEditor
    {
        // ── Entity ──────────────────────────────────────────────────────────
        private SerializedProperty _entityIdProp;
        private SerializedProperty _entityNameProp;
        private SerializedProperty _entityTypeProp;

        // ── Capabilities ─────────────────────────────────────────────────────
        private SerializedProperty _isGrabbableProp;
        private SerializedProperty _isDamageableProp;

        // ── Systems ──────────────────────────────────────────────────────────
        private SerializedProperty _healthSystemProp;
        private SerializedProperty _buffSystemProp;

        // ── Grab config ───────────────────────────────────────────────────────
        private SerializedProperty _returnDelayProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _runtimeOpen      = true;
        private bool _entityOpen       = true;
        private bool _capabilitiesOpen = true;
        private bool _grabOpen         = true;
        private bool _damageOpen       = true;
        private bool _buffsOpen        = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            _entityIdProp      = serializedObject.FindProperty("_entityId");
            _entityNameProp    = serializedObject.FindProperty("_entityName");
            _entityTypeProp    = serializedObject.FindProperty("_entityType");
            _isGrabbableProp   = serializedObject.FindProperty("_isGrabbable");
            _isDamageableProp  = serializedObject.FindProperty("_isDamageable");
            _healthSystemProp  = serializedObject.FindProperty("HealthSystem");
            _buffSystemProp    = serializedObject.FindProperty("BuffSystem");
            _returnDelayProp   = serializedObject.FindProperty("_returnDelay");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_Prop prop = (M2922_Prop)target;

            bool isGrabbable  = _isGrabbableProp.boolValue;
            bool isDamageable = _isDamageableProp.boolValue;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    // Held state
                    if (isGrabbable)
                    {
                        Color prev = GUI.contentColor;
                        GUI.contentColor = prop.IsHeld ? Color.cyan : Color.gray;
                        EditorGUILayout.LabelField(
                            prop.IsHeld ? "● Tenu par un joueur" : "○ Posé",
                            EditorStyles.boldLabel);
                        GUI.contentColor = prev;
                    }

                    // Health bar
                    if (isDamageable)
                    {
                        GUILayout.Space(2);
                        if (prop.HasHealth)
                        {
                            float ratio = prop.MaxHealth > 0f ? prop.Health / prop.MaxHealth : 0f;
                            Color barColor = ratio > 0.5f ? Color.green
                                           : ratio > 0.25f ? Color.yellow : Color.red;
                            Color prevC = GUI.color;
                            GUI.color   = barColor;
                            Rect rect   = EditorGUILayout.GetControlRect(false, 18f);
                            EditorGUI.ProgressBar(rect, ratio,
                                $"{prop.Health:F1} / {prop.MaxHealth:F1} HP");
                            GUI.color = prevC;
                            EditorGUILayout.LabelField(
                                prop.IsAlive ? "Vivant" : "Détruit",
                                EditorStyles.miniLabel);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                "isDamageable activé mais pas de HealthSystem !",
                                MessageType.Warning);
                        }
                    }

                    if (!isGrabbable && !isDamageable)
                        EditorGUILayout.LabelField("Prop statique", EditorStyles.miniLabel);
                });
            }

            // =================================================================
            // ENTITY SETTINGS
            // =================================================================
            _entityOpen = Section("ENTITY SETTINGS", _entityOpen, () =>
            {
                EditorGUILayout.PropertyField(_entityIdProp,   new GUIContent("Entity ID"));
                EditorGUILayout.PropertyField(_entityNameProp, new GUIContent("Entity Name"));
                EditorGUILayout.PropertyField(_entityTypeProp, new GUIContent("Entity Type"));
            });

            // =================================================================
            // CAPABILITIES
            // =================================================================
            _capabilitiesOpen = Section("CAPABILITIES", _capabilitiesOpen, () =>
            {
                EditorGUILayout.PropertyField(_isGrabbableProp,
                    new GUIContent("Grabbable",
                        "Ce prop peut être ramassé/lâché.\nNécessite un VRC_Pickup sur ce GameObject."));
                EditorGUILayout.PropertyField(_isDamageableProp,
                    new GUIContent("Damageable",
                        "Ce prop peut recevoir des dégâts.\nNécessite un M2922_HealthSystem assigné."));

                // Warnings config manquante
                if (_isGrabbableProp.boolValue && prop.GetComponent<VRC.SDKBase.VRC_Pickup>() == null)
                    EditorGUILayout.HelpBox(
                        "Grabbable activé mais aucun VRC_Pickup sur ce GameObject.",
                        MessageType.Error);

                if (_isDamageableProp.boolValue && _healthSystemProp.objectReferenceValue == null)
                    EditorGUILayout.HelpBox(
                        "Damageable activé mais aucun HealthSystem assigné.",
                        MessageType.Error);
            });

            // =================================================================
            // GRAB CONFIG (uniquement si grabbable)
            // =================================================================
            if (isGrabbable)
            {
                _grabOpen = Section("GRAB CONFIG", _grabOpen, () =>
                {
                    EditorGUILayout.PropertyField(_returnDelayProp,
                        new GUIContent("Return Delay (s)",
                            "Délai avant retour à l'origine après avoir été lâché.\n" +
                            "-1 = reste là où il est posé."));

                    float delay = _returnDelayProp.floatValue;
                    if (delay < 0f)
                        EditorGUILayout.LabelField("→ Permanent (ne retourne pas)",
                            EditorStyles.miniLabel);
                    else
                        EditorGUILayout.LabelField($"→ Retour après {delay:F1}s",
                            EditorStyles.miniLabel);
                });
            }

            // =================================================================
            // DAMAGE CONFIG (uniquement si damageable)
            // =================================================================
            if (isDamageable)
            {
                _damageOpen = Section("DAMAGE CONFIG", _damageOpen, () =>
                {
                    EditorGUILayout.PropertyField(_healthSystemProp,
                        new GUIContent("Health System"));

                    if (_healthSystemProp.objectReferenceValue == null)
                        EditorGUILayout.LabelField(
                            "→ Assigne un HealthSystem pour activer les dégâts.",
                            EditorStyles.miniLabel);
                });
            }

            // =================================================================
            // BUFFS (toujours optionnel)
            // =================================================================
            _buffsOpen = Section("BUFFS (optionnel)", _buffsOpen, () =>
            {
                EditorGUILayout.PropertyField(_buffSystemProp,
                    new GUIContent("Buff System"));
                if (_buffSystemProp.objectReferenceValue == null)
                    EditorGUILayout.LabelField("→ Pas de buffs",
                        EditorStyles.miniLabel);
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
