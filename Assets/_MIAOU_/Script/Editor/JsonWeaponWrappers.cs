using System;
using System.Collections.Generic;
using UnityEngine;

namespace M2922.Component.Weapon.Editor
{
    // ===================================================
    // WRAPPERS pour JsonUtility (ne supporte pas Dictionary)
    // ===================================================

    [Serializable]
    public class JsonWeaponDatabase
    {
        public JsonMetadata metadata;
        public string[] weapon_types;
        public JsonMasterworksWrapper masterworks;
        public JsonModsWrapper mods;
        public JsonPerkCategories perk_categories;
        // frames_by_weapon_type est parsé manuellement (dictionary)
    }

    [Serializable]
    public class JsonMetadata
    {
        public string title;
        public string game;
        public string description;
        public string version;
    }

    // ===== MASTERWORKS =====
    [Serializable]
    public class JsonMasterworksWrapper
    {
        public JsonMasterworkDetails standard_legendary;
        public JsonMasterworkDetails adept_legendary;
    }

    [Serializable]
    public class JsonMasterworkDetails
    {
        public int stat_boost;
        public int primary_stat_boost;
        public int secondary_stat_boost;
        public string note;
        public string[] available_stats;
    }

    // ===== MODS =====
    [Serializable]
    public class JsonModsWrapper
    {
        public JsonModEntry[] standard_mods;
        public JsonModEntry[] adept_mods;
    }

    [Serializable]
    public class JsonModEntry
    {
        public string name;
        public string effect;
    }

    // ===== PERKS =====
    [Serializable]
    public class JsonPerkCategories
    {
        public JsonPerkEntry[] column_1_barrels_sights;
        public JsonPerkEntry[] column_2_magazines_battery;
        public string[] column_3_utility_traits;
        public string[] column_4_damage_traits;
    }

    [Serializable]
    public class JsonPerkEntry
    {
        public string name;
        public JsonPerkStats stats;
    }

    [Serializable]
    public class JsonPerkStats
    {
        public string Range;
        public string Stability;
        public string Handling;
        public string ReloadSpeed;
        public string RecoilDirection;
        public string AimAssistance;
        public string AirborneEffectiveness;
        public string Magazine;
        public string BlastRadius;
        public string Velocity;
        public string Description;
    }

    // ===== FRAMES =====
    // Pour contourner la limite de JsonUtility avec les dictionnaires,
    // on parse manuellement chaque type d'arme

    [Serializable]
    public class JsonFrameEntry
    {
        public string frame_name;
        public float rpm;
        public string rpm_string; // pour les valeurs comme "2-burst"
        public float charge_time;
        public float draw_time;
        public float swing_speed;
        public string description;
        public JsonFrameBaseStats base_stats;
    }

    [Serializable]
    public class JsonFrameBaseStats
    {
        public float Impact;
        public float Range;
        public float Stability;
        public float Handling;
        public float ReloadSpeed;
        public float AimAssistance;
        public float Zoom;
        public float RecoilDirection;
        public float AirborneEffectiveness;
        public int Magazine;
        public float BlastRadius;
        public float Velocity;
        public float Accuracy;
        public float SwingSpeed;
        public float GuardResistance;
        public float GuardEfficiency;
        public float ChargeRate;
        public int AmmoCapacity;
        public float ShieldDuration;
        public float DrawTime;
        public float ChargeTime;
    }

    /// <summary>
    /// Conteneur pour un type d'arme + ses frames.
    /// </summary>
    [Serializable]
    public class JsonWeaponTypeFrames
    {
        public string weapon_type;
        public JsonFrameEntry[] frames;
    }

    /// <summary>
    /// Wrapper racine pour les frames parsées manuellement.
    /// </summary>
    [Serializable]
    public class JsonFramesRoot
    {
        public JsonWeaponTypeFrames[] frames_by_weapon_type;
    }

    // ===== HELPERS =====

