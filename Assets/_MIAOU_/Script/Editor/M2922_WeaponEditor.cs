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
    public class M2922_WeaponEditor : UnityEditor.Editor
    {
        private M2922_Weapon _target;
        private SerializedObject _so;

        private void OnEnable()
        {
            _target = (M2922_Weapon)target;
            _so = new SerializedObject(_target);
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
                BakeFromDefinition(_target._weaponDefinition);

            // Rafraîchir après bake
            _so.Update();

            string bakedName = SP("_bakedWeaponName")?.stringValue;
            if (!string.IsNullOrEmpty(bakedName))
            {
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
                EditorGUILayout.LabelField("Impact", FV("_bakedFrameImpact").ToString("F1"));
                EditorGUILayout.LabelField("Range", FV("_bakedFrameRange").ToString("F1"));
                EditorGUILayout.LabelField("Stability", FV("_bakedFrameStability").ToString("F1"));
                EditorGUILayout.LabelField("Handling", FV("_bakedFrameHandling").ToString("F1"));
                EditorGUILayout.LabelField("Reload", FV("_bakedFrameReloadSpeed").ToString("F1"));
                EditorGUILayout.LabelField("AA", FV("_bakedFrameAimAssistance").ToString("F1"));
                EditorGUILayout.LabelField("RecoilDir", FV("_bakedFrameRecoilDirection").ToString("F1"));
                EditorGUILayout.LabelField("Airborne", FV("_bakedFrameAirborneEffectiveness").ToString("F1"));
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
            Debug.Log("[WeaponEditor] Bake OK: " + def.WeaponName
                + " | Imp=" + def.Frame.BaseStats.Impact
                + " Rng=" + def.Frame.BaseStats.Range
                + " Stab=" + def.Frame.BaseStats.Stability);
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
    }
}
