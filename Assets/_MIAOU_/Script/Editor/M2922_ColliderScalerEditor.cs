using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UdonSharpEditor;
using M2922.Combat;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_ColliderScaler))]
    public class M2922_ColliderScalerEditor : M2922_BaseEditor
    {
        // ── Props ──────────────────────────────────────────────────────────────
        private SerializedProperty _refAProp;
        private SerializedProperty _refBProp;
        private SerializedProperty _capsuleColliderProp;
        private SerializedProperty _boxColliderProp;
        private SerializedProperty _baseRadiusProp;
        private SerializedProperty _referenceScaleProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _refsOpen    = true;
        private bool _colliderOpen = true;
        private bool _scaleOpen   = true;
        private bool _runtimeOpen = true;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _refAProp            = serializedObject.FindProperty("_refA");
            _refBProp            = serializedObject.FindProperty("_refB");
            _capsuleColliderProp = serializedObject.FindProperty("_capsuleCollider");
            _boxColliderProp     = serializedObject.FindProperty("_boxCollider");
            _baseRadiusProp      = serializedObject.FindProperty("_baseRadius");
            _referenceScaleProp  = serializedObject.FindProperty("_referenceScale");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_ColliderScaler scaler = (M2922_ColliderScaler)target;

            // =================================================================
            // VALIDATION
            // =================================================================
            bool missingA  = _refAProp.objectReferenceValue == null;
            bool missingB  = _refBProp.objectReferenceValue == null;
            bool missingCol = _capsuleColliderProp.objectReferenceValue == null
                           && _boxColliderProp.objectReferenceValue == null;

            if (missingA || missingB)
                EditorGUILayout.HelpBox("Assigner _refA et _refB pour activer l'alignement.", MessageType.Warning);
            if (missingCol)
                EditorGUILayout.HelpBox("Aucun collider assigné — utiliser Auto-Find.", MessageType.Warning);

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                _runtimeOpen = Section("RUNTIME STATUS", _runtimeOpen, () =>
                {
                    Vector3 dir  = scaler.LastDir;
                    float   dist = scaler.LastDist;
                    float   fact = scaler.LastRadFact;

                    EditorGUILayout.LabelField($"Direction  : ({dir.x:F2}, {dir.y:F2}, {dir.z:F2})", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Distance   : {dist:F4} m", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Rayon ×    : {fact:F3}  →  r = {_baseRadiusProp.floatValue * fact:F4}", EditorStyles.miniLabel);

                    if (dist > 0f)
                    {
                        Color prev = GUI.color;
                        GUI.color  = Color.cyan;
                        Rect rect  = EditorGUILayout.GetControlRect(false, 14f);
                        EditorGUI.ProgressBar(rect, Mathf.Clamp01(dist / 0.6f), $"dist {dist:F3} m");
                        GUI.color  = prev;
                    }
                });
            }

            // =================================================================
            // RÉFÉRENCES A → B
            // =================================================================
            _refsOpen = Section("RÉFÉRENCES A → B", _refsOpen, () =>
            {
                DrawRefSlot(_refAProp, "Ref A (départ)", "Articulation de départ (ex: Shoulder, Hip).");
                DrawRefSlot(_refBProp, "Ref B (arrivée)", "Articulation d'arrivée (ex: Elbow, Knee).");

                if (!missingA && !missingB)
                {
                    Transform a = _refAProp.objectReferenceValue as Transform;
                    Transform b = _refBProp.objectReferenceValue as Transform;
                    float d = Vector3.Distance(a.position, b.position);
                    EditorGUILayout.LabelField($"→ Distance actuelle : {d:F4} m", EditorStyles.miniLabel);
                }
            });

            // =================================================================
            // COLLIDER
            // =================================================================
            _colliderOpen = Section("COLLIDER", _colliderOpen, () =>
            {
                DrawColliderSlot(_capsuleColliderProp, "Capsule Collider");
                DrawColliderSlot(_boxColliderProp,     "Box Collider");

                GUILayout.Space(2);
                if (GUILayout.Button("Auto-Find (GetComponent)"))
                {
                    Undo.RecordObject(target, "ColliderScaler Auto-Find");
                    if (_capsuleColliderProp.objectReferenceValue == null)
                        _capsuleColliderProp.objectReferenceValue = scaler.GetComponent<CapsuleCollider>();
                    if (_boxColliderProp.objectReferenceValue == null)
                        _boxColliderProp.objectReferenceValue = scaler.GetComponent<BoxCollider>();
                }
            });

            // =================================================================
            // SCALE REFERENCE
            // =================================================================
            _scaleOpen = Section("RAYON & SCALE REFERENCE", _scaleOpen, () =>
            {
                EditorGUILayout.PropertyField(_baseRadiusProp,
                    new GUIContent("Base Radius",
                        "Rayon à l'échelle 1× de l'avatar.\n" +
                        "Scalé automatiquement par lossyScale de Ref A."));

                GUILayout.Space(4);

                EditorGUILayout.PropertyField(_referenceScaleProp,
                    new GUIContent("Reference Scale",
                        "lossyScale de Ref A quand l'avatar est à l'échelle 1×.\n" +
                        "Utiliser le bouton ci-dessous pour le capturer."));

                if (GUILayout.Button("Capturer le lossyScale actuel de Ref A →"))
                {
                    Transform a = _refAProp.objectReferenceValue as Transform;
                    if (a != null)
                    {
                        Undo.RecordObject(target, "Capture Reference Scale");
                        _referenceScaleProp.vector3Value = a.lossyScale;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Ref A manquante",
                            "Assigner Ref A avant de capturer.", "OK");
                    }
                }

                if (_referenceScaleProp.vector3Value != Vector3.one)
                {
                    Vector3 rs = _referenceScaleProp.vector3Value;
                    EditorGUILayout.LabelField(
                        $"→ Ref scale : ({rs.x:F3}, {rs.y:F3}, {rs.z:F3})",
                        EditorStyles.miniLabel);
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

        private static void DrawRefSlot(SerializedProperty prop, string label, string tooltip)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = prop.objectReferenceValue != null
                ? new Color(0.55f, 1f, 0.55f)
                : new Color(1f, 0.55f, 0.55f);
            EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));
            GUI.backgroundColor = prevBg;
        }

        private static void DrawColliderSlot(SerializedProperty prop, string label)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = prop.objectReferenceValue != null
                ? new Color(0.55f, 1f, 0.55f)
                : Color.white;
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            GUI.backgroundColor = prevBg;
        }
    }
}

