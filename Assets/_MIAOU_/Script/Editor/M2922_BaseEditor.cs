using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

namespace M2922.Editor
{
    /// <summary>
    /// Classe de base pour tous les éditeurs personnalisés M2922.
    /// Fournit : Section(), DrawVisualDebug() (props héritées de M2922_Base).
    /// Héritage : class MonEditor : M2922_BaseEditor — appeler base.OnEnable() dans OnEnable().
    /// </summary>
    public abstract class M2922_BaseEditor : UnityEditor.Editor
    {
        // ── Visual Debug props (hérités de M2922_Base._showGizmo / _gizmoColor / _gizmoSize) ──
        private SerializedProperty _showGizmoProp;
        private SerializedProperty _gizmoColorProp;
        private SerializedProperty _gizmoSizeProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _visualDebugOpen = false;

        // ─────────────────────────────────────────────────────────────────────

        protected virtual void OnEnable()
        {
            _showGizmoProp  = serializedObject.FindProperty("_showGizmo");
            _gizmoColorProp = serializedObject.FindProperty("_gizmoColor");
            _gizmoSizeProp  = serializedObject.FindProperty("_gizmoSize");
        }

        // =====================================================================
        // VISUAL DEBUG
        // =====================================================================

        /// <summary>
        /// Dessine la section VISUAL DEBUG (ShowGizmo, GizmoColor, GizmoSize).
        /// Appeler AVANT serializedObject.ApplyModifiedProperties().
        /// </summary>
        protected void DrawVisualDebug()
        {
            if (_showGizmoProp == null) return;

            _visualDebugOpen = Section("VISUAL DEBUG", _visualDebugOpen, () =>
            {
                EditorGUILayout.PropertyField(_showGizmoProp,  new GUIContent("Show Gizmo"));
                EditorGUI.BeginDisabledGroup(!_showGizmoProp.boolValue);
                EditorGUILayout.PropertyField(_gizmoColorProp, new GUIContent("Gizmo Color"));
                EditorGUILayout.PropertyField(_gizmoSizeProp,  new GUIContent("Gizmo Size"));
                EditorGUI.EndDisabledGroup();
            });
        }

        // =====================================================================
        // SECTION HELPER
        // =====================================================================

        /// <summary>
        /// Foldout stylisé avec bordure EditorStyles.helpBox.
        /// Retourne le nouvel état open/closed à stocker dans la variable bool.
        /// </summary>
        protected static bool Section(string title, bool open, System.Action content)
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
