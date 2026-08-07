using UnityEngine;
using UnityEditor;

namespace M2922.Component.Weapon.Editor
{
    /// <summary>
    /// Inspector custom pour M2922_Weapon.
    /// Le baking est fait DIRECTEMENT par l'éditeur via SerializedObject,
    /// sans dépendre de la méthode #if-guarded BakeWeaponData().
    /// </summary>
    [CustomEditor(typeof(M2922_Weapon))]
    [CanEditMultipleObjects]
    public class M2922_WeaponEditor : UnityEditor.Editor
    {
        private M2922_Weapon _target;
        private SerializedObject _so;

        // Cache des noms pour les popups (reconstruit après chaque bake)
        private string[][] _poolDisplayNames;
        private bool _needsRefreshDisplayNames = true;

        private void OnEnable()
        {
            _target = (M2922_Weapon)target;
            _so = new SerializedObject(_target);
            _needsRefreshDisplayNames = true;
        }

        public override void OnInspectorGUI()
        {
            _so.Update();

            WeaponDefinition prevDef = _target._weaponDefinition;
            _target._weaponDefinition = (WeaponDefinition)EditorGUILayout.ObjectField(
                "Weapon Definition (Source)",
                _target._weaponDefinition,
                typeof(WeaponDefinition),
                false
            );

            bool doBake = (_target._weaponDefinition != prevDef);

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake Weapon Data", GUILayout.Height(25)))
                doBake = true;

            if (doBake && _target._weaponDefinition != null)
            {
                BakeFromDefinition(_target._weaponDefinition);
                _needsRefreshDisplayNames = true;
            }

            // Rafraîchir après bake
            _so.Update();

            string bakedName = SP("_bakedWeaponName")?.stringValue;
            bool hasBakedData = !string.IsNullOrEmpty(bakedName)
                || !string.IsNullOrEmpty(SP("_bakedFrameName")?.stringValue);
            if (hasBakedData)
            {
                // ---- BAKED DATA ----
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("=== BAKED DATA ===", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Name", bakedName);
                EditorGUILayout.LabelField("Type", ((WeaponType)IV("_bakedWeaponType")).ToString());
                EditorGUILayout.LabelField("Damage", ((DamageType)IV("_bakedDamageType")).ToString());
                EditorGUILayout.LabelField("Slot", ((WeaponSlot)IV("_bakedSlot")).ToString());
                EditorGUILayout.LabelField("Rarity", ((WeaponRarity)IV("_bakedRarity")).ToString());
                EditorGUILayout.LabelField("Frame", SP("_bakedFrameName")?.stringValue);
                EditorGUILayout.LabelField("Magazine", IV("_bakedFrameMagazine").ToString());
                EditorGUILayout.LabelField("RPM", FV("_bakedFrameRPM").ToString("F0"));

                float baseImpact = FV("_bakedFrameImpact");
                float baseRange = FV("_bakedFrameRange");
                float baseZoom = FV("_bakedFrameZoom");
                WeaponType wt = (WeaponType)IV("_bakedWeaponType");
                float dmgMult = GetRawDamageMultiplier(wt);
                float effRange = GetEffectiveRange(wt, baseRange, baseZoom);

                EditorGUILayout.LabelField("Impact (frame)", $"{baseImpact:F1}  →  raw dmg: {baseImpact * dmgMult:F1}");
                EditorGUILayout.LabelField("Range  (frame)", $"{baseRange:F1}  →  portee: {effRange:F1}m  falloff@{effRange * 0.4f:F1}m");
                EditorGUILayout.LabelField("Stability", FV("_bakedFrameStability").ToString("F1"));
                EditorGUILayout.LabelField("Handling", FV("_bakedFrameHandling").ToString("F1"));
                EditorGUILayout.LabelField("Reload", FV("_bakedFrameReloadSpeed").ToString("F1"));
                EditorGUILayout.LabelField("AA", FV("_bakedFrameAimAssistance").ToString("F1"));
                EditorGUILayout.LabelField("RecoilDir", FV("_bakedFrameRecoilDirection").ToString("F1"));
                EditorGUILayout.LabelField("Airborne", FV("_bakedFrameAirborneEffectiveness").ToString("F1"));
                EditorGUILayout.LabelField("Reload Style", ((ReloadStyle)IV("_bakedFrameReloadStyle")).ToString());

                // ---- LAUNCHER STATS (si BlastRadius > 0) ----
                float blastStat = FV("_bakedFrameBlastRadius");
                float velStat = FV("_bakedFrameVelocity");
                if (blastStat > 0f)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("=== LAUNCHER STATS ===", EditorStyles.boldLabel);

                    float launcherMult = GetLauncherMult((WeaponType)IV("_bakedWeaponType"));
                    float launcherBaseImpact = baseImpact * launcherMult;

                    float splashRatio = Mathf.Lerp(0.20f, 0.80f, blastStat / 100f);
                    float directRatio = 1f - splashRatio;
                    float velMult = 1f + (velStat / 100f) * 0.5f;

                    float directDmg = (launcherBaseImpact * directRatio) * velMult;
                    float splashDmg = launcherBaseImpact * splashRatio;
                    float explRadius = Mathf.Lerp(2.0f, 8.0f, blastStat / 100f);
                    float projSpeed = 15f + velStat * 0.5f;

                    Color old = GUI.color;
                    GUI.color = new Color(1f, 0.7f, 0.3f);
                    EditorGUILayout.LabelField("Base (Impact × Mult)", $"{baseImpact:F1} × {launcherMult:F1} = {launcherBaseImpact:F1}");
                    EditorGUILayout.LabelField("Direct Impact", $"{directDmg:F1}  (ratio: {directRatio:P0}, vel mult: ×{velMult:F2})");
                    EditorGUILayout.LabelField("Splash Damage", $"{splashDmg:F1}  (ratio: {splashRatio:P0})");
                    EditorGUILayout.LabelField("Explosion Radius", $"{explRadius:F1}m");
                    EditorGUILayout.LabelField("Projectile Speed", $"{projSpeed:F0} m/s");
                    EditorGUILayout.LabelField("Blast Radius (stat)", $"{blastStat:F1}");
                    EditorGUILayout.LabelField("Velocity (stat)", $"{velStat:F1}");
                    GUI.color = old;
                }

                // ---- AMMO SETTINGS (editable) ----
                EditorGUILayout.Space();
                SerializedProperty infiniteAmmoProp = _so.FindProperty("_infiniteAmmo");
                if (infiniteAmmoProp != null)
                    EditorGUILayout.PropertyField(infiniteAmmoProp, new GUIContent("Infinite Ammo"));

                // ---- PREVIEW SELECTION ----
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("=== PREVIEW SELECTION ===", EditorStyles.boldLabel);

                RefreshDisplayNames();

                DrawPoolPopup("Perk 1", "_previewPerk1Index", 0);
                DrawPoolPopup("Perk 2", "_previewPerk2Index", 1);
                DrawPoolPopup("Perk 3", "_previewPerk3Index", 2);
                DrawPoolPopup("Perk 4", "_previewPerk4Index", 3);
                DrawPoolPopup("Masterwork", "_previewMasterworkIndex", 4);
                DrawPoolPopup("Mod", "_previewModIndex", 5);

                // ---- COMPUTED PREVIEW STATS ----
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("=== COMPUTED STATS (preview) ===", EditorStyles.boldLabel);

                int[] previewIndices = {
                    IV("_previewPerk1Index"), IV("_previewPerk2Index"),
                    IV("_previewPerk3Index"), IV("_previewPerk4Index"),
                    IV("_previewMasterworkIndex"), IV("_previewModIndex")
                };

                float previewImpact = ComputePreviewStat(0, previewIndices);
                float previewRange  = ComputePreviewStat(1, previewIndices);
                float previewStab   = ComputePreviewStat(2, previewIndices);
                float previewHand   = ComputePreviewStat(3, previewIndices);
                float previewReload = ComputePreviewStat(4, previewIndices);
                float previewAA     = ComputePreviewStat(5, previewIndices);
                float previewZoom   = ComputePreviewStat(6, previewIndices);
                float previewAir    = ComputePreviewStat(7, previewIndices);
                float previewRD     = ComputePreviewStat(8, previewIndices);
                float previewRPM    = ComputePreviewStat(9, previewIndices);
                float previewCharge = ComputePreviewStat(10, previewIndices);
                float previewDraw   = ComputePreviewStat(11, previewIndices);
                float previewBlast  = ComputePreviewStat(13, previewIndices);
                float previewVel    = ComputePreviewStat(14, previewIndices);
                float previewDmgMult = GetRawDamageMultiplier((WeaponType)IV("_bakedWeaponType"));
                float previewEffRange = GetEffectiveRange((WeaponType)IV("_bakedWeaponType"), previewRange, previewZoom);

                Color oldColor = GUI.color;
                GUI.color = Color.green;
                EditorGUILayout.LabelField("Impact  (reel)", $"{previewImpact:F1}  →  raw dmg: {previewImpact * previewDmgMult:F1}");
                EditorGUILayout.LabelField("Range   (reel)", $"{previewRange:F1}  →  portee: {previewEffRange:F1}m  falloff@{previewEffRange * 0.4f:F1}m");
                EditorGUILayout.LabelField("Stability", $"{previewStab:F1}");
                EditorGUILayout.LabelField("Handling", $"{previewHand:F1}");
                EditorGUILayout.LabelField("Reload", $"{previewReload:F1}");
                EditorGUILayout.LabelField("AA", $"{previewAA:F1}");
                EditorGUILayout.LabelField("RecoilDir", $"{previewRD:F1}");
                EditorGUILayout.LabelField("RPM", $"{previewRPM:F0}");
                EditorGUILayout.LabelField("Blast Radius", $"{previewBlast:F1}");
                EditorGUILayout.LabelField("Velocity", $"{previewVel:F1}");
                GUI.color = oldColor;

                // ---- LAUNCHER PREVIEW ----
                if (previewBlast > 0f)
                {
                    float launcherMult = GetLauncherMult((WeaponType)IV("_bakedWeaponType"));
                    float launcherBase = previewImpact * launcherMult;

                    float splashRatio = Mathf.Lerp(0.20f, 0.80f, previewBlast / 100f);
                    float directRatio = 1f - splashRatio;
                    float velMult = 1f + (previewVel / 100f) * 0.5f;
                    float directDmg = (launcherBase * directRatio) * velMult;
                    float splashDmg = launcherBase * splashRatio;
                    float explRadius = Mathf.Lerp(2.0f, 8.0f, previewBlast / 100f);
                    float projSpeed = 15f + previewVel * 0.5f;

                    EditorGUILayout.Space();
                    GUI.color = new Color(1f, 0.6f, 0.1f);
                    EditorGUILayout.LabelField("Base (Impact × Mult)", $"{previewImpact:F1} × {launcherMult:F1} = {launcherBase:F1}");
                    EditorGUILayout.LabelField("Dir. Impact (preview)", $"{directDmg:F1}");
                    EditorGUILayout.LabelField("Splash Dmg (preview)", $"{splashDmg:F1}");
                    EditorGUILayout.LabelField("Expl. Radius (preview)", $"{explRadius:F1}m");
                    EditorGUILayout.LabelField("Proj Speed (preview)", $"{projSpeed:F0} m/s");
                    GUI.color = oldColor;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Aucune donnee bakee. Glissez un WeaponDefinition et cliquez Bake.", MessageType.Warning);
            }

            _so.ApplyModifiedProperties();
        }

        // ===================================================
        // BAKING direct (sans appeler BakeWeaponData)
        // ===================================================

        private void BakeFromDefinition(WeaponDefinition def)
        {
            const int S = 16; // STAT_STRIDE

            SetS("_bakedWeaponName", def.WeaponName);
            SetI("_bakedWeaponType", (int)def.WeaponType);
            SetI("_bakedDamageType", (int)def.DamageType);
            SetI("_bakedSlot", (int)def.Slot);
            SetI("_bakedRarity", (int)def.Rarity);
            SetS("_bakedFrameName", def.Frame != null ? def.Frame.FrameName : "Aucune");

            if (def.Frame != null)
            {
                var fs = def.Frame.BaseStats;
                SetF("_bakedFrameImpact", fs.Impact);
                SetF("_bakedFrameRange", fs.Range);
                SetF("_bakedFrameStability", fs.Stability);
                SetF("_bakedFrameHandling", fs.Handling);
                SetF("_bakedFrameReloadSpeed", fs.ReloadSpeed);
                SetF("_bakedFrameAimAssistance", fs.AimAssistance);
                SetF("_bakedFrameZoom", fs.Zoom);
                SetF("_bakedFrameAirborneEffectiveness", fs.AirborneEffectiveness);
                SetF("_bakedFrameRecoilDirection", fs.RecoilDirection);
                SetF("_bakedFrameRPM", fs.RPM);
                SetF("_bakedFrameChargeTime", fs.ChargeTime);
                SetF("_bakedFrameDrawTime", fs.DrawTime);
                SetI("_bakedFrameMagazine", fs.Magazine);
                SetF("_bakedFrameBlastRadius", fs.BlastRadius);
                SetF("_bakedFrameVelocity", fs.Velocity);
                SetF("_bakedFrameAccuracy", fs.Accuracy);
                SetI("_bakedFrameReloadStyle", (int)def.Frame.ReloadStyle);
            }

            BakePerkPool(def.PerkColumn1Pool, "_bakedPerk1PoolNames", "_bakedPerk1PoolStats", S);
            BakePerkPool(def.PerkColumn2Pool, "_bakedPerk2PoolNames", "_bakedPerk2PoolStats", S);
            BakePerkPool(def.PerkColumn3Pool, "_bakedPerk3PoolNames", "_bakedPerk3PoolStats", S);
            BakePerkPool(def.PerkColumn4Pool, "_bakedPerk4PoolNames", "_bakedPerk4PoolStats", S);

            BakeMwPool(def.MasterworkPool, "_bakedMasterworkPoolNames", "_bakedMasterworkPoolStats", S);

            BakeModPool(def.ModPool, "_bakedModPoolNames", "_bakedModPoolStats",
                "_bakedModPoolBossDmg", "_bakedModPoolMajorDmg",
                "_bakedModPoolMinorDmg", "_bakedModPoolPlayerDmg", S);

            _so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_target);
            string frameInfo = (def.Frame != null)
                ? $"Imp={def.Frame.BaseStats.Impact} Rng={def.Frame.BaseStats.Range} Stab={def.Frame.BaseStats.Stability}"
                : "NO FRAME";
            Debug.Log("[WeaponEditor] Bake OK: " + (def.WeaponName ?? "(unnamed)")
                + " | " + frameInfo);
        }

