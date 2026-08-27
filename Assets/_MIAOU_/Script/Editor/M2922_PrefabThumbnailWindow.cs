using System.IO;
using UnityEditor;
using UnityEngine;

namespace M2922.Editor
{
    /// <summary>
    /// Fenêtre d'édition : générateur de vignettes de préfabs avec
    /// PRÉVISUALISATION live. On référence un préfab, on voit l'aperçu en
    /// temps réel, on règle yaw / pitch / zoom et le fond (transparent ou
    /// couleur unie), puis on sauvegarde "<Nom> Icon.png" à côté du préfab,
    /// importé en Sprite. Menu : M2922/Prefab Thumbnail Generator.
    /// </summary>
    public class M2922_PrefabThumbnailWindow : EditorWindow
    {
        private const int THUMBNAIL_SIZE = 256;
        private const string PREF_PREFIX = "M2922_PrefabThumbnailWindow.V2.";

        private const float DEFAULT_YAW = -35f;   // vue 3/4 classique
        private const float DEFAULT_PITCH = 18f;
        private const float DEFAULT_ZOOM = 1f;

        private GameObject _prefab;
        private float _yaw = DEFAULT_YAW;
        private float _pitch = DEFAULT_PITCH;
        private float _roll = 0f;
        private float _zoom = DEFAULT_ZOOM;
        private bool _transparentBackground = true;
        private Color _backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

        private PreviewRenderUtility _preview;
        private GameObject _instance;
        private GameObject _instancePrefab;
        private Texture2D _checker;

        [MenuItem("M2922/Prefab Thumbnail Generator")]
        public static void ShowWindow()
        {
            M2922_PrefabThumbnailWindow window = GetWindow<M2922_PrefabThumbnailWindow>();
            window.titleContent = new GUIContent("Prefab Thumbnail");
            window.minSize = new Vector2(380f, 640f);
        }

        private void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            _checker = CreateCheckerTexture();
            LoadSettings();
        }

        private void OnDisable()
        {
            try { SaveSettings(); } catch { }
            DestroyInstance();
            if (_preview != null)
            {
                _preview.Cleanup();
                _preview = null;
            }
            if (_checker != null)
            {
                Object.DestroyImmediate(_checker);
                _checker = null;
            }
        }

        // =============================================
        //  PERSISTANCE (survit aux recompilations)
        // =============================================

