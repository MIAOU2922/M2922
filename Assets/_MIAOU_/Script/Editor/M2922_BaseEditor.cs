using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace M2922.Editor
{
    /// <summary>
    /// Custom editor pour tous les scripts héritant de M2922_Base.
    /// Gère le conditionnal display :
    /// - Global Debug → caché sauf sur le Manager
    /// - Manager ref → caché si assigné
    /// - Auto Name → si coché + ScriptName rempli → cache ScriptName
    /// - Show Gizmo décoché → cache Auto Gizmo + config gizmo
    /// - Auto Gizmo coché → cache les valeurs de config gizmo
    /// </summary>
    [CustomEditor(typeof(M2922.Core.M2922_Base), true)]
    [CanEditMultipleObjects]
    public class M2922_BaseEditor : UnityEditor.Editor
    {
        // Properties gérées manuellement (protected pour les classes dérivées)
        protected static readonly HashSet<string> HandledProperties = new HashSet<string>
        {
            "DEBUG",
            "VERBOSE_DEBUG",
            "Manager",
            "_autoName",
            "_ScriptName",
            "_showGizmo",
            "_autoGizmo",
            "_gizmoOffsetY",
            "_gizmoHeaderScale",
            "_gizmoValueScale",
            "_gizmoHeaderColor",
            "_gizmoOnlyWhenSelected",
        };

        // Properties à cacher conditionnellement
        protected static readonly HashSet<string> GizmoConfigProps = new HashSet<string>
        {
            "_gizmoOffsetY",
            "_gizmoHeaderScale",
            "_gizmoValueScale",
            "_gizmoHeaderColor",
            "_gizmoOnlyWhenSelected",
        };

        protected bool _isManager;

        // Serialized properties (protected pour les classes dérivées)
        protected SerializedProperty _propDebug;
        protected SerializedProperty _propVerboseDebug;
        protected SerializedProperty _propManager;
        protected SerializedProperty _propAutoName;
        protected SerializedProperty _propScriptName;
        protected SerializedProperty _propShowGizmo;
        protected SerializedProperty _propAutoGizmo;
        protected SerializedProperty _propGizmoOffsetY;
        protected SerializedProperty _propGizmoHeaderScale;
        protected SerializedProperty _propGizmoValueScale;
        protected SerializedProperty _propGizmoHeaderColor;
        protected SerializedProperty _propGizmoOnlyWhenSelected;

        protected virtual void OnEnable()
        {
            _propDebug = serializedObject.FindProperty("DEBUG");
            _propVerboseDebug = serializedObject.FindProperty("VERBOSE_DEBUG");
            _propManager = serializedObject.FindProperty("Manager");
            _propAutoName = serializedObject.FindProperty("_autoName");
            _propScriptName = serializedObject.FindProperty("_ScriptName");
            _propShowGizmo = serializedObject.FindProperty("_showGizmo");
            _propAutoGizmo = serializedObject.FindProperty("_autoGizmo");
            _propGizmoOffsetY = serializedObject.FindProperty("_gizmoOffsetY");
            _propGizmoHeaderScale = serializedObject.FindProperty("_gizmoHeaderScale");
            _propGizmoValueScale = serializedObject.FindProperty("_gizmoValueScale");
            _propGizmoHeaderColor = serializedObject.FindProperty("_gizmoHeaderColor");
            _propGizmoOnlyWhenSelected = serializedObject.FindProperty("_gizmoOnlyWhenSelected");

            _isManager = target is M2922.Core.M2922_Manager;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- Compute conditional visibility ---
            bool hideGlobalDebug = !_isManager;
            bool hideManagerRef = _propManager.objectReferenceValue != null;
            bool hideScriptName = _propAutoName.boolValue
                && !string.IsNullOrEmpty(_propScriptName.stringValue);
            bool hideGizmoConfig =
                !_propShowGizmo.boolValue       // Show Gizmo décoché → cache tout
                || _propAutoGizmo.boolValue;     // Auto Gizmo coché → cache config

            // --- Build hide set ---
            HashSet<string> hideSet = new HashSet<string>();
            if (hideGlobalDebug)
            {
                hideSet.Add("DEBUG");
                hideSet.Add("VERBOSE_DEBUG");
            }
            if (hideManagerRef)
            {
                hideSet.Add("Manager");
            }
            if (hideScriptName)
            {
                hideSet.Add("_ScriptName");
            }
            if (hideGizmoConfig)
            {
                hideSet.UnionWith(GizmoConfigProps);
                // Si Show Gizmo est décoché, on cache aussi Auto Gizmo
                if (!_propShowGizmo.boolValue)
                    hideSet.Add("_autoGizmo");
            }

            // --- Iterate all serialized properties ---
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            // Passer le premier niveau (m_Script)
            if (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                do
                {
                    string propName = iterator.name;

                    // On garde m_Script (le référence de script Unity)
                    if (propName == "m_Script")
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.PropertyField(iterator, true);
                        EditorGUI.EndDisabledGroup();
                        continue;
                    }

                    // Si la propriété est gérée par nous, on l'affiche
                    // seulement si elle n'est pas dans le hideSet
                    if (HandledProperties.Contains(propName))
                    {
                        if (!hideSet.Contains(propName))
                        {
                            EditorGUILayout.PropertyField(iterator, true);
                        }
                        continue;
                    }

                    // Propriétés des classes dérivées : on les affiche telles quelles
                    EditorGUILayout.PropertyField(iterator, true);
                }
                while (iterator.NextVisible(enterChildren));
            }

            // Hook pour les classes dérivées (UI custom après les propriétés)
            OnAfterProperties();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Surchargez cette méthode dans les classes dérivées pour ajouter
        /// de l'UI custom après l'affichage des propriétés serialized.
        /// </summary>
        protected virtual void OnAfterProperties() { }
    }
}