        private void BakePerkPool(WeaponPerkData[] pool, string nProp, string sProp, int stride)
        {
            int len = (pool != null) ? pool.Length : 0;
            string[] names = new string[len];
            float[] stats = new float[len * stride];

            for (int i = 0; i < len; i++)
            {
                var p = pool[i];
                names[i] = (p != null) ? p.PerkName : "Vide";
                if (p != null) PackStats(p.StatModifiers, stats, i * stride);
            }
            SetArr(nProp, names);
            SetFArr(sProp, stats);
        }

        private void BakeMwPool(WeaponMasterworkData[] pool, string nProp, string sProp, int stride)
        {
            int len = (pool != null) ? pool.Length : 0;
            string[] names = new string[len];
            float[] stats = new float[len * stride];

            for (int i = 0; i < len; i++)
            {
                var mw = pool[i];
                names[i] = (mw != null) ? mw.MasterworkName : "Vide";
                if (mw != null) PackStats(mw.GetStatModifiers(), stats, i * stride);
            }
            SetArr(nProp, names);
            SetFArr(sProp, stats);
        }

        private void BakeModPool(WeaponModData[] pool, string nProp, string sProp,
            string bProp, string maProp, string miProp, string pProp, int stride)
        {
            int len = (pool != null) ? pool.Length : 0;
            string[] names = new string[len];
            float[] stats = new float[len * stride];
            float[] bDmg = new float[len];
            float[] maDmg = new float[len];
            float[] miDmg = new float[len];
            float[] pDmg = new float[len];

            for (int i = 0; i < len; i++)
            {
                var mod = pool[i];
                names[i] = (mod != null) ? mod.ModName : "Vide";
                bDmg[i] = (mod != null) ? mod.BossDamageMultiplier : 1f;
                maDmg[i] = (mod != null) ? mod.MajorDamageMultiplier : 1f;
                miDmg[i] = (mod != null) ? mod.MinorDamageMultiplier : 1f;
                pDmg[i] = (mod != null) ? mod.PlayerDamageMultiplier : 1f;
                if (mod != null) PackStats(mod.StatModifiers, stats, i * stride);
            }
            SetArr(nProp, names);
            SetFArr(sProp, stats);
            SetFArr(bProp, bDmg);
            SetFArr(maProp, maDmg);
            SetFArr(miProp, miDmg);
            SetFArr(pProp, pDmg);
        }