        private void LoadSettings()
        {
            _yaw = EditorPrefs.GetFloat(PREF_PREFIX + "Yaw", DEFAULT_YAW);
            _pitch = EditorPrefs.GetFloat(PREF_PREFIX + "Pitch", DEFAULT_PITCH);
            _roll = EditorPrefs.GetFloat(PREF_PREFIX + "Roll", 0f);
            _zoom = EditorPrefs.GetFloat(PREF_PREFIX + "Zoom", DEFAULT_ZOOM);
            _transparentBackground = EditorPrefs.GetBool(PREF_PREFIX + "Transparent", true);

            string hex = EditorPrefs.GetString(PREF_PREFIX + "BgColor",
                ColorUtility.ToHtmlStringRGBA(_backgroundColor));
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color parsed))
                _backgroundColor = parsed;

            string prefabPath = EditorPrefs.GetString(PREF_PREFIX + "PrefabPath", "");
            if (!string.IsNullOrEmpty(prefabPath))
                _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetFloat(PREF_PREFIX + "Yaw", _yaw);
            EditorPrefs.SetFloat(PREF_PREFIX + "Pitch", _pitch);
            EditorPrefs.SetFloat(PREF_PREFIX + "Roll", _roll);
            EditorPrefs.SetFloat(PREF_PREFIX + "Zoom", _zoom);
            EditorPrefs.SetBool(PREF_PREFIX + "Transparent", _transparentBackground);
            EditorPrefs.SetString(PREF_PREFIX + "BgColor", ColorUtility.ToHtmlStringRGBA(_backgroundColor));

            string path = _prefab != null ? AssetDatabase.GetAssetPath(_prefab) : "";
            EditorPrefs.SetString(PREF_PREFIX + "PrefabPath", path);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab", _prefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                _prefab = newPrefab;
                InvalidateInstance();
            }

            EditorGUILayout.Space();

            // ---- PRÉVIEW LIVE ----
            float previewSize = Mathf.Clamp(position.width - 20f, 128f, 400f);
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(true));
            DrawPreview(previewRect);

            EditorGUILayout.Space();

            // ---- RÉGLAGES CAMÉRA / FOND ----
            _yaw = EditorGUILayout.Slider("Yaw (rotation horizontale)", _yaw, -180f, 180f);
            _pitch = EditorGUILayout.Slider("Pitch (élévation)", _pitch, -89f, 89f);
            _roll = EditorGUILayout.Slider("Roll (axe de visée)", _roll, -180f, 180f);
            _zoom = EditorGUILayout.Slider("Zoom (distance caméra)", _zoom, 0.2f, 5f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Vue 3/4", GUILayout.Height(22f)))
            {
                _yaw = DEFAULT_YAW;
                _pitch = DEFAULT_PITCH;
                _roll = 0f;
                _zoom = DEFAULT_ZOOM;
                Repaint();
            }
            if (GUILayout.Button("Face", GUILayout.Height(22f)))
            {
                _yaw = 0f;
                _pitch = 0f;
                _roll = 0f;
                _zoom = DEFAULT_ZOOM;
                Repaint();
            }
            if (GUILayout.Button("Side", GUILayout.Height(22f)))
            {
                _yaw = -90f;
                _pitch = 0f;
                _roll = 0f;
                _zoom = DEFAULT_ZOOM;
                Repaint();
            }
            if (GUILayout.Button("Dessus", GUILayout.Height(22f)))
            {
                _yaw = 0f;
                _pitch = 89f;
                _roll = 0f;
                _zoom = DEFAULT_ZOOM;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            _transparentBackground = EditorGUILayout.Toggle("Fond transparent", _transparentBackground);
            if (!_transparentBackground)
                _backgroundColor = EditorGUILayout.ColorField("Couleur de fond", _backgroundColor);

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(_prefab == null);
            if (GUILayout.Button("Save Thumbnail", GUILayout.Height(30f)))
                SaveThumbnail();
            EditorGUI.EndDisabledGroup();

            if (_prefab == null)
                EditorGUILayout.HelpBox("Glissez un préfab dans le champ ci-dessus pour voir l'aperçu.", MessageType.Info);
        }

        // =============================================
        //  PRÉVIEW LIVE
        // =============================================

        private void DrawPreview(Rect r)
        {
            if (_prefab == null || _preview == null)
            {
                EditorGUI.HelpBox(r, "Aucun préfab référencé.", MessageType.Info);
                return;
            }

            // Fond : damier (transparence) ou couleur unie.
            if (_transparentBackground)
                GUI.DrawTextureWithTexCoords(r, _checker,
                    new Rect(0f, 0f, r.width / _checker.width, r.height / _checker.height));
            else
                EditorGUI.DrawRect(r, _backgroundColor);

            EnsureInstance();
            if (_instance == null)
            {
                EditorGUI.HelpBox(new Rect(r.x, r.y + 4f, r.width, 40f),
                    "Impossible d'instancier le préfab.", MessageType.Warning);
                return;
            }

            try
            {
                Texture previewTex = RenderPreviewToTexture(r);
                if (previewTex != null)
                    GUI.DrawTexture(r, previewTex, ScaleMode.StretchToFill, false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[M2922] Erreur de rendu de l'aperçu : {e.Message}");
                EditorGUI.HelpBox(new Rect(r.x, r.y + 4f, r.width, 44f),
                    "Erreur de rendu : " + e.Message, MessageType.Error);
            }
        }

        /// <summary>
        /// Rend la scène de préview via le flux officiel de PreviewRenderUtility
        /// (BeginPreview → Render → EndPreview) et retourne le RenderTexture.
        /// </summary>
        private Texture RenderPreviewToTexture(Rect r)
        {
            float aspect = r.width / Mathf.Max(1f, r.height);
            _preview.BeginPreview(r, GUIStyle.none);
            PositionCamera(aspect);
            _preview.camera.Render();
            return _preview.EndPreview();
        }

        // =============================================
        //  INSTANCE DE PRÉVIEW + CAMÉRA
        // =============================================

        private void EnsureInstance()
        {
            if (_prefab == null || _instancePrefab == _prefab) return;
            DestroyInstance();

            _instance = Object.Instantiate(_prefab);

            // Révèle tout le préfab (pickups / accessoires inactifs).
            foreach (Transform t in _instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);

            // ⚠ AddSingleGO réinitialise la transform du GO (position 0,
            // rotation identité) : on préserve l'orientation du préfab.
            Quaternion prefabRotation = _instance.transform.localRotation;
            _preview.AddSingleGO(_instance);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = prefabRotation;

            _instancePrefab = _prefab;
        }

        private void InvalidateInstance()
        {
            DestroyInstance();
            Repaint();
        }

        private void DestroyInstance()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
                _instance = null;
            }
            _instancePrefab = null;
        }

        /// <summary>Place la caméra de préview selon yaw / pitch / zoom et l'AABB projeté.</summary>
        private void PositionCamera(float aspect = 1f)
        {
            if (_instance == null || _preview == null) return;

            Bounds bounds = ComputeBounds(_instance);
            Vector3 center = bounds.center;
            Vector3 ext = bounds.extents;
            if (ext.sqrMagnitude < 0.000001f)
                ext = Vector3.one * 0.5f; // bounds dégénérées (aucun renderer) → cadrage par défaut

            Camera cam = _preview.camera;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _transparentBackground
                ? new Color(0f, 0f, 0f, 0f)
                : _backgroundColor;

            // Vue yaw / pitch / ROLL : le roll (3e angle d'Euler) bascule la
            // caméra autour de son axe de visée, sans changer la direction.
            Quaternion viewRot = Quaternion.Euler(_pitch, _yaw, _roll);
            Vector3 viewDir = viewRot * Vector3.back;
            Vector3 camDir = -viewDir;
            Vector3 camUp = viewRot * Vector3.up;
            Vector3 camRight = Vector3.Cross(camUp, camDir).normalized;

            // Cadrage serré : demi-extents projetés sur les axes de la caméra,
            // corrigé par l'ASPECT du rect pour que l'objet reste centré et
            // entier même dans une zone non carrée.
            float halfW = ProjectHalfSize(ext, camRight);
            float halfH = ProjectHalfSize(ext, camUp);
            float halfD = ProjectHalfSize(ext, camDir);

            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float requiredHalfH = Mathf.Max(halfH, halfW / Mathf.Max(0.0001f, aspect));
            float distance = halfD + requiredHalfH / tanHalf;
            distance = Mathf.Max(distance * _zoom, 0.05f);

            cam.transform.position = center + viewDir * distance;
            // LookRotation avec l'up ROLLÉ : le centre de l'AABB reste exactement
            // au centre de l'image, le roll tourne autour de l'axe de visée.
            cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position, camUp);
        }

        // =============================================
        //  SAUVEGARDE
        // =============================================

        private void SaveThumbnail()
        {
            if (_prefab == null) return;

            EnsureInstance();
            if (_instance == null)
            {
                Debug.LogWarning($"[M2922] Impossible d'instancier '{_prefab.name}'.");
                return;
            }

            Texture2D thumbnail = RenderSnapshot();
            if (thumbnail == null) return;

            string assetPath = AssetDatabase.GetAssetPath(_prefab);
            string directory = Path.GetDirectoryName(assetPath);
            string fileName = Path.GetFileNameWithoutExtension(assetPath) + " Icon.png";
            string filePath = Path.Combine(directory, fileName);

            File.WriteAllBytes(filePath, thumbnail.EncodeToPNG());
            Object.DestroyImmediate(thumbnail);

            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Debug.Log($"[M2922] Vignette sauvegardée : {fileName}",
                AssetDatabase.LoadAssetAtPath<Texture2D>(filePath));
        }

        /// <summary>Rend la vignette finale (THUMBNAIL_SIZE) et la retourne en Texture2D.</summary>
        private Texture2D RenderSnapshot()
        {
            if (_preview == null || _instance == null) return null;

            int size = THUMBNAIL_SIZE;
            Texture previewTex = RenderPreviewToTexture(new Rect(0f, 0f, size, size));
            if (!(previewTex is RenderTexture rt)) return null;

            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            return result;
        }

        // =============================================
        //  UTILITAIRES
        // =============================================

        /// <summary>
        /// Demi-taille projetée de l'AABB (extents) sur un axe unitaire :
        /// Σ |extent·axis| — sert au cadrage serré de la caméra.
        /// </summary>
        private static float ProjectHalfSize(Vector3 ext, Vector3 axis)
        {
            return Mathf.Abs(ext.x * axis.x) + Mathf.Abs(ext.y * axis.y) + Mathf.Abs(ext.z * axis.z);
        }

        /// <summary>
        /// Bounds englobant les MESHES du préfab (particules / trails / lines
        /// exclus : leurs bounds parasites décentrent l'AABB et faussent le
        /// cadrage). Fallback : tout renderer.
        /// </summary>
        private static Bounds ComputeBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

            Bounds bounds = default;
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude < 0.000001f) continue;
                if (!found) { bounds = b; found = true; }
                else bounds.Encapsulate(b);
            }

            if (!found)
                return new Bounds(go.transform.position, Vector3.one);
            return bounds;
        }

        /// <summary>Texture damier (représente la transparence dans l'aperçu).</summary>
        private static Texture2D CreateCheckerTexture()
        {
            const int cell = 12;
            const int size = cell * 2;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Point;

            Color a = new Color(0.35f, 0.35f, 0.35f, 1f);
            Color b = new Color(0.55f, 0.55f, 0.55f, 1f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = ((x / cell) + (y / cell)) % 2 == 0 ? a : b;

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
