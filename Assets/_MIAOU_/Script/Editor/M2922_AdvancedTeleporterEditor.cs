using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace M2922.Component.Utils.Editor
{
    /// <summary>
    /// Custom editor pour M2922_AdvancedTeleporter.
    /// Affiche une ReorderableList unifiée qui synchronise _destinations
    /// et _linkedTeleporters en parallèle.
    ///
    /// Auto-détection :
    ///   - Si on set un Transform, on cherche un AdvancedTeleporter sur le même GO
    ///   - Si on set un AdvancedTeleporter, on ajoute son Transform aux destinations
    /// </summary>
    [CustomEditor(typeof(M2922_AdvancedTeleporter))]
    [CanEditMultipleObjects]
    public class M2922_AdvancedTeleporterEditor : M2922.Editor.M2922_BaseEditor
    {
        // Props gérées manuellement (cachées de l'itérateur de base)
        private static readonly HashSet<string> ManualProps = new HashSet<string>
        {
            "_destination", "_destinations",
            "_linkedTeleporters", "_linkedTeleporter", "_arrivalImmunity",
        };

        private SerializedProperty _propSelectionMode;
        private SerializedProperty _propDestination;
        private SerializedProperty _propDestinations;
        private SerializedProperty _propLinkedTeleporters;
        private SerializedProperty _propLinkedTeleporter;
        private SerializedProperty _propArrivalImmunity;
        private ReorderableList _destinationList;

        static M2922_AdvancedTeleporterEditor()
        {
            HandledProperties.Add("_selectionMode");
            foreach (var p in ManualProps) HandledProperties.Add(p);
        }

        public override void OnInspectorGUI()
        {
            // On réplique le comportement du BaseEditor mais en cachant
            // nos ManualProps de l'itérateur.
            serializedObject.Update();

            if (_propSelectionMode == null) CacheProperties();

            DrawBaseIterator();

            OnAfterProperties();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Copie de la boucle d'itération du BaseEditor, avec nos props cachées.</summary>
        private void DrawBaseIterator()
        {
            // Lazy init (UdonSharp peut skip OnEnable)
            if (_propDebug == null)
            {
                _propDebug          = serializedObject.FindProperty("DEBUG");
                _propVerboseDebug   = serializedObject.FindProperty("VERBOSE_DEBUG");
                _propManager        = serializedObject.FindProperty("Manager");
                _propAutoName       = serializedObject.FindProperty("_autoName");
                _propScriptName     = serializedObject.FindProperty("_ScriptName");
                _propShowGizmo      = serializedObject.FindProperty("_showGizmo");
                _propAutoGizmo      = serializedObject.FindProperty("_autoGizmo");
            }

            bool hideGlobalDebug = !_isManager;
            bool hideManagerRef = _propManager.objectReferenceValue != null;
            bool hideScriptName = _propAutoName.boolValue
                && !string.IsNullOrEmpty(_propScriptName.stringValue);
            bool hideGizmoConfig =
                !_propShowGizmo.boolValue || _propAutoGizmo.boolValue;

            var hideSet = new HashSet<string>();
            if (hideGlobalDebug) { hideSet.Add("DEBUG"); hideSet.Add("VERBOSE_DEBUG"); }
            if (hideManagerRef) hideSet.Add("Manager");
            if (hideScriptName) hideSet.Add("_ScriptName");
            if (hideGizmoConfig) hideSet.UnionWith(GizmoConfigProps);
            if (!_propShowGizmo.boolValue) hideSet.Add("_autoGizmo");
            // Toujours cacher nos props manuelles
            hideSet.UnionWith(ManualProps);

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            if (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                do
                {
                    string propName = iterator.name;
                    if (propName == "m_Script")
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.PropertyField(iterator, true);
                        EditorGUI.EndDisabledGroup();
                        continue;
                    }
                    if (HandledProperties.Contains(propName))
                    {
                        if (!hideSet.Contains(propName))
                            EditorGUILayout.PropertyField(iterator, true);
                        continue;
                    }
                    EditorGUILayout.PropertyField(iterator, true);
                }
                while (iterator.NextVisible(enterChildren));
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheProperties();
            BuildList();
        }

        private void CacheProperties()
        {
            _propSelectionMode     = serializedObject.FindProperty("_selectionMode");
            _propDestination       = serializedObject.FindProperty("_destination");
            _propDestinations      = serializedObject.FindProperty("_destinations");
            _propLinkedTeleporters = serializedObject.FindProperty("_linkedTeleporters");
            _propLinkedTeleporter  = serializedObject.FindProperty("_linkedTeleporter");
            _propArrivalImmunity   = serializedObject.FindProperty("_arrivalImmunity");
        }

        private void BuildList()
        {
            _destinationList = new ReorderableList(
                serializedObject,
                _propDestinations,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true)
            {
                drawHeaderCallback  = DrawHeader,
                drawElementCallback = DrawElement,
                onAddCallback       = OnAdd,
                onRemoveCallback    = OnRemove,
                onReorderCallbackWithDetails = OnReorder,
                elementHeight = EditorGUIUtility.singleLineHeight + 4f
            };
        }

        protected override void OnAfterProperties()
        {
            // Lazy init
            if (_propSelectionMode == null) CacheProperties();
            if (_destinationList == null || _destinationList.serializedProperty != _propDestinations)
                BuildList();

            var mode = (TeleporterSelectionMode)_propSelectionMode.enumValueIndex;

            // ── Mode SINGLE ──────────────────────────────────────────────
            if (mode == TeleporterSelectionMode.Single)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_propDestination, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                {
                    AutoDetectLinkFromSingleDest();
                }

                EditorGUILayout.PropertyField(_propLinkedTeleporter, GUIContent.none);
                EditorGUILayout.PropertyField(_propArrivalImmunity, GUIContent.none);
            }

            // ── Mode RANDOM / SEQUENTIAL / RANDOM_NO_REPEAT ─────────────
            else
            {
                SyncArrays();

                EditorGUILayout.Space();
                _destinationList.DoLayoutList();

                EditorGUILayout.PropertyField(_propArrivalImmunity, GUIContent.none);
            }
        }

        // =============================================
        //  REORDERABLE LIST CALLBACKS
        // =============================================

        private void DrawHeader(Rect rect)
        {
            float w = rect.width;
            EditorGUI.LabelField(new Rect(rect.x,      rect.y, 20, rect.height), "#",  EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + 25, rect.y, w * 0.45f - 25, rect.height), "Destination", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + w * 0.45f + 5, rect.y, w * 0.55f - 5, rect.height), "Linked Teleporter", EditorStyles.miniLabel);
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= _propDestinations.arraySize) return;

            SerializedProperty destProp = _propDestinations.GetArrayElementAtIndex(index);
            SerializedProperty linkProp = _propLinkedTeleporters.arraySize > index
                ? _propLinkedTeleporters.GetArrayElementAtIndex(index)
                : null;

            float w = rect.width;
            float h = rect.height;

            EditorGUI.LabelField(new Rect(rect.x, rect.y, 20, h), index.ToString());

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(rect.x + 25, rect.y, w * 0.45f - 25, h),
                destProp, GUIContent.none);

            // Auto-detect : si on change le Transform, chercher un AdvancedTeleporter sur le même GO
            if (EditorGUI.EndChangeCheck())
            {
                AutoDetectLinkFromDest(index);
            }

            if (linkProp != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(
                    new Rect(rect.x + w * 0.45f + 5, rect.y, w * 0.55f - 5, h),
                    linkProp, GUIContent.none);

                // Auto-detect inverse : si on set un linked teleporter, ajouter son Transform
                if (EditorGUI.EndChangeCheck())
                {
                    AutoDetectDestFromLink(index);
                }
            }
        }

        private void OnAdd(ReorderableList list)
        {
            int idx = _propDestinations.arraySize;
            _propDestinations.arraySize++;
            _propLinkedTeleporters.arraySize++;
            _propDestinations.GetArrayElementAtIndex(idx).objectReferenceValue = null;
            _propLinkedTeleporters.GetArrayElementAtIndex(idx).objectReferenceValue = null;
        }

        private void OnRemove(ReorderableList list)
        {
            int idx = list.index;
            if (idx < 0 || idx >= _propDestinations.arraySize) return;

            // Décaler les éléments après l'index supprimé
            for (int i = idx + 1; i < _propDestinations.arraySize; i++)
            {
                _propDestinations.GetArrayElementAtIndex(i - 1).objectReferenceValue
                    = _propDestinations.GetArrayElementAtIndex(i).objectReferenceValue;
            }
            for (int i = idx + 1; i < _propLinkedTeleporters.arraySize; i++)
            {
                _propLinkedTeleporters.GetArrayElementAtIndex(i - 1).objectReferenceValue
                    = _propLinkedTeleporters.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            _propDestinations.arraySize--;
            _propLinkedTeleporters.arraySize--;
        }

        private void OnReorder(ReorderableList list, int oldIndex, int newIndex)
        {
            _propLinkedTeleporters.MoveArrayElement(oldIndex, newIndex);
        }

        // =============================================
        //  AUTO-DETECT
        // =============================================

        private void AutoDetectLinkFromSingleDest()
        {
            Transform dest = _propDestination.objectReferenceValue as Transform;
            if (dest == null) return;
            var advTp = dest.GetComponent<M2922_AdvancedTeleporter>();
            if (advTp != null)
                _propLinkedTeleporter.objectReferenceValue = advTp;
        }

        private void AutoDetectLinkFromDest(int index)
        {
            if (index >= _propDestinations.arraySize) return;
            Transform dest = _propDestinations.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
            if (dest == null) return;

            var advTp = dest.GetComponent<M2922_AdvancedTeleporter>();
            if (advTp == null) return;

            // S'assurer que _linkedTeleporters est assez grand
            if (index >= _propLinkedTeleporters.arraySize)
                _propLinkedTeleporters.arraySize = index + 1;

            _propLinkedTeleporters.GetArrayElementAtIndex(index).objectReferenceValue = advTp;
        }

        private void AutoDetectDestFromLink(int index)
        {
            if (index >= _propLinkedTeleporters.arraySize) return;
            var link = _propLinkedTeleporters.GetArrayElementAtIndex(index).objectReferenceValue as M2922_AdvancedTeleporter;
            if (link == null) return;

            // S'assurer que _destinations est assez grand
            if (index >= _propDestinations.arraySize)
                _propDestinations.arraySize = index + 1;

            _propDestinations.GetArrayElementAtIndex(index).objectReferenceValue = link.transform;
        }

        // =============================================
        //  HELPERS
        // =============================================

        private void SyncArrays()
        {
            int ds = _propDestinations.arraySize;
            int ls = _propLinkedTeleporters.arraySize;
            int max = Mathf.Max(ds, ls);

            if (ds != ls)
            {
                _propDestinations.arraySize = max;
                _propLinkedTeleporters.arraySize = max;
            }
        }
    }
}