        private void PackStats(WeaponBaseStats s, float[] arr, int o)
        {
            arr[o] = s.Impact;
            arr[o + 1] = s.Range;
            arr[o + 2] = s.Stability;
            arr[o + 3] = s.Handling;
            arr[o + 4] = s.ReloadSpeed;
            arr[o + 5] = s.AimAssistance;
            arr[o + 6] = s.Zoom;
            arr[o + 7] = s.AirborneEffectiveness;
            arr[o + 8] = s.RecoilDirection;
            arr[o + 9] = s.RPM;
            arr[o + 10] = s.ChargeTime;
            arr[o + 11] = s.DrawTime;
            arr[o + 12] = s.Magazine;
            arr[o + 13] = s.BlastRadius;
            arr[o + 14] = s.Velocity;
            arr[o + 15] = s.Accuracy;
        }

        // ===================================================
        // SerializedProperty shortcuts
        // ===================================================

        private SerializedProperty SP(string n) => _so.FindProperty(n);
        private int IV(string n) { var p = SP(n); return p != null ? p.intValue : 0; }
        private float FV(string n) { var p = SP(n); return p != null ? p.floatValue : 0f; }
        private void SetS(string n, string v) { var p = SP(n); if (p != null) p.stringValue = v; }
        private void SetI(string n, int v) { var p = SP(n); if (p != null) p.intValue = v; }
        private void SetF(string n, float v) { var p = SP(n); if (p != null) p.floatValue = v; }

