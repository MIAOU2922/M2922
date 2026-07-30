using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace M2922.Component.Weapon.Editor
{
    /// <summary>
    /// Générateur de TOUS les ScriptableObjects à partir du JSON Destiny 2.
    /// 
    /// UTILISATION :
    /// 1. Placez destiny2_weapons_database.json dans Assets/_MIAOU_/Weapons/
    /// 2. Menu : Tools → M2922 → Weapons → Generate ALL from JSON
    /// 
    /// Génère : Frames, Perks, Masterworks, Mods
    /// </summary>
    public class WeaponAssetGenerator : EditorWindow
    {
        private const string ASSETS_ROOT = "Assets/_MIAOU_/Weapons";
        private const string JSON_PATH = ASSETS_ROOT + "/destiny2_weapons_database.json";

        private static string[] ALL_WEAPON_TYPE_KEYS = new string[]
        {
            "Auto Rifle", "Pulse Rifle", "Scout Rifle", "Hand Cannon",
            "Submachine Gun", "Sidearm", "Combat Bow", "Shotgun",
            "Fusion Rifle", "Sniper Rifle", "Trace Rifle",
            "Breech-Loaded Grenade Launcher", "Heavy Grenade Launcher",
            "Rocket Launcher", "Linear Fusion Rifle", "Machine Gun",
            "Sword", "Glaive", "Rocket Sidearm"
        };

        [MenuItem("M2922/Weapons/Generate ALL from JSON", priority = 10)]
        public static void GenerateAllFromJson()
        {
            if (!File.Exists(JSON_PATH))
            {
                EditorUtility.DisplayDialog("Erreur",
                    "Fichier JSON introuvable :\n" + JSON_PATH +
                    "\n\nPlacez destiny2_weapons_database.json dans " + ASSETS_ROOT + "/",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirmation",
                "Ceci va generer TOUS les ScriptableObjects :\n\n" +
                "• Frames (~60)\n• Perks (~60)\n• Masterworks (~20)\n• Mods (~22)\n\n" +
                "Les assets existants seront ecrases. Continuer ?",
                "Oui, generer tout", "Annuler"))
                return;

            string fullJson = File.ReadAllText(JSON_PATH, Encoding.UTF8);

            int createdCount = 0;

            try
            {
                createdCount += GenerateFrames(fullJson);
                createdCount += GeneratePerks(fullJson);
                createdCount += GenerateMasterworks(fullJson);
                createdCount += GenerateMods(fullJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[WeaponGenerator] Erreur : " + ex.Message + "\n" + ex.StackTrace);
                EditorUtility.DisplayDialog("Erreur", "Echec de la generation :\n" + ex.Message, "OK");
                return;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Termine !",
                createdCount + " assets crees avec succes !\n\n" +
                ASSETS_ROOT + "/Frames/\n" +
                ASSETS_ROOT + "/Perks/\n" +
                ASSETS_ROOT + "/Masterworks/\n" +
                ASSETS_ROOT + "/Mods/",
                "OK");
        }

        [MenuItem("M2922/Weapons/Create Folder Structure", priority = 0)]
        public static void CreateFolderStructure()
        {
            EnsureDirectory(ASSETS_ROOT + "/Frames");
            EnsureDirectory(ASSETS_ROOT + "/Perks/Column1");
            EnsureDirectory(ASSETS_ROOT + "/Perks/Column2");
            EnsureDirectory(ASSETS_ROOT + "/Perks/Column3");
            EnsureDirectory(ASSETS_ROOT + "/Perks/Column4");
            EnsureDirectory(ASSETS_ROOT + "/Masterworks/Adept");
            EnsureDirectory(ASSETS_ROOT + "/Mods/Adept");
            EnsureDirectory(ASSETS_ROOT + "/Definitions");
            AssetDatabase.Refresh();
            Debug.Log("[Weapons] Structure de dossiers creee.");
        }

        [MenuItem("M2922/Weapons/Create Example Weapon", priority = 100)]
        public static void CreateExampleWeapon()
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.WeaponName = "Example Auto Rifle";
            def.WeaponType = WeaponType.AutoRifle;
            def.Description = "Arme d'exemple pour tester le systeme.";

            EnsureDirectory(ASSETS_ROOT + "/Definitions");
            string path = AssetDatabase.GenerateUniqueAssetPath(ASSETS_ROOT + "/Definitions/Weapon_Example.asset");
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = def;
            Debug.Log("[Weapons] WeaponDefinition d'exemple cree : " + path);
        }

        // ===================================================
        // GENERATION
        // ===================================================

        private static int GenerateFrames(string json)
        {
            EnsureDirectory(ASSETS_ROOT + "/Frames");
            int count = 0;

            // Trouver la section frames_by_weapon_type pour éviter les faux positifs
            int framesSectionStart = json.IndexOf("\"frames_by_weapon_type\"", System.StringComparison.Ordinal);
            if (framesSectionStart < 0)
            {
                Debug.LogWarning("[WeaponGenerator] Section frames_by_weapon_type introuvable.");
                return 0;
            }

            foreach (string weaponTypeKey in ALL_WEAPON_TYPE_KEYS)
            {
                // Chercher la clé APRÈS le début de la section frames
                string framesJson = ExtractJsonArray(json, "\"" + weaponTypeKey + "\"", framesSectionStart);
                if (string.IsNullOrEmpty(framesJson)) continue;

                WeaponType weaponType = JsonStatParser.ParseWeaponType(weaponTypeKey);

                // Parser chaque frame individuellement
                List<string> frameObjects = SplitJsonObjects(framesJson);
                foreach (string frameObjJson in frameObjects)
                {
                    try
                    {
                        var frameEntry = JsonUtility.FromJson<JsonFrameEntry>(frameObjJson);
                        if (frameEntry == null || string.IsNullOrEmpty(frameEntry.frame_name)) continue;

                        var asset = ScriptableObject.CreateInstance<WeaponFrameData>();
                        asset.FrameName = frameEntry.frame_name;
                        asset.WeaponType = weaponType;
                        asset.Description = frameEntry.description ?? "";

                        // Stats de base
                        // JsonUtility ne supporte pas les clés avec espaces
                        // ("Reload Speed", "Aim Assistance", "Recoil Direction", etc.)
                        // → on les extrait manuellement du JSON brut
                        float reloadSpeed = ExtractFloatValue(frameObjJson, "Reload Speed");
                        float aimAssist = ExtractFloatValue(frameObjJson, "Aim Assistance");
                        float recoilDir = ExtractFloatValue(frameObjJson, "Recoil Direction");
                        float airborneEff = ExtractFloatValue(frameObjJson, "Airborne Effectiveness");
                        float blastRadius = ExtractFloatValue(frameObjJson, "Blast Radius");
                        float swingSpeed = ExtractFloatValue(frameObjJson, "Swing Speed");
                        float guardResist = ExtractFloatValue(frameObjJson, "Guard Resistance");
                        float guardEff = ExtractFloatValue(frameObjJson, "Guard Efficiency");
                        float chargeRate = ExtractFloatValue(frameObjJson, "Charge Rate");
                        float ammoCap = ExtractFloatValue(frameObjJson, "Ammo Capacity");
                        float shieldDur = ExtractFloatValue(frameObjJson, "Shield Duration");
                        float drawTime = ExtractFloatValue(frameObjJson, "Draw Time");
                        float chargeTime = ExtractFloatValue(frameObjJson, "Charge Time");
                        float accuracy = ExtractFloatValue(frameObjJson, "Accuracy");

                        // Stats parsées par JsonUtility (clés sans espace)
                        float impact = 0f, range = 0f, stability = 0f, handling = 0f, zoom = 0f, velocity = 0f;
                        int magazine = 0;
                        if (frameEntry.base_stats != null)
                        {
                            var bs = frameEntry.base_stats;
                            impact = bs.Impact;
                            range = bs.Range;
                            stability = bs.Stability;
                            handling = bs.Handling;
                            zoom = bs.Zoom;
                            velocity = bs.Velocity;
                            magazine = bs.Magazine;
                        }

                        asset.BaseStats = new WeaponBaseStats
                        {
                            Impact = impact,
                            Range = range,
                            Stability = stability,
                            Handling = handling,
                            ReloadSpeed = reloadSpeed,
                            AimAssistance = aimAssist,
                            Zoom = zoom,
                            RecoilDirection = recoilDir,
                            AirborneEffectiveness = airborneEff,
                            Magazine = magazine,
                            BlastRadius = blastRadius,
                            Velocity = velocity,
                            Accuracy = accuracy,
                            SwingSpeed = swingSpeed,
                            GuardResistance = guardResist,
                            GuardEfficiency = guardEff,
                            ChargeRate = chargeRate,
                            AmmoCapacity = (int)ammoCap,
                            ShieldDuration = shieldDur,
                            RPM = frameEntry.rpm,
                            ChargeTime = frameEntry.charge_time + chargeTime,
                            DrawTime = frameEntry.draw_time + drawTime
                        };

                        string fileName = "Frame_" + JsonStatParser.SanitizeFileName(weaponTypeKey)
                            + "_" + JsonStatParser.SanitizeFileName(frameEntry.frame_name) + ".asset";
                        string path = AssetDatabase.GenerateUniqueAssetPath(ASSETS_ROOT + "/Frames/" + fileName);
                        AssetDatabase.CreateAsset(asset, path);
                        count++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[WeaponGenerator] Frame skipped : " + ex.Message);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponGenerator] " + count + " Frames generees.");
            return count;
        }

        private static int GeneratePerks(string json)
        {
            int count = 0;

            // Parser les perks avec structure
            var root = JsonUtility.FromJson<PerksRootWrapper>(json);
            if (root == null || root.perk_categories == null)
            {
                Debug.LogWarning("[WeaponGenerator] Impossible de parser les perks.");
                return 0;
            }

            var cats = root.perk_categories;

            // Column 1 : Barrels/Sights (avec stats)
            count += CreatePerksFromEntries(cats.column_1_barrels_sights,
                PerkColumn.Column1_Barrel_Sight, ASSETS_ROOT + "/Perks/Column1");

            // Column 2 : Magazines/Battery (avec stats)
            count += CreatePerksFromEntries(cats.column_2_magazines_battery,
                PerkColumn.Column2_Magazine_Battery, ASSETS_ROOT + "/Perks/Column2");

            // Column 3 : Utility Traits (strings)
            count += CreatePerksFromStrings(cats.column_3_utility_traits,
                PerkColumn.Column3_Utility, ASSETS_ROOT + "/Perks/Column3");

            // Column 4 : Damage Traits (strings)
            count += CreatePerksFromStrings(cats.column_4_damage_traits,
                PerkColumn.Column4_Damage, ASSETS_ROOT + "/Perks/Column4");

            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponGenerator] " + count + " Perks generes.");
            return count;
        }

        private static int CreatePerksFromEntries(JsonPerkEntry[] entries, PerkColumn column, string folder)
        {
            EnsureDirectory(folder);
            int count = 0;
            if (entries == null) return 0;

            foreach (var entry in entries)
            {
                var asset = ScriptableObject.CreateInstance<WeaponPerkData>();
                asset.PerkName = entry.name;
                asset.Column = column;
                asset.Description = (entry.stats != null && !string.IsNullOrEmpty(entry.stats.Description))
                    ? entry.stats.Description : "";

                if (entry.stats != null)
                {
                    asset.StatModifiers = new WeaponBaseStats
                    {
                        Range = JsonStatParser.ParseStatString(entry.stats.Range),
                        Stability = JsonStatParser.ParseStatString(entry.stats.Stability),
                        Handling = JsonStatParser.ParseStatString(entry.stats.Handling),
                        ReloadSpeed = JsonStatParser.ParseStatString(entry.stats.ReloadSpeed),
                        AimAssistance = JsonStatParser.ParseStatString(entry.stats.AimAssistance),
                        RecoilDirection = JsonStatParser.ParseStatString(entry.stats.RecoilDirection),
                        AirborneEffectiveness = JsonStatParser.ParseStatString(entry.stats.AirborneEffectiveness),
                        Magazine = (int)JsonStatParser.ParseStatString(entry.stats.Magazine),
                        BlastRadius = JsonStatParser.ParseStatString(entry.stats.BlastRadius),
                        Velocity = JsonStatParser.ParseStatString(entry.stats.Velocity)
                    };
                }

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/Perk_" + JsonStatParser.SanitizeFileName(entry.name) + ".asset");
                AssetDatabase.CreateAsset(asset, path);
                count++;
            }
            return count;
        }

        private static int CreatePerksFromStrings(string[] names, PerkColumn column, string folder)
        {
            EnsureDirectory(folder);
            int count = 0;
            if (names == null) return 0;

            foreach (string name in names)
            {
                var asset = ScriptableObject.CreateInstance<WeaponPerkData>();
                asset.PerkName = name;
                asset.Column = column;
                asset.Description = "Trait : " + name;

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/Perk_" + JsonStatParser.SanitizeFileName(name) + ".asset");
                AssetDatabase.CreateAsset(asset, path);
                count++;
            }
            return count;
        }

        private static int GenerateMasterworks(string json)
        {
            var root = JsonUtility.FromJson<MasterworksRootWrapper>(json);
            if (root == null || root.masterworks == null) return 0;

            int count = 0;
            var mw = root.masterworks;

            // Standard
            string stdFolder = ASSETS_ROOT + "/Masterworks";
            EnsureDirectory(stdFolder);
            if (mw.standard_legendary != null && mw.standard_legendary.available_stats != null)
            {
                foreach (string statName in mw.standard_legendary.available_stats)
                {
                    var asset = ScriptableObject.CreateInstance<WeaponMasterworkData>();
                    asset.MasterworkName = "Masterwork (" + statName + ")";
                    asset.PrimaryStat = JsonStatParser.ParseWeaponStat(statName);
                    asset.PrimaryBoost = mw.standard_legendary.stat_boost;
                    asset.IsAdept = false;
                    asset.Description = "Standard masterwork : +" + asset.PrimaryBoost + " " + statName;

                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        stdFolder + "/Masterwork_" + JsonStatParser.SanitizeFileName(statName) + ".asset");
                    AssetDatabase.CreateAsset(asset, path);
                    count++;
                }
            }

            // Adept
            string adeptFolder = ASSETS_ROOT + "/Masterworks/Adept";
            EnsureDirectory(adeptFolder);
            if (mw.adept_legendary != null && mw.adept_legendary.available_stats != null)
            {
                foreach (string statName in mw.adept_legendary.available_stats)
                {
                    var asset = ScriptableObject.CreateInstance<WeaponMasterworkData>();
                    asset.MasterworkName = "Adept Masterwork (" + statName + ")";
                    asset.PrimaryStat = JsonStatParser.ParseWeaponStat(statName);
                    asset.PrimaryBoost = mw.adept_legendary.primary_stat_boost;
                    asset.SecondaryBoost = mw.adept_legendary.secondary_stat_boost;
                    asset.IsAdept = true;
                    asset.Description = "Adept : +" + asset.PrimaryBoost + " " + statName
                        + ", +" + asset.SecondaryBoost + " autres stats";

                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        adeptFolder + "/Masterwork_Adept_" + JsonStatParser.SanitizeFileName(statName) + ".asset");
                    AssetDatabase.CreateAsset(asset, path);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponGenerator] " + count + " Masterworks generes.");
            return count;
        }

        private static int GenerateMods(string json)
        {
            var root = JsonUtility.FromJson<ModsRootWrapper>(json);
            if (root == null || root.mods == null) return 0;

            int count = 0;
            var mods = root.mods;

            // Standard mods
            string stdFolder = ASSETS_ROOT + "/Mods";
            EnsureDirectory(stdFolder);
            if (mods.standard_mods != null)
            {
                foreach (var mod in mods.standard_mods)
                {
                    var asset = ScriptableObject.CreateInstance<WeaponModData>();
                    asset.ModName = mod.name;
                    asset.Description = mod.effect;

                    // Parser les stats numériques depuis l'effet
                    asset.StatModifiers = JsonStatParser.ParseModEffect(mod.effect);

                    // Parser les multiplicateurs de dégâts
                    asset.BossDamageMultiplier = ExtractDamagePercent(mod.effect, "Boss");
                    asset.MajorDamageMultiplier = ExtractDamagePercent(mod.effect, "Powerful|Champion|Major");
                    asset.MinorDamageMultiplier = ExtractDamagePercent(mod.effect, "Rank-and-File|Minor");
                    asset.PlayerDamageMultiplier = ExtractDamagePercent(mod.effect, "Taken");

                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        stdFolder + "/Mod_" + JsonStatParser.SanitizeFileName(mod.name) + ".asset");
                    AssetDatabase.CreateAsset(asset, path);
                    count++;
                }
            }

            // Adept mods
            string adeptFolder = ASSETS_ROOT + "/Mods/Adept";
            EnsureDirectory(adeptFolder);
            if (mods.adept_mods != null)
            {
                foreach (var mod in mods.adept_mods)
                {
                    var asset = ScriptableObject.CreateInstance<WeaponModData>();
                    asset.ModName = mod.name;
                    asset.Description = mod.effect;
                    asset.StatModifiers = JsonStatParser.ParseModEffect(mod.effect);
                    asset.BossDamageMultiplier = ExtractDamagePercent(mod.effect, "Boss");
                    asset.MajorDamageMultiplier = ExtractDamagePercent(mod.effect, "Powerful|Champion|Major");
                    asset.MinorDamageMultiplier = ExtractDamagePercent(mod.effect, "Rank-and-File|Minor");
                    asset.PlayerDamageMultiplier = ExtractDamagePercent(mod.effect, "Taken");

                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        adeptFolder + "/Mod_" + JsonStatParser.SanitizeFileName(mod.name) + ".asset");
                    AssetDatabase.CreateAsset(asset, path);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WeaponGenerator] " + count + " Mods generes.");
            return count;
        }

        // ===================================================
        // JSON PARSING HELPERS
        // ===================================================

        /// <summary>
        /// Extrait un tableau JSON après une clé donnée, en cherchant à partir de startIdx.
        /// </summary>
        private static string ExtractJsonArray(string json, string key, int startIdx = 0)
        {
            int keyIdx = json.IndexOf(key, startIdx, System.StringComparison.Ordinal);
            if (keyIdx < 0) return null;

            // Trouver le ':' après la clé
            int colonIdx = json.IndexOf(':', keyIdx + key.Length);
            if (colonIdx < 0) return null;

            // Trouver le '['
            int openBracket = json.IndexOf('[', colonIdx);
            if (openBracket < 0) return null;

            // Trouver le ']' correspondant
            int depth = 1;
            int i = openBracket + 1;
            bool inString = false;
            while (i < json.Length && depth > 0)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\')) inString = !inString;
                if (!inString)
                {
                    if (c == '[') depth++;
                    else if (c == ']') depth--;
                }
                i++;
            }

            return json.Substring(openBracket + 1, i - openBracket - 2).Trim();
        }

        /// <summary>
        /// Sépare un tableau JSON d'objets en objets individuels.
        /// </summary>
        private static List<string> SplitJsonObjects(string arrayContent)
        {
            var objects = new List<string>();
            if (string.IsNullOrEmpty(arrayContent)) return objects;

            int depth = 0;
            int start = -1;
            bool inString = false;

            for (int i = 0; i < arrayContent.Length; i++)
            {
                char c = arrayContent[i];
                if (c == '"' && (i == 0 || arrayContent[i - 1] != '\\')) inString = !inString;
                if (inString) continue;

                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        objects.Add(arrayContent.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }
            return objects;
        }

        /// <summary>
        /// Extrait un pourcentage de dégâts depuis un texte comme "+7.7% damage against Bosses"
        /// Retourne le multiplicateur (ex: 1.077)
        /// </summary>
        private static float ExtractDamagePercent(string effect, string targetPattern)
        {
            if (string.IsNullOrEmpty(effect)) return 1f;

            // Vérifie si le pattern cible est mentionné
            bool targetsMatch = false;
            foreach (string pattern in targetPattern.Split('|'))
            {
                if (effect.Contains(pattern))
                {
                    targetsMatch = true;
                    break;
                }
            }
            if (!targetsMatch) return 1f;

            // Extrait le pourcentage
            int pctIdx = effect.IndexOf('%');
            if (pctIdx < 0) return 1f;

            // Cherche le nombre avant le %
            string before = effect.Substring(0, pctIdx);
            int lastSpace = before.LastIndexOf(' ');
            int lastPlus = before.LastIndexOf('+');
            int startIdx = System.Math.Max(lastSpace, lastPlus);
            string numStr = before.Substring(startIdx + 1).Trim();

            if (float.TryParse(numStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float percent))
            {
                return 1f + (percent / 100f);
            }

            return 1f;
        }

        /// <summary>
        /// Extrait une valeur float d'un objet JSON par clé.
        /// Gère les clés avec espaces que JsonUtility ne supporte pas.
        /// Ex: Extraire "Reload Speed": 50 d'un objet JSON.
        /// </summary>
        private static float ExtractFloatValue(string jsonObject, string key)
        {
            // Chercher "key":
            string searchKey = "\"" + key + "\"";
            int keyIdx = jsonObject.IndexOf(searchKey, System.StringComparison.Ordinal);
            if (keyIdx < 0) return 0f;

            // Chercher ':' après la clé
            int colonIdx = jsonObject.IndexOf(':', keyIdx + searchKey.Length);
            if (colonIdx < 0) return 0f;

            // Extraire la valeur après ':'
            string afterColon = jsonObject.Substring(colonIdx + 1).Trim();
            // Prendre jusqu'à ',' ou '}' ou '\n'
            int endIdx = afterColon.IndexOfAny(new char[] { ',', '}', '\n', '\r' });
            if (endIdx < 0) endIdx = afterColon.Length;
            string valStr = afterColon.Substring(0, endIdx).Trim();

            if (float.TryParse(valStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;

            return 0f;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // ===================================================
        // WRAPPERS POUR JsonUtility (extraction partielle)
        // ===================================================

        [System.Serializable]
        private class PerksRootWrapper
        {
            public JsonPerkCategories perk_categories;
        }

        [System.Serializable]
        private class MasterworksRootWrapper
        {
            public MasterworksInner masterworks;
        }

        [System.Serializable]
        private class MasterworksInner
        {
            public MasterworkDetailInner standard_legendary;
            public MasterworkDetailInner adept_legendary;
        }

        [System.Serializable]
        private class MasterworkDetailInner
        {
            public int stat_boost;
            public int primary_stat_boost;
            public int secondary_stat_boost;
            public string[] available_stats;
        }

        [System.Serializable]
        private class ModsRootWrapper
        {
            public ModsInner mods;
        }

        [System.Serializable]
        private class ModsInner
        {
            public JsonModEntry[] standard_mods;
            public JsonModEntry[] adept_mods;
        }
    }
}