    /// <summary>
    /// Helper pour parser les valeurs de stats depuis le JSON.
    /// </summary>
    public static class JsonStatParser
    {
        /// <summary>
        /// Parse une valeur de stat string comme "+10", "-5", etc. vers float.
        /// </summary>
        public static float ParseStatString(string val)
        {
            if (string.IsNullOrEmpty(val)) return 0f;
            string cleaned = val.Replace("+", "").Replace("%", "").Trim();
            if (float.TryParse(cleaned, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            return 0f;
        }

        /// <summary>
        /// Parse un effet de mod style "+15 Recoil Direction" ou "+10 Range, -5 Stability"
        /// et retourne les modificateurs WeaponBaseStats.
        /// </summary>
        public static WeaponBaseStats ParseModEffect(string effect)
        {
            var s = new WeaponBaseStats();
            if (string.IsNullOrEmpty(effect)) return s;

            // Patterns: "+10 Range", "-5 Stability", "+15 Airborne Effectiveness", etc.
            s.Range += ExtractModifier(effect, "Range");
            s.Stability += ExtractModifier(effect, "Stability");
            s.Handling += ExtractModifier(effect, "Handling");
            s.ReloadSpeed += ExtractModifier(effect, "Reload Speed");
            s.AimAssistance += ExtractModifier(effect, "Aim Assistance");
            s.RecoilDirection += ExtractModifier(effect, "Recoil Direction");
            s.AirborneEffectiveness += ExtractModifier(effect, "Airborne Effectiveness");
            s.BlastRadius += ExtractModifier(effect, "Blast Radius");
            s.Velocity += ExtractModifier(effect, "Velocity");

            return s;
        }

        private static float ExtractModifier(string text, string statName)
        {
            int idx = text.IndexOf(statName, StringComparison.OrdinalIgnoreCase);
            if (idx <= 0) return 0f;

            // Cherche le nombre précédent
            string before = text.Substring(0, idx).TrimEnd();
            int lastSep = before.LastIndexOfAny(new char[] { ' ', ',' });
            string numStr = before.Substring(lastSep + 1).Trim();

            return ParseStatString(numStr);
        }

        public static WeaponType ParseWeaponType(string name)
        {
            switch (name)
            {
                case "Auto Rifle": return WeaponType.AutoRifle;
                case "Pulse Rifle": return WeaponType.PulseRifle;
                case "Scout Rifle": return WeaponType.ScoutRifle;
                case "Hand Cannon": return WeaponType.HandCannon;
                case "Submachine Gun": return WeaponType.SubmachineGun;
                case "Sidearm": return WeaponType.Sidearm;
                case "Combat Bow": return WeaponType.CombatBow;
                case "Shotgun": return WeaponType.Shotgun;
                case "Fusion Rifle": return WeaponType.FusionRifle;
                case "Sniper Rifle": return WeaponType.SniperRifle;
                case "Trace Rifle": return WeaponType.TraceRifle;
                case "Breech-Loaded Grenade Launcher": return WeaponType.BreechLoadedGrenadeLauncher;
                case "Heavy Grenade Launcher": return WeaponType.HeavyGrenadeLauncher;
                case "Rocket Launcher": return WeaponType.RocketLauncher;
                case "Linear Fusion Rifle": return WeaponType.LinearFusionRifle;
                case "Machine Gun": return WeaponType.MachineGun;
                case "Sword": return WeaponType.Sword;
                case "Glaive": return WeaponType.Glaive;
                case "Rocket Sidearm": return WeaponType.RocketSidearm;
                default: return WeaponType.AutoRifle;
            }
        }

        public static WeaponStat ParseWeaponStat(string stat)
        {
            switch (stat)
            {
                case "Range": return WeaponStat.Range;
                case "Stability": return WeaponStat.Stability;
                case "Handling": return WeaponStat.Handling;
                case "Reload Speed": return WeaponStat.ReloadSpeed;
                case "Aim Assistance": return WeaponStat.AimAssistance;
                case "Blast Radius": return WeaponStat.BlastRadius;
                case "Velocity": return WeaponStat.Velocity;
                case "Draw Time": return WeaponStat.DrawTime;
                case "Charge Time": return WeaponStat.ChargeTime;
                case "Impact (Swords)": return WeaponStat.Impact_Sword;
                default: return WeaponStat.Range;
            }
        }

        public static string SanitizeFileName(string name)
        {
            return name
                .Replace(" ", "_")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("%", "pct");
        }
    }
}