        private void SetArr(string n, string[] v)
        {
            var p = SP(n); if (p == null) return;
            p.ClearArray(); p.arraySize = v.Length;
            for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).stringValue = v[i];
        }
        private void SetFArr(string n, float[] v)
        {
            var p = SP(n); if (p == null) return;
            p.ClearArray(); p.arraySize = v.Length;
            for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).floatValue = v[i];
        }

        // ===================================================
        // PREVIEW SELECTION HELPERS
        // ===================================================

        private static readonly string[] _poolNameProps = {
            "_bakedPerk1PoolNames", "_bakedPerk2PoolNames",
            "_bakedPerk3PoolNames", "_bakedPerk4PoolNames",
            "_bakedMasterworkPoolNames", "_bakedModPoolNames"
        };

        private void RefreshDisplayNames()
        {
            if (!_needsRefreshDisplayNames && _poolDisplayNames != null) return;
            _needsRefreshDisplayNames = false;

            _poolDisplayNames = new string[6][];
            for (int p = 0; p < 6; p++)
            {
                var sp = SP(_poolNameProps[p]);
                int count = (sp != null) ? sp.arraySize : 0;
                _poolDisplayNames[p] = new string[count + 1];
                _poolDisplayNames[p][0] = "Random";
                for (int i = 0; i < count; i++)
                    _poolDisplayNames[p][i + 1] = sp.GetArrayElementAtIndex(i).stringValue;
            }
        }

        private void DrawPoolPopup(string label, string indexProp, int poolIdx)
        {
            var sp = SP(indexProp);
            if (sp == null) return;
            if (_poolDisplayNames == null || poolIdx >= _poolDisplayNames.Length) return;

            string[] names = _poolDisplayNames[poolIdx];
            if (names == null || names.Length <= 1)
            {
                EditorGUILayout.LabelField(label, "(pool vide)");
                return;
            }

            // -1 = Random, mappé à l'index 0 du tableau d'affichage
            int displayIdx = sp.intValue + 1;
            if (displayIdx < 0 || displayIdx >= names.Length) displayIdx = 0;

            EditorGUI.BeginChangeCheck();
            int newDisplayIdx = EditorGUILayout.Popup(label, displayIdx, names);
            if (EditorGUI.EndChangeCheck())
            {
                sp.intValue = newDisplayIdx - 1; // -1 pour Random, 0..N pour les perks
            }
        }

        /// <summary>
        /// Calcule la stat preview pour l'index de stat donné (0=Impact, 1=Range).
        /// frame + somme des contributions des pools aux indices choisis.
        /// </summary>
        private float ComputePreviewStat(int statIndex, int[] previewIndices)
        {
            const int S = 16;

            string[] statProps = {
                "_bakedPerk1PoolStats", "_bakedPerk2PoolStats",
                "_bakedPerk3PoolStats", "_bakedPerk4PoolStats",
                "_bakedMasterworkPoolStats", "_bakedModPoolStats"
            };

            float total = 0f;
            // Map statIndex → baked frame property name
            string[] framePropMap = {
                "_bakedFrameImpact",                // 0
                "_bakedFrameRange",                 // 1
                "_bakedFrameStability",             // 2
                "_bakedFrameHandling",              // 3
                "_bakedFrameReloadSpeed",           // 4
                "_bakedFrameAimAssistance",         // 5
                "_bakedFrameZoom",                  // 6
                "_bakedFrameAirborneEffectiveness", // 7
                "_bakedFrameRecoilDirection",       // 8
                "_bakedFrameRPM",                   // 9
                "_bakedFrameChargeTime",            // 10
                "_bakedFrameDrawTime",              // 11
                "_bakedFrameMagazine",              // 12 (int → float)
                "_bakedFrameBlastRadius",           // 13
                "_bakedFrameVelocity",              // 14
                "_bakedFrameAccuracy",              // 15
            };
            if (statIndex >= 0 && statIndex < framePropMap.Length)
            {
                if (statIndex == 12) total = IV(framePropMap[statIndex]); // Magazine is int
                else total = FV(framePropMap[statIndex]);
            }

            for (int p = 0; p < 6; p++)
            {
                int idx = previewIndices[p];
                if (idx < 0) continue; // Random = pas de contribution (on ne sait pas)

                var sp = SP(statProps[p]);
                if (sp == null) continue;
                int count = sp.arraySize / S;
                if (idx >= count) continue;

                total += sp.GetArrayElementAtIndex(idx * S + statIndex).floatValue;
            }
            return total;
        }

        /// <summary>
        /// Multiplicateur Impact → raw damage selon le type d'arme.
        /// Hitscan: ×0.5 (sauf MachineGun ×1) | Projectile/Melee: ×2 | Beam: ×10
        /// </summary>
        private static float GetRawDamageMultiplier(WeaponType wt)
        {
            switch (wt)
            {
                case WeaponType.Sword:
                case WeaponType.Glaive:
                    return 2f;
                case WeaponType.RocketLauncher:
                    return 5f;
                case WeaponType.BreechLoadedGrenadeLauncher:
                case WeaponType.RocketSidearm:
                    return 2.5f;
                case WeaponType.HeavyGrenadeLauncher:
                    return 3f;
                case WeaponType.TraceRifle:
                    return 10f;
                case WeaponType.MachineGun:
                    return 1f;
                default: // Hitscan
                    return 0.5f;
            }
        }

        private static float GetLauncherMult(WeaponType wt)
        {
            return GetRawDamageMultiplier(wt);
        }

        /// <summary>
        /// Portée effective = portée de base du type d'arme + Range×0.8 + Zoom×1.2.
        /// Reproduit M2922_WeaponFireHandler.GetEffectiveRange().
        /// </summary>
        private static float GetEffectiveRange(WeaponType wt, float rangeStat, float zoomStat)
        {
            float baseRange = FireModeMapping.GetHitscanRange(wt);
            return baseRange + (rangeStat * 0.8f) + (zoomStat * 1.2f);
        }
    }
}
