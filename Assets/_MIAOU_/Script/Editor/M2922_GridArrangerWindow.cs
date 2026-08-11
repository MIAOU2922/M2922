using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace M2922.Editor
{
    /// <summary>
    /// Fenetre d'editeur pour arranger les GameObjects selectionnes en grille.
    /// 
    /// UTILISATION :
    /// 1. Selectionnez les objets dans la scene
    /// 2. Menu : Tools → M2922 → Grid Arranger
    /// 3. Reglez l'espacement X et Y
    /// 4. Cliquez "Arrange in Grid"
    /// 
    /// Raccourci rapide : Tools → M2922 → Arrange Selected (Grid Default 1x1)
    /// </summary>
    public class M2922_GridArrangerWindow : EditorWindow
    {
        private float _spacingX = 1f;
        private float _spacingY = 1f;
        private int _columns = 0; // 0 = auto (racine carree du nombre d'objets)
        private Vector3 _startPosition = Vector3.zero;
        private bool _centerGrid = true;

        // Memorisation des derniers reglages (survit au recompile via EditorPrefs)
        private const string PREF_SPACING_X = "M2922_GridArranger_SpacingX";
        private const string PREF_SPACING_Y = "M2922_GridArranger_SpacingY";
        private const string PREF_COLUMNS = "M2922_GridArranger_Columns";
        private const string PREF_CENTER = "M2922_GridArranger_CenterGrid";

        [MenuItem("M2922/Grid Arranger", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<M2922_GridArrangerWindow>("Grid Arranger");
            window.minSize = new Vector2(280, 220);
            window.Show();
        }

        /// <summary>
        /// Raccourci rapide : arrange les objets selectionnes avec l'espacement par defaut (1,1).
        /// </summary>
        [MenuItem("M2922/Arrange Selected in Grid (Default 1x1)", priority = 201)]
        public static void ArrangeSelectedDefault()
        {
            ArrangeSelected(1f, 1f, 0, Vector3.zero, true);
        }

        /// <summary>
        /// Raccourci rapide : arrange avec les derniers reglages sauvegardes.
        /// </summary>
        [MenuItem("M2922/Arrange Selected in Grid (Last Settings)", priority = 202)]
        public static void ArrangeSelectedLastSettings()
        {
            float sx = EditorPrefs.GetFloat(PREF_SPACING_X, 1f);
            float sy = EditorPrefs.GetFloat(PREF_SPACING_Y, 1f);
            int cols = EditorPrefs.GetInt(PREF_COLUMNS, 0);
            bool center = EditorPrefs.GetBool(PREF_CENTER, true);
            ArrangeSelected(sx, sy, cols, Vector3.zero, center);
        }

        private void OnEnable()
        {
            _spacingX = EditorPrefs.GetFloat(PREF_SPACING_X, 1f);
            _spacingY = EditorPrefs.GetFloat(PREF_SPACING_Y, 1f);
            _columns = EditorPrefs.GetInt(PREF_COLUMNS, 0);
            _centerGrid = EditorPrefs.GetBool(PREF_CENTER, true);
        }

        private void OnGUI()
        {
            GUILayout.Label("Grid Arranger", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // --- Selection info ---
            EditorGUILayout.LabelField("Selection:", $"{Selection.transforms.Length} objet(s)");

            EditorGUILayout.Space();

            // --- Parametres de grille ---
            _spacingX = EditorGUILayout.FloatField("Spacing X", _spacingX);
            _spacingY = EditorGUILayout.FloatField("Spacing Y", _spacingY);

            EditorGUILayout.BeginHorizontal();
            _columns = EditorGUILayout.IntField("Columns (0 = auto)", _columns);
            if (_columns < 0) _columns = 0;
            EditorGUILayout.EndHorizontal();

            _centerGrid = EditorGUILayout.Toggle("Center Grid", _centerGrid);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // --- Bouton d'action ---
            GUI.enabled = Selection.transforms.Length > 0;
            if (GUILayout.Button("Arrange in Grid", GUILayout.Height(30)))
            {
                SaveSettings();
                ArrangeSelected(_spacingX, _spacingY, _columns, _startPosition, _centerGrid);
            }
            GUI.enabled = true;

            // --- Bouton Pick Start Position ---
            if (GUILayout.Button("Set Start Position from First Selected"))
            {
                if (Selection.transforms.Length > 0)
                {
                    _startPosition = Selection.transforms[0].position;
                    Repaint();
                }
            }
            EditorGUILayout.LabelField("Start Position:", _startPosition.ToString("F2"));

            EditorGUILayout.Space();

            // --- Raccourcis rapides ---
            EditorGUILayout.LabelField("Quick Actions:", EditorStyles.miniLabel);
            if (GUILayout.Button("Arrange Default (1x1)"))
            {
                ArrangeSelected(1f, 1f, 0, Vector3.zero, true);
            }

            if (GUILayout.Button("Arrange Tight (0.5x0.5)"))
            {
                ArrangeSelected(0.5f, 0.5f, 0, Vector3.zero, true);
            }
        }

        private void SaveSettings()
        {
            EditorPrefs.SetFloat(PREF_SPACING_X, _spacingX);
            EditorPrefs.SetFloat(PREF_SPACING_Y, _spacingY);
            EditorPrefs.SetInt(PREF_COLUMNS, _columns);
            EditorPrefs.SetBool(PREF_CENTER, _centerGrid);
        }

        /// <summary>
        /// Arrange les GameObjects selectionnes en grille.
        /// </summary>
        /// <param name="spacingX">Espacement horizontal entre les objets.</param>
        /// <param name="spacingY">Espacement vertical entre les objets.</param>
        /// <param name="columns">Nombre de colonnes (0 = auto).</param>
        /// <param name="startPos">Position de depart (coin haut-gauche de la grille).</param>
        /// <param name="center">Si true, centre la grille autour de startPos.</param>
        public static void ArrangeSelected(float spacingX, float spacingY, int columns, Vector3 startPos, bool center)
        {
            Transform[] selected = Selection.transforms;

            if (selected.Length == 0)
            {
                Debug.LogWarning("[GridArranger] Aucun objet selectionne.");
                return;
            }

            // Determiner le nombre de colonnes
            int cols = columns > 0
                ? columns
                : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(selected.Length)));

            // Calculer l'offset de centrage
            int rows = Mathf.CeilToInt((float)selected.Length / cols);
            Vector3 offset = Vector3.zero;
            if (center)
            {
                float totalWidth = (cols - 1) * spacingX;
                float totalHeight = (rows - 1) * spacingY;
                offset = new Vector3(-totalWidth * 0.5f, 0f, -totalHeight * 0.5f);
            }

            // Enregistrer l'undo group
            Undo.SetCurrentGroupName("Arrange in Grid");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < selected.Length; i++)
            {
                int row = i / cols;
                int col = i % cols;

                Vector3 newPosition = startPos + offset + new Vector3(
                    col * spacingX,
                    0f,
                    row * spacingY
                );

                Undo.RecordObject(selected[i], "Grid Arrange");
                selected[i].position = newPosition;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[GridArranger] {selected.Length} objets arranges en grille " +
                      $"({cols} colonnes, {rows} rangees).");
        }
    }
}
