using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Entity.Weapon;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_WeaponEntity))]
    public class M2922_WeaponEntityEditor : M2922_BaseEditor
    {
        // ── Entity ──────────────────────────────────────────────────────────
        private SerializedProperty _entityIdProp;
        private SerializedProperty _entityNameProp;
        private SerializedProperty _entityTypeProp;

        // ── Weapon system ──────────────────────────────────────────────────
        private SerializedProperty _weaponProp;

        // ── Pickup config ──────────────────────────────────────────────────
        private SerializedProperty _returnDelayProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _runtimeOpen  = true;
        private bool _entityOpen   = true;
        private bool _weaponOpen   = true;
        private bool _pickupOpen   = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _entityIdProp    = serializedObject.FindProperty("_entityId");
            _entityNameProp  = serializedObject.FindProperty("_entityName");
            _entityTypeProp  = serializedObject.FindProperty("_entityType");
            _weaponProp      = serializedObject.FindProperty("_weapon");
            _returnDelayProp = serializedObject.FindProperty("_returnDelay");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_WeaponEntity we = (M2922_WeaponEntity)target;

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME", _runtimeOpen, () =>
                {
                    bool equipped = we.IsEquipped;
                    Color prev = GUI.contentColor;
                    GUI.contentColor = equipped ? Color.green : Color.gray;
                    EditorGUILayout.LabelField(
                        equipped ? "\u25cf Equip\u00e9" : "\u25cb Non \u00e9quip\u00e9",
                        EditorStyles.boldLabel);
                    GUI.contentColor = prev;

                    M2922_Weapon w = we.Weapon;
                    if (w != null)
                    {
                        GUILayout.Space(2);
                        int mag  = w.CurrentAmmo;
                        int rsrv = w.ReserveAmmo;
                        bool inf = w.InfiniteAmmo;
                        EditorGUILayout.LabelField(
                            inf ? $"Munitions : {mag} / \u221e" : $"Munitions : {mag} / {rsrv} (r\u00e9serve)",
                            EditorStyles.miniLabel);
                    }
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
            // WEAPON SYSTEM
            // =================================================================
            _weaponOpen = Section("WEAPON SYSTEM", _weaponOpen, () =>
            {
                EditorGUILayout.PropertyField(_weaponProp,
                    new GUIContent("Weapon",
                        "R\u00e9f\u00e9rence au M2922_Weapon.\n" +
                        "Si vide, cherch\u00e9 automatiquement sur ce GameObject."));

                if (_weaponProp.objectReferenceValue == null)
                    EditorGUILayout.HelpBox(
                        "Aucune r\u00e9f\u00e9rence — M2922_Weapon sera cherch\u00e9 en GetComponent() au Start.",
                        MessageType.None);
            });

            // =================================================================
            // PICKUP CONFIG
            // =================================================================
            _pickupOpen = Section("PICKUP CONFIG", _pickupOpen, () =>
            {
                EditorGUILayout.PropertyField(_returnDelayProp,
                    new GUIContent("Return Delay (s)",
                        "D\u00e9lai avant retour \u00e0 l'origine apr\u00e8s avoir \u00e9t\u00e9 l\u00e2ch\u00e9.\n" +
                        "-1 = l'arme reste l\u00e0 o\u00f9 elle est (permanente)."));

                float rd = _returnDelayProp.floatValue;
                EditorGUILayout.LabelField(
                    rd < 0f ? "\u2192 Arme permanente (pas de retour automatique)"
                            : $"\u2192 Retour dans {rd:F1}\u202fs apr\u00e8s le l\u00e2ch\u00e9",
                    EditorStyles.miniLabel);
            });

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
