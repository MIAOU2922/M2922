using UnityEditor;
using UnityEngine;

namespace M2922.Editor
{
    /// <summary>
    /// Custom Editor pour M2922_EventBus
    /// Affiche le nombre d'événements et permet de forcer le recalcul
    /// </summary>
    [CustomEditor(typeof(M2922.Core.M2922_EventBus))]
    public class M2922_EventBusEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Dessiner l'inspecteur par défaut
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            
            // Section d'information sur les événements
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📊 Event System Info", EditorStyles.boldLabel);
            
            SerializedProperty eventTypeCountProp = serializedObject.FindProperty("_eventTypeCount");
            int eventCount = eventTypeCountProp.intValue;
            
            // Calculer le nombre réel d'événements dans l'enum
            int actualCount = System.Enum.GetValues(typeof(M2922.Core.EventType)).Length;
            
            EditorGUILayout.LabelField("Événements configurés:", eventCount.ToString());
            EditorGUILayout.LabelField("Événements dans enum:", actualCount.ToString());
            
            // Avertissement si mismatch
            if (eventCount != actualCount)
            {
                EditorGUILayout.HelpBox(
                    $"⚠️ Mismatch détecté!\n" +
                    $"Configuré: {eventCount} | Enum: {actualCount}\n" +
                    $"Cliquez sur 'Update Event Count' pour synchroniser.",
                    MessageType.Warning
                );
                
                GUILayout.Space(5);
                
                if (GUILayout.Button("🔄 Update Event Count", GUILayout.Height(30)))
                {
                    eventTypeCountProp.intValue = actualCount;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    Debug.Log($"[M2922 EventBus] Event count updated: {eventCount} → {actualCount}");
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "✅ Event count is synchronized with enum.",
                    MessageType.Info
                );
            }
            
            EditorGUILayout.EndVertical();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
