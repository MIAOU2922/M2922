using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

namespace M2922.Editor
{
    /// <summary>
    /// Custom Editor pour M2922_SpawnManager
    /// Auto-remplit la liste de spawn points quand Auto Discover est activé
    /// </summary>
    [CustomEditor(typeof(M2922.Spawning.M2922_SpawnManager))]
    public class M2922_SpawnManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _autoDiscoverProp;
        private SerializedProperty _spawnPointsProp;
        
        private void OnEnable()
        {
            _autoDiscoverProp = serializedObject.FindProperty("_autoDiscoverSpawnPoints");
            _spawnPointsProp = serializedObject.FindProperty("_spawnPoints");
        }
        
        public override void OnInspectorGUI()
        {
            // Mettre à jour les données sérialisées
            serializedObject.Update();
            
            // Dessiner l'inspecteur par défaut
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            
            // Section Auto-Discover avec bouton de refresh
            if (_autoDiscoverProp.boolValue)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Auto Discover Activé", EditorStyles.boldLabel);
                
                EditorGUILayout.HelpBox(
                    "La liste des spawn points sera automatiquement remplie au runtime.\n" +
                    "Cliquez sur 'Refresh Spawn Points' pour mettre à jour la liste maintenant.",
                    MessageType.Info
                );
                
                GUILayout.Space(5);
                
                // Bouton pour rafraîchir la liste
                if (GUILayout.Button("🔄 Refresh Spawn Points", GUILayout.Height(30)))
                {
                    RefreshSpawnPoints();
                }
                
                // Afficher le nombre de spawn points trouvés
                int spawnCount = _spawnPointsProp.arraySize;
                EditorGUILayout.LabelField("Spawn Points Trouvés:", spawnCount.ToString(), EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Mode Manuel", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Assignez manuellement les spawn points dans la liste 'Spawn Points'.\n" +
                    "Ou activez 'Auto Discover' pour remplir automatiquement.",
                    MessageType.Info
                );
                EditorGUILayout.EndVertical();
            }
            
            // Appliquer les modifications
            serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// Rafraîchit la liste des spawn points dans l'éditeur
        /// </summary>
        private void RefreshSpawnPoints()
        {
            // Trouver tous les M2922_SpawnPoint dans la scène
            M2922.Spawning.M2922_SpawnPoint[] foundSpawns = 
                GameObject.FindObjectsOfType<M2922.Spawning.M2922_SpawnPoint>();
            
            // Mettre à jour la liste
            _spawnPointsProp.ClearArray();
            _spawnPointsProp.arraySize = foundSpawns.Length;
            
            for (int i = 0; i < foundSpawns.Length; i++)
            {
                _spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = foundSpawns[i];
            }
            
            serializedObject.ApplyModifiedProperties();
            
            Debug.Log($"[M2922 SpawnManager] Trouvé et assigné {foundSpawns.Length} spawn points");
        }
    }
}
