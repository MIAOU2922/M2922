using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.Combat;
using M2922.Core;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_Weapon))]
    public class M2922_WeaponEditor : UnityEditor.Editor
    {
        // ── Identity ──────────────────────────────────────────────────────────
        private SerializedProperty _weaponDisplayNameProp;
        private SerializedProperty _weaponTypeProp;

        // ── Damage ────────────────────────────────────────────────────────────
        private SerializedProperty _damageTypesProp;
        private SerializedProperty _baseDamageAmountsProp;
        private SerializedProperty _critMultiplierProp;

        // ── Fire ──────────────────────────────────────────────────────────────
        private SerializedProperty _fireRateProp;
        private SerializedProperty _rangeProp;
        private SerializedProperty _hitLayersProp;
        private SerializedProperty _useProjectileProp;

        // ── Projectile pool ────────────────────────────────────────────────────
        private SerializedProperty _projectilePoolProp;
        private SerializedProperty _projectileSpeedProp;

        // ── Ammo ──────────────────────────────────────────────────────────────
        private SerializedProperty _baseMagSizeProp;
        private SerializedProperty _baseReserveSizeProp;
        private SerializedProperty _infiniteAmmoProp;

        // ── Reload ────────────────────────────────────────────────────────────
        private SerializedProperty _reloadModeProp;
        private SerializedProperty _autoReloadDelayProp;
        private SerializedProperty _reloadTimeProp;
        private SerializedProperty _loseBulletsOnReloadProp;

        // ── Spread ────────────────────────────────────────────────────────────
        private SerializedProperty _baseMaxSpreadProp;
        private SerializedProperty _baseSpreadPerShotProp;
        private SerializedProperty _spreadRecoveryRateProp;
        private SerializedProperty _resetSpreadOnReloadProp;

        // ── VFX / SFX ─────────────────────────────────────────────────────────
        private SerializedProperty _muzzleFlashProp;
        private SerializedProperty _fireAudioProp;
        private SerializedProperty _reloadAudioProp;
        private SerializedProperty _emptyAudioProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _damageOpen   = true;
        private bool _fireOpen     = true;
        private bool _ammoOpen     = true;
        private bool _reloadOpen   = true;
        private bool _spreadOpen   = true;
        private bool _vfxOpen      = false;
        private bool _poolOpen     = true;

        // ── Pool generator ────────────────────────────────────────────────────
        private GameObject    _projectilePrefab;
        private int           _poolSize = 10;
        private static GUIStyle _boxStyle;

        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _weaponDisplayNameProp    = serializedObject.FindProperty("_weaponDisplayName");
            _weaponTypeProp           = serializedObject.FindProperty("_weaponType");
            _damageTypesProp          = serializedObject.FindProperty("_damageTypes");
            _baseDamageAmountsProp    = serializedObject.FindProperty("_baseDamageAmounts");
            _critMultiplierProp       = serializedObject.FindProperty("_critMultiplier");
            _fireRateProp             = serializedObject.FindProperty("_fireRate");
            _rangeProp                = serializedObject.FindProperty("_range");
            _hitLayersProp            = serializedObject.FindProperty("_hitLayers");
            _useProjectileProp        = serializedObject.FindProperty("_useProjectile");
            _projectilePoolProp       = serializedObject.FindProperty("_projectilePool");
            _projectileSpeedProp      = serializedObject.FindProperty("_projectileSpeed");
            _baseMagSizeProp          = serializedObject.FindProperty("_baseMagSize");
            _baseReserveSizeProp      = serializedObject.FindProperty("_baseReserveSize");
            _infiniteAmmoProp         = serializedObject.FindProperty("_infiniteAmmo");
            _reloadModeProp           = serializedObject.FindProperty("_reloadMode");
            _autoReloadDelayProp      = serializedObject.FindProperty("_autoReloadDelay");
            _reloadTimeProp           = serializedObject.FindProperty("_reloadTime");
            _loseBulletsOnReloadProp  = serializedObject.FindProperty("_loseBulletsOnReload");
            _baseMaxSpreadProp        = serializedObject.FindProperty("_baseMaxSpread");
            _baseSpreadPerShotProp    = serializedObject.FindProperty("_baseSpreadPerShot");
            _spreadRecoveryRateProp   = serializedObject.FindProperty("_spreadRecoveryRate");
            _resetSpreadOnReloadProp  = serializedObject.FindProperty("_resetSpreadOnReload");
            _muzzleFlashProp          = serializedObject.FindProperty("_muzzleFlash");
            _fireAudioProp            = serializedObject.FindProperty("_fireAudio");
            _reloadAudioProp          = serializedObject.FindProperty("_reloadAudio");
            _emptyAudioProp           = serializedObject.FindProperty("_emptyAudio");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 6) };
            }

            serializedObject.Update();
            M2922_Weapon weapon = (M2922_Weapon)target;

            // =================================================================
            // IDENTITY
            // =================================================================
            EditorGUILayout.BeginVertical(_boxStyle);
            EditorGUILayout.LabelField("IDENTITY", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_weaponDisplayNameProp, new GUIContent("Weapon Name"));
            EditorGUILayout.PropertyField(_weaponTypeProp,        new GUIContent("Weapon Type"));
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                EditorGUILayout.LabelField("RUNTIME STATUS", EditorStyles.boldLabel);

                // Ammo bar
                float ammoRatio = weapon.MaxAmmo > 0 && weapon.CurrentAmmo != int.MaxValue
                    ? (float)weapon.CurrentAmmo / weapon.MaxAmmo : 1f;
                Rect ammoRect = EditorGUILayout.GetControlRect(false, 18f);
                string ammoLabel = weapon.CurrentAmmo == int.MaxValue
                    ? "∞ / ∞"
                    : $"{weapon.CurrentAmmo} / {weapon.MaxAmmo}  (réserve: {weapon.ReserveAmmo})";
                EditorGUI.ProgressBar(ammoRect, ammoRatio, ammoLabel);

                // Spread bar
                if (weapon.MaxSpread > 0f)
                {
                    float spreadRatio = weapon.MaxSpread > 0f ? weapon.CurrentSpread / weapon.MaxSpread : 0f;
                    Rect spreadRect = EditorGUILayout.GetControlRect(false, 18f);
                    EditorGUI.ProgressBar(spreadRect, spreadRatio,
                        $"Spread: {weapon.CurrentSpread:F2}° / {weapon.MaxSpread:F2}°");
                }

                EditorGUILayout.LabelField("Reloading",   weapon.IsReloading.ToString());
                EditorGUILayout.LabelField("Can Fire",    weapon.CanFire.ToString());
                EditorGUILayout.LabelField("Total DPS",   $"{weapon.TotalDamage * weapon.FireRate:F1} dmg/s");

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Force Reload")) weapon.TriggerReload();
                if (GUILayout.Button("+30 ammo"))     weapon.AddAmmo(30);
                if (GUILayout.Button("Infinite ON"))  weapon.SetInfiniteAmmo(true);
                if (GUILayout.Button("Infinite OFF")) weapon.SetInfiniteAmmo(false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Spread ×2"))    weapon.SetSpreadMultiplier(2f);
                if (GUILayout.Button("Spread ×0"))    weapon.SetSpreadMultiplier(0f);
                if (GUILayout.Button("Spread reset")) weapon.SetSpreadMultiplier(1f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(8);
            }

            // =================================================================
            // DAMAGE
            // =================================================================
            _damageOpen = Section("DAMAGE", _damageOpen, () =>
            {
                // Tableaux synchronisés (affichage côte-à-côte)
                int typeCount = _damageTypesProp.arraySize;
                int amtCount  = _baseDamageAmountsProp.arraySize;
                int rows      = Mathf.Max(typeCount, amtCount);

                bool mismatch = typeCount != amtCount;
                if (mismatch)
                    EditorGUILayout.HelpBox("_damageTypes et _baseDamageAmounts doivent avoir la même longueur !", MessageType.Warning);

                // Adjust sizes together
                int newRows = Mathf.Max(0, EditorGUILayout.IntField("Entries", rows));
                if (newRows != rows)
                {
                    _damageTypesProp.arraySize       = newRows;
                    _baseDamageAmountsProp.arraySize  = newRows;
                }

                for (int i = 0; i < newRows; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(
                        _damageTypesProp.GetArrayElementAtIndex(i),
                        GUIContent.none, GUILayout.Width(100));
                    EditorGUILayout.PropertyField(
                        _baseDamageAmountsProp.GetArrayElementAtIndex(i),
                        GUIContent.none);
                    EditorGUILayout.LabelField("dmg", EditorStyles.miniLabel, GUILayout.Width(28));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.PropertyField(_critMultiplierProp, new GUIContent("Crit Multiplier (tag 'Crit')"));

                if (Application.isPlaying)
                    EditorGUILayout.LabelField($"Total dégâts effectifs: {weapon.TotalDamage:F1}", EditorStyles.miniLabel);
            });

            // =================================================================
            // FIRE SETTINGS
            // =================================================================
            _fireOpen = Section("FIRE", _fireOpen, () =>
            {
                EditorGUILayout.PropertyField(_fireRateProp,       new GUIContent("Fire Rate (shots/s)"));
                EditorGUILayout.PropertyField(_useProjectileProp,  new GUIContent("Use Projectile (vs Raycast)"));

                if (!_useProjectileProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_rangeProp,     new GUIContent("Range (m)"));
                    EditorGUILayout.PropertyField(_hitLayersProp, new GUIContent("Hit Layers"));
                }

                // DPS helper
                if (_fireRateProp.floatValue > 0f && Application.isPlaying)
                    EditorGUILayout.LabelField($"DPS estimé: {weapon.TotalDamage * _fireRateProp.floatValue:F1}", EditorStyles.miniLabel);
            });

            // =================================================================
            // PROJECTILE POOL
            // =================================================================
            if (_useProjectileProp.boolValue)
            {
                _poolOpen = Section("PROJECTILE POOL", _poolOpen, () =>
                {
                    EditorGUILayout.PropertyField(_projectilePoolProp,  new GUIContent("Projectile Pool"));
                    EditorGUILayout.PropertyField(_projectileSpeedProp, new GUIContent("Speed (m/s)"));

                    GUILayout.Space(6);
                    DrawPoolGenerator(weapon);
                });
            }

            // =================================================================
            // AMMO
            // =================================================================
            _ammoOpen = Section("AMMO", _ammoOpen, () =>
            {
                EditorGUILayout.PropertyField(_infiniteAmmoProp,     new GUIContent("Infinite Ammo"));
                if (!_infiniteAmmoProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_baseMagSizeProp,     new GUIContent("Base Mag Size"));
                    EditorGUILayout.PropertyField(_baseReserveSizeProp, new GUIContent("Base Reserve Size"));
                }
            });

            // =================================================================
            // RELOAD
            // =================================================================
            _reloadOpen = Section("RELOAD", _reloadOpen, () =>
            {
                EditorGUILayout.PropertyField(_reloadModeProp,          new GUIContent("Reload Mode"));
                EditorGUILayout.PropertyField(_reloadTimeProp,          new GUIContent("Reload Time (s)"));
                EditorGUILayout.PropertyField(_loseBulletsOnReloadProp, new GUIContent("Lose Bullets On Reload"));

                ReloadMode mode = (ReloadMode)_reloadModeProp.enumValueIndex;
                if (mode == ReloadMode.Auto)
                    EditorGUILayout.PropertyField(_autoReloadDelayProp, new GUIContent("Auto Reload Delay (s)"));

                if (mode == ReloadMode.MagSwap)
                    EditorGUILayout.HelpBox(
                        "MagSwap : appeler MagSwap(int) depuis le prop chargeur physique.", MessageType.Info);

                if (mode == ReloadMode.Manual)
                    EditorGUILayout.HelpBox(
                        "Manual : trigger opposé en VR, touche [R] en desktop. Géré par M2922_WeaponEntity.", MessageType.Info);
            });

            // =================================================================
            // SPREAD
            // =================================================================
            _spreadOpen = Section("SPREAD", _spreadOpen, () =>
            {
                EditorGUILayout.PropertyField(_baseMaxSpreadProp,       new GUIContent("Max Spread (°)"));

                bool hasSpread = _baseMaxSpreadProp.floatValue > 0f;
                EditorGUI.BeginDisabledGroup(!hasSpread);
                EditorGUILayout.PropertyField(_baseSpreadPerShotProp,   new GUIContent("Spread Per Shot (°)"));
                EditorGUILayout.PropertyField(_spreadRecoveryRateProp,  new GUIContent("Recovery Rate (°/s)"));
                EditorGUILayout.PropertyField(_resetSpreadOnReloadProp, new GUIContent("Reset On Reload"));
                EditorGUI.EndDisabledGroup();

                if (!hasSpread)
                    EditorGUILayout.HelpBox("Max Spread = 0 → aucune dispersion (raycast parfaitement droit).", MessageType.None);
                else
                {
                    // Estimation temps pour atteindre le max
                    float perShot = _baseSpreadPerShotProp.floatValue;
                    float maxSpr  = _baseMaxSpreadProp.floatValue;
                    float rate    = _fireRateProp.floatValue;
                    if (perShot > 0f && rate > 0f)
                    {
                        float shotsToMax   = maxSpr / perShot;
                        float timeToMax    = shotsToMax / rate;
                        EditorGUILayout.LabelField(
                            $"Max spread atteint après ~{shotsToMax:F0} tirs ({timeToMax:F1}s en rafale)",
                            EditorStyles.miniLabel);
                    }

                    // Estimation temps récup 0 → max si on arrête de tirer
                    float recovRate = _spreadRecoveryRateProp.floatValue;
                    if (recovRate > 0f)
                        EditorGUILayout.LabelField(
                            $"Retour à 0° en ~{maxSpr / recovRate:F1}s sans tirer",
                            EditorStyles.miniLabel);
                }
            });

            // =================================================================
            // VFX / SFX
            // =================================================================
            _vfxOpen = Section("VFX / SFX", _vfxOpen, () =>
            {
                EditorGUILayout.PropertyField(_muzzleFlashProp,  new GUIContent("Muzzle Flash"));
                EditorGUILayout.PropertyField(_fireAudioProp,    new GUIContent("Fire Sound"));
                EditorGUILayout.PropertyField(_reloadAudioProp,  new GUIContent("Reload Sound"));
                EditorGUILayout.PropertyField(_emptyAudioProp,   new GUIContent("Empty Sound"));
            });

            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // POOL GENERATOR
        // =====================================================================

        private void DrawPoolGenerator(M2922_Weapon weapon)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Générateur de pool", EditorStyles.boldLabel);

            _projectilePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab projectile", _projectilePrefab, typeof(GameObject), false);

            _poolSize = Mathf.Max(1, EditorGUILayout.IntField("Taille du pool", _poolSize));

            bool canGenerate = _projectilePrefab != null
                && _projectilePrefab.GetComponent<M2922_Projectile>() != null;

            if (_projectilePrefab != null && !canGenerate)
                EditorGUILayout.HelpBox("Le prefab doit avoir un composant M2922_Projectile.", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(!canGenerate || Application.isPlaying);

            if (GUILayout.Button("Générer pool en child"))
                GeneratePool(weapon);

            EditorGUI.EndDisabledGroup();

            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Génération disponible uniquement en Edit mode.", MessageType.Info);

            // Affiche pool existant
            int existing = _projectilePoolProp.arraySize;
            if (existing > 0)
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField($"Pool actuel : {existing} slot(s)", EditorStyles.miniLabel);
                if (!Application.isPlaying && GUILayout.Button("Vider pool (remove slots)"))
                    ClearPool(weapon);
            }

            EditorGUILayout.EndVertical();
        }

        private void GeneratePool(M2922_Weapon weapon)
        {
            // Trouve ou crée le conteneur "ProjectilePool" en child
            Transform parent = weapon.transform.Find("ProjectilePool");
            if (parent == null)
            {
                GameObject container = new GameObject("ProjectilePool");
                Undo.RegisterCreatedObjectUndo(container, "Create Projectile Pool");
                container.transform.SetParent(weapon.transform, false);
                parent = container.transform;
            }

            // Détruit les anciens enfants si on regénère
            int childCount = parent.childCount;
            for (int i = childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);

            M2922_Projectile[] pool = new M2922_Projectile[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(_projectilePrefab, parent);
                go.name = $"Projectile_{i:00}";
                go.SetActive(false);
                Undo.RegisterCreatedObjectUndo(go, "Create Pool Projectile");
                pool[i] = go.GetComponent<M2922_Projectile>();
            }

            // Injecte dans la propriété sérialisée
            serializedObject.Update();
            _projectilePoolProp.arraySize = _poolSize;
            for (int i = 0; i < _poolSize; i++)
            {
                SerializedProperty element = _projectilePoolProp.GetArrayElementAtIndex(i);
                element.objectReferenceValue = pool[i];
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(weapon.gameObject);

            Debug.Log($"[M2922_WeaponEditor] Pool de {_poolSize} projectiles créée sous '{parent.name}'.");
        }

        private void ClearPool(M2922_Weapon weapon)
        {
            serializedObject.Update();
            _projectilePoolProp.arraySize = 0;
            serializedObject.ApplyModifiedProperties();

            Transform parent = weapon.transform.Find("ProjectilePool");
            if (parent != null)
            {
                int childCount = parent.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static bool Section(string title, bool open, System.Action content)
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
