using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace M2922.Component.UI.Editor
{
    /// <summary>
    /// Custom editor pour M2922_CounterUI.
    /// Hérite de M2922_BaseEditor pour les règles conditionnelles,
    /// et ajoute une ReorderableList pour les thresholds + aperçu.
    /// </summary>
    [CustomEditor(typeof(M2922_CounterUI))]
    [CanEditMultipleObjects]
    public class M2922_CounterUIEditor : M2922.Editor.M2922_BaseEditor
    {
        private M2922_CounterUI _target;

        private SerializedProperty _propThresholdValues;
        private SerializedProperty _propThresholdColors;
        private SerializedProperty _propDefaultColor;

        private ReorderableList _thresholdList;

        /// <summary>
        /// Ajoute les propriétés de thresholds aux handled properties
        /// pour les masquer de l'itérateur du BaseEditor.
        /// </summary>
        static M2922_CounterUIEditor()
        {
            HandledProperties.Add("_thresholdValues");
            HandledProperties.Add("_thresholdColors");
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _target = (M2922_CounterUI)target;

            _propThresholdValues = serializedObject.FindProperty("_thresholdValues");
            _propThresholdColors = serializedObject.FindProperty("_thresholdColors");
            _propDefaultColor = serializedObject.FindProperty("_defaultColor");

            BuildThresholdList();
        }

        private void BuildThresholdList()
        {
            _thresholdList = new ReorderableList(
                serializedObject,
                _propThresholdValues,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true)
            {
                drawHeaderCallback = DrawThresholdHeader,
                drawElementCallback = DrawThresholdElement,
                onAddCallback = OnAddThreshold,
                onRemoveCallback = OnRemoveThreshold,
                onReorderCallbackWithDetails = OnReorderThresholds,
                elementHeight = EditorGUIUtility.singleLineHeight + 2f
            };
        }

        /// <summary>
        /// Appelé par M2922_BaseEditor après l'affichage des propriétés serialized.
        /// </summary>
        protected override void OnAfterProperties()
        {
            // Lazy init : UdonSharp peut appeler OnInspectorGUI sans OnEnable()
            if (_propThresholdValues == null)
            {
                _target = (M2922_CounterUI)target;
                _propThresholdValues = serializedObject.FindProperty("_thresholdValues");
                _propThresholdColors = serializedObject.FindProperty("_thresholdColors");
                _propDefaultColor = serializedObject.FindProperty("_defaultColor");
            }

            if (_thresholdList == null || _thresholdList.serializedProperty != _propThresholdValues)
                BuildThresholdList();

            SyncArrays();

            // ---- COLOR THRESHOLDS (ReorderableList unifiée) ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== COLOR THRESHOLDS ===", EditorStyles.boldLabel);
            _thresholdList.DoLayoutList();
        }

        // =============================================
        //  REORDERABLE LIST CALLBACKS
        // =============================================

        private void DrawThresholdHeader(Rect rect)
        {
            float w = rect.width;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 30, rect.height), "#", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + 30, rect.y, 70, rect.height), "Seuil (≥)", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + 100, rect.y, w - 100, rect.height), "Couleur", EditorStyles.miniLabel);
        }

        private void DrawThresholdElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty valProp = _propThresholdValues.GetArrayElementAtIndex(index);
            SerializedProperty colProp = _propThresholdColors.GetArrayElementAtIndex(index);

            EditorGUI.LabelField(new Rect(rect.x, rect.y, 25, rect.height), index.ToString());
            EditorGUI.PropertyField(new Rect(rect.x + 30, rect.y, 65, rect.height), valProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + 100, rect.y, rect.width - 100, rect.height), colProp, GUIContent.none);
        }

        private void OnAddThreshold(ReorderableList list)
        {
            if (_propThresholdValues == null) return;

            int idx = _propThresholdValues.arraySize;
            _propThresholdValues.arraySize++;
            _propThresholdColors.arraySize++;
            _propThresholdValues.GetArrayElementAtIndex(idx).intValue = 0;
            _propThresholdColors.GetArrayElementAtIndex(idx).colorValue = Color.white;
        }

        private void OnRemoveThreshold(ReorderableList list)
        {
            if (_propThresholdValues == null || _propThresholdColors == null) return;

            int idx = list.index;
            if (idx >= 0 && idx < _propThresholdColors.arraySize - 1)
                _propThresholdColors.MoveArrayElement(idx + 1, idx);
            _propThresholdValues.arraySize--;
            _propThresholdColors.arraySize--;
        }

        private void OnReorderThresholds(ReorderableList list, int oldIndex, int newIndex)
        {
            if (_propThresholdColors == null) return;
            _propThresholdColors.MoveArrayElement(oldIndex, newIndex);
        }

        // =============================================
        //  HELPERS
        // =============================================

        private void SyncArrays()
        {
            if (_propThresholdValues == null || _propThresholdColors == null)
                return;

            int vs = _propThresholdValues.arraySize;
            int cs = _propThresholdColors.arraySize;
            if (vs != cs)
            {
                _propThresholdValues.arraySize = Mathf.Max(vs, cs);
                _propThresholdColors.arraySize = Mathf.Max(vs, cs);
            }
        }
    }
}

