using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Entity.Player;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_PlayerController))]
    public class M2922_PlayerControllerEditor : M2922_BaseEditor
    {
        // ── Entity props ───────────────────────────────────────────────────────
        private SerializedProperty _entityIdProp;
        private SerializedProperty _entityNameProp;
        private SerializedProperty _entityTypeProp;

        // ── System refs ───────────────────────────────────────────────────────
        private SerializedProperty _healthSystemProp;
        private SerializedProperty _armorSystemProp;
        private SerializedProperty _buffSystemProp;
        private SerializedProperty _hitboxSystemProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _entityOpen  = true;
        private bool _systemsOpen = true;
        private bool _runtimeOpen = true;

        // ── Runtime test ──────────────────────────────────────────────────────
        private float _testDamage = 25f;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _entityIdProp   = serializedObject.FindProperty("_entityId");
            _entityNameProp = serializedObject.FindProperty("_entityName");
            _entityTypeProp = serializedObject.FindProperty("_entityType");

            _healthSystemProp = serializedObject.FindProperty("HealthSystem");
            _armorSystemProp  = serializedObject.FindProperty("ArmorSystem");
            _buffSystemProp   = serializedObject.FindProperty("BuffSystem");
            _hitboxSystemProp = serializedObject.FindProperty("HitboxSystem");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_PlayerController pc = (M2922_PlayerController)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME STATUS", _runtimeOpen, () =>
                {
                    // ── Santé ─────────────────────────────────────────────────
                    if (pc.HasHealth)
                    {
                        float hp    = pc.Health;
                        float maxHp = pc.MaxHealth;
                        float ratio = maxHp > 0f ? hp / maxHp : 0f;

                        Color barColor = ratio > 0.5f ? Color.green
                                       : ratio > 0.25f ? Color.yellow
                                       : Color.red;
                        Color prev = GUI.color;
                        GUI.color  = barColor;
                        Rect rect  = EditorGUILayout.GetControlRect(false, 18f);
                        EditorGUI.ProgressBar(rect, ratio, $"HP  {hp:F0} / {maxHp:F0}");
                        GUI.color  = prev;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("HP : aucun système (invincible)", EditorStyles.miniLabel);
                    }

                    // ── Armure ────────────────────────────────────────────────
                    if (pc.HasArmor)
                    {
                        float ap    = pc.ArmorPoints;
                        float maxAp = pc.MaxArmorPoints;
                        float ratio = maxAp > 0f ? ap / maxAp : 0f;

                        Color prev = GUI.color;
                        GUI.color  = ratio > 0.5f ? new Color(0.3f, 0.6f, 1f)
                                   : ratio > 0.25f ? Color.yellow : Color.red;
                        Rect rect  = EditorGUILayout.GetControlRect(false, 18f);
                        EditorGUI.ProgressBar(rect, ratio, $"AP  {ap:F0} / {maxAp:F0}");
                        GUI.color  = prev;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("AP : aucun système", EditorStyles.miniLabel);
                    }

                    // ── Hitboxes ──────────────────────────────────────────────
                    if (pc.HasHitboxes)
                    {
                        M2922_HitboxSystem hb = pc.HitboxSystem;
                        EditorGUILayout.LabelField(
                            $"Hitboxes : {hb.HitboxCount} collider(s) enregistré(s)",
                            EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Hitboxes : aucun système (non touchable)", EditorStyles.miniLabel);
                    }

                    GUILayout.Space(6);

                    // ── Statut ────────────────────────────────────────────────
                    EditorGUILayout.BeginHorizontal();
                    DrawStatusPill("Vivant",   pc.IsAlive,        Color.green, Color.red);
                    DrawStatusPill("Armure",   pc.HasArmor,       new Color(0.3f, 0.6f, 1f), Color.gray);
                    DrawStatusPill("Buffs",    pc.HasActiveBuff,  Color.yellow, Color.gray);
                    DrawStatusPill("Hitboxes", pc.HasHitboxes,    Color.green, Color.gray);
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(4);

                    // ── Actions de test ───────────────────────────────────────
                    EditorGUILayout.LabelField("Test dégâts", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Montant", GUILayout.Width(60));
                    _testDamage = EditorGUILayout.FloatField(_testDamage, GUILayout.Width(55));
                    if (GUILayout.Button($"-{_testDamage:F0} HP"))
                        pc.TakeDamage(_testDamage, -1, DamageType.Generic);
                    if (GUILayout.Button("Kill"))
                        pc.Die(-1);
                    if (GUILayout.Button("Soigner"))
                        pc.Heal(pc.MaxHealth);
                    EditorGUILayout.EndHorizontal();
                });
            }

            // =================================================================
            // ENTITY SETTINGS
            // =================================================================
            _entityOpen = Section("ENTITY SETTINGS", _entityOpen, () =>
            {
                EditorGUILayout.PropertyField(_entityIdProp,
                    new GUIContent("Entity ID", "Identifiant unique du joueur dans le monde."));
                EditorGUILayout.PropertyField(_entityNameProp,
                    new GUIContent("Entity Name"));
                EditorGUILayout.PropertyField(_entityTypeProp,
                    new GUIContent("Entity Type"));
            });

            // =================================================================
            // SYSTEMS
            // =================================================================
            _systemsOpen = Section("SYSTEMS", _systemsOpen, () =>
            {
                DrawSystemSlot(_healthSystemProp, "Health System",
                    "M2922_HealthSystem — gère HP, mort et soins.\nNull = invincible.");

                DrawSystemSlot(_armorSystemProp, "Armor System",
                    "M2922_ArmorSystem — absorbe les dégâts avant la santé.\nNull = pas d'armure.");

                DrawSystemSlot(_buffSystemProp, "Buff System",
                    "M2922_BuffSystem — multiplicateurs de stats, regen, poison…\nNull = aucun buff.");

                DrawSystemSlot(_hitboxSystemProp, "Hitbox System",
                    "M2922_HitboxSystem — liste les Collider qui reçoivent les tirs.\n" +
                    "Permet de savoir QUI est touché et si c'est une zone critique.\nNull = non touchable physiquement.");

                GUILayout.Space(2);

                // Bouton auto-wire
                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Auto-Wire (GetComponent)"))
                    {
                        Undo.RecordObject(target, "PlayerController Auto-Wire");
                        M2922_PlayerController pc2 = (M2922_PlayerController)target;
                        if (!pc2.HasHealth)   pc2.HealthSystem  = pc2.GetComponent<M2922_HealthSystem>();
                        if (!pc2.HasArmor)    pc2.ArmorSystem   = pc2.GetComponent<M2922_ArmorSystem>();
                        if (!pc2.HasBuffs)    pc2.BuffSystem    = pc2.GetComponent<M2922_BuffSystem>();
                        if (!pc2.HasHitboxes) pc2.HitboxSystem  = pc2.GetComponent<M2922_HitboxSystem>();
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                    }
                }
            });

            // =================================================================
            // VISUAL DEBUG
            // =================================================================
            DrawVisualDebug();

            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static void DrawSystemSlot(SerializedProperty prop, string label, string tooltip)
        {
            Color prevBg = GUI.backgroundColor;
            bool  filled = prop.objectReferenceValue != null;
            GUI.backgroundColor = filled ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f);

            EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));

            GUI.backgroundColor = prevBg;

            if (!filled)
                EditorGUILayout.LabelField(
                    $"  ↳ {label} absent — fonctionnalité désactivée.",
                    EditorStyles.miniLabel);
        }

        private static void DrawStatusPill(string label, bool active, Color activeColor, Color inactiveColor)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = active ? activeColor : inactiveColor;
            EditorGUILayout.LabelField(
                (active ? "● " : "○ ") + label,
                EditorStyles.miniLabel,
                GUILayout.Width(72f));
            GUI.contentColor = prev;
        }
    }
}
