using System;
using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Types d'armes supportés (aligné avec Destiny 2).
    /// </summary>
    public enum WeaponType
    {
        AutoRifle,
        PulseRifle,
        ScoutRifle,
        HandCannon,
        SubmachineGun,
        Sidearm,
        CombatBow,
        Shotgun,
        FusionRifle,
        SniperRifle,
        TraceRifle,
        BreechLoadedGrenadeLauncher,
        HeavyGrenadeLauncher,
        RocketLauncher,
        LinearFusionRifle,
        MachineGun,
        Sword,
        Glaive,
        RocketSidearm
    }

    /// <summary>
    /// Colonne de perk : détermine où le perk peut être équipé.
    /// </summary>
    public enum PerkColumn
    {
        Column1_Barrel_Sight,   // Barrel / Sight / Launch
        Column2_Magazine_Battery, // Magazine / Battery / Grip
        Column3_Utility,        // Trait colonne 3
        Column4_Damage          // Trait colonne 4
    }

    /// <summary>
    /// Type de dégât élémentaire.
    /// </summary>
    public enum DamageType
    {
        Kinetic = 0,    // Cinétique
        Solar = 1,      // Solaire (Feu)
        Arc = 2,        // Cryo-électrique (Foudre)
        Void = 3,       // Abyssal
        Stasis = 4,     // Stase (Glace)
        Strand = 5      // Filobscur
    }

    /// <summary>
    /// Emplacement d'arme (détermine les munitions et types compatibles).
    /// </summary>
    public enum WeaponSlot
    {
        Kinetic = 1,    // Emplacement Cinétique (primary/special)
        Energy = 2,     // Emplacement Énergétique (primary/special, éléments Light)
        Power = 3       // Emplacement Puissant (heavy, tous éléments)
    }

    /// <summary>
    /// Rareté de l'arme.
    /// </summary>
    public enum WeaponRarity
    {
        Common = 1,     // Commune (blanc)
        Uncommon = 2,   // Peu commune (vert)
        Rare = 3,       // Rare (bleu)
        Legendary = 4,  // Légendaire (violet)
        Exotic = 5      // Exotique (jaune/or) — max 1 équipée
    }

    /// <summary>
    /// Mode de tir déterminé par le type d'arme.
    /// </summary>
    public enum FireMode
    {
        FullAuto,       // Tir continu tant que la gâchette est pressée
        SemiAuto,       // Un tir par pression
        Burst,          // Rafale de N projectiles (Pulse Rifle = 3, Aggressive Burst = 4, etc.)
        Charge,         // Charge puis tir (Fusion Rifle, Linear Fusion)
        ChargeBow,      // Charge avec Draw Time (Combat Bow)
        SingleShot,     // Un coup puis rechargement obligatoire (Rocket Launcher, Breech GL)
        Beam,           // Faisceau continu (Trace Rifle)
        Melee,          // Cac (Sword)
        Hybrid          // Mêlée + projectile (Glaive)
    }

    /// <summary>
    /// Déclencheur d'explosion pour les projectiles (roquettes, grenades).
    /// </summary>
    public enum ExplosionTrigger
    {
        /// <summary>Déterminé automatiquement par le type d'arme.</summary>
        Default = -1,
        /// <summary>Explose à l'impact uniquement, PAS à la mort (rocket par défaut).</summary>
        OnImpact = 0,
        /// <summary>Explose à l'impact ET à la mort (grenade par défaut).</summary>
        OnImpactAndDeath = 1,
        /// <summary>Explose uniquement quand la durée de vie expire.</summary>
        OnDeath = 2,
        /// <summary>Explose à l'impact ET après un délai (grenade à retardement).</summary>
        OnImpactAndAfterDelay = 3,
        /// <summary>Explose à l'impact ET après un délai ET à la mort (grenade à retardement + timer).</summary>
        OnImpactAndAfterDelayAndOnDeath = 4
    }

    /// <summary>
    /// Mapping statique WeaponType → FireMode + burst count.
    /// </summary>
    public static class FireModeMapping
    {
        public static FireMode GetFireMode(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.AutoRifle: return FireMode.FullAuto;
                case WeaponType.SubmachineGun: return FireMode.FullAuto;
                case WeaponType.MachineGun: return FireMode.FullAuto;

                case WeaponType.ScoutRifle: return FireMode.SemiAuto;
                case WeaponType.HandCannon: return FireMode.SemiAuto;
                case WeaponType.Sidearm: return FireMode.SemiAuto;
                case WeaponType.SniperRifle: return FireMode.SemiAuto;

                case WeaponType.PulseRifle: return FireMode.Burst;
                // RocketSidearm est en FullAuto (comme Destiny 2)

                case WeaponType.FusionRifle: return FireMode.Charge;
                case WeaponType.LinearFusionRifle: return FireMode.Charge;

                case WeaponType.CombatBow: return FireMode.ChargeBow;

                case WeaponType.RocketLauncher: return FireMode.SingleShot;
                case WeaponType.BreechLoadedGrenadeLauncher: return FireMode.SingleShot;
                case WeaponType.HeavyGrenadeLauncher: return FireMode.SemiAuto;

                case WeaponType.TraceRifle: return FireMode.Beam;

                case WeaponType.Sword: return FireMode.Melee;
                case WeaponType.Glaive: return FireMode.Hybrid;

                case WeaponType.Shotgun: return FireMode.SemiAuto;
                case WeaponType.RocketSidearm: return FireMode.FullAuto;

                default: return FireMode.SemiAuto;
            }
        }

        /// <summary>
        /// Nombre de projectiles par burst (pour Pulse Rifle etc.).
        /// </summary>
        public static int GetBurstCount(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.PulseRifle: return 3;
                case WeaponType.Sidearm: return 3; // certains sidearms
                default: return 1;
            }
        }

        /// <summary>
        /// Nombre de raycasts par tir (Shotgun = 12 pellets, Fusion = 7 bolts).
        /// </summary>
        public static int GetPelletCount(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Shotgun: return 12;
                case WeaponType.FusionRifle: return 7;
                default: return 1;
            }
        }

        /// <summary>
        /// True si l'arme utilise un raycast hitscan (pas de projectile réseau).
        /// </summary>
        public static bool UseHitscan(WeaponType type)
        {
            switch (type)
            {
                // Hitscan : armes à balle
                case WeaponType.AutoRifle:
                case WeaponType.PulseRifle:
                case WeaponType.ScoutRifle:
                case WeaponType.HandCannon:
                case WeaponType.SubmachineGun:
                case WeaponType.Sidearm:
                case WeaponType.SniperRifle:
                case WeaponType.MachineGun:
                case WeaponType.Shotgun:
                case WeaponType.CombatBow:
                case WeaponType.FusionRifle:
                case WeaponType.LinearFusionRifle:
                    return true;

                default: return false;
            }
        }

        /// <summary>
        /// True si l'arme tire un projectile physique (roquette, grenade, etc.).
        /// </summary>
        public static bool UseProjectile(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.RocketLauncher:
                case WeaponType.BreechLoadedGrenadeLauncher:
                case WeaponType.HeavyGrenadeLauncher:
                case WeaponType.RocketSidearm:
                    return true;

                default: return false;
            }
        }

        /// <summary>
        /// Portée effective du hitscan basée sur le type d'arme.
        /// </summary>
        public static float GetHitscanRange(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Shotgun: return 25f;
                case WeaponType.Sidearm: return 45f;
                case WeaponType.SubmachineGun: return 65f;
                case WeaponType.FusionRifle: return 75f;
                case WeaponType.HandCannon: return 90f;
                case WeaponType.AutoRifle: return 120f;
                case WeaponType.PulseRifle: return 150f;
                case WeaponType.MachineGun: return 160f;
                case WeaponType.CombatBow: return 180f;
                case WeaponType.ScoutRifle: return 220f;
                case WeaponType.SniperRifle: return 400f;
                case WeaponType.LinearFusionRifle: return 250f;
                default: return 100f;
            }
        }

        /// <summary>
        /// Temps de rechargement de base (secondes) selon le type d'arme.
        /// </summary>
        public static float GetBaseReloadTime(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Sidearm:             return 1.6f;
                case WeaponType.SubmachineGun:       return 2.0f;
                case WeaponType.AutoRifle:           return 2.5f;
                case WeaponType.PulseRifle:          return 2.4f;
                case WeaponType.HandCannon:          return 2.6f;
                case WeaponType.ScoutRifle:          return 2.7f;
                case WeaponType.Shotgun:             return 3.0f;
                case WeaponType.SniperRifle:         return 3.2f;
                case WeaponType.LinearFusionRifle:   return 3.3f;
                case WeaponType.MachineGun:          return 5.0f;
                case WeaponType.FusionRifle:         return 2.8f;
                case WeaponType.CombatBow:           return 2.3f;
                case WeaponType.TraceRifle:          return 3.0f;
                case WeaponType.RocketLauncher:
                case WeaponType.BreechLoadedGrenadeLauncher:
                case WeaponType.HeavyGrenadeLauncher: return 3.5f;
                case WeaponType.RocketSidearm:       return 3.5f;
                case WeaponType.Sword:               return 2.0f;
                case WeaponType.Glaive:              return 2.0f;
                default: return 2.5f;
            }
        }

        /// <summary>
        /// True si l'arme utilise un rechargement balle par balle (Shotgun, etc.).
        /// </summary>
        public static bool IsSequentialReload(WeaponType type)
        {
            // Types qui sont généralement balle par balle
            switch (type)
            {
                case WeaponType.Shotgun:
                case WeaponType.BreechLoadedGrenadeLauncher:
                    return true;
                // HandCannon, ScoutRifle : peuvent être les deux selon l'arme
                // → le ReloadStyle dans le FireHandler permet l'override
                default: return false;
            }
        }

        /// <summary>
        /// Temps de base (secondes) pour chaque phase du rechargement séquentiel,
        /// à stat ReloadSpeed = 0.
        /// </summary>
        public static void GetSequentialReloadTimes(WeaponType type, out float startTime, out float perShellTime, out float endTime)
        {
            switch (type)
            {
                case WeaponType.Shotgun:
                    startTime = 0.5f;
                    perShellTime = 0.55f;
                    endTime = 0.4f;
                    break;
                case WeaponType.BreechLoadedGrenadeLauncher:
                    startTime = 0.6f;
                    perShellTime = 0.7f;
                    endTime = 0.5f;
                    break;
                case WeaponType.HandCannon:   // Revolver
                    startTime = 0.5f;
                    perShellTime = 0.45f;
                    endTime = 0.5f;
                    break;
                case WeaponType.ScoutRifle:
                    startTime = 0.4f;
                    perShellTime = 0.5f;
                    endTime = 0.4f;
                    break;
                default:
                    startTime = 0.4f;
                    perShellTime = 0.5f;
                    endTime = 0.4f;
                    break;
            }
        }
    }

    /// <summary>
    /// Structure de stats de base pour une arme.
    /// Utilisée pour les frames ET les modificateurs (perks, mods, masterwork).
    /// </summary>
    [Serializable]
    public struct WeaponBaseStats
    {
        [Header("Core Stats")]
        public float Impact;
        public float Range;
        public float Stability;
        public float Handling;
        public float ReloadSpeed;
        public float AimAssistance;
        public float Zoom;
        public float AirborneEffectiveness;
        public float RecoilDirection;

        [Header("Fire Rate & Timing")]
        public float RPM;
        public float ChargeTime;
        public float DrawTime;

        [Header("Ammo")]
        public int Magazine;

        [Header("Special (Grenade/Rocket)")]
        public float BlastRadius;
        public float Velocity;

        [Header("Special (Sword)")]
        public float SwingSpeed;
        public float GuardResistance;
        public float GuardEfficiency;
        public float ChargeRate;
        public int AmmoCapacity;

        [Header("Special (Glaive)")]
        public float ShieldDuration;

        [Header("Special (Combat Bow)")]
        public float Accuracy;

        /// <summary>
        /// Additionne deux WeaponBaseStats.
        /// </summary>
        public static WeaponBaseStats operator +(WeaponBaseStats a, WeaponBaseStats b)
        {
            return new WeaponBaseStats
            {
                Impact = a.Impact + b.Impact,
                Range = a.Range + b.Range,
                Stability = a.Stability + b.Stability,
                Handling = a.Handling + b.Handling,
                ReloadSpeed = a.ReloadSpeed + b.ReloadSpeed,
                AimAssistance = a.AimAssistance + b.AimAssistance,
                Zoom = a.Zoom + b.Zoom,
                AirborneEffectiveness = a.AirborneEffectiveness + b.AirborneEffectiveness,
                RecoilDirection = a.RecoilDirection + b.RecoilDirection,
                Accuracy = a.Accuracy + b.Accuracy,
                RPM = a.RPM + b.RPM,
                ChargeTime = a.ChargeTime + b.ChargeTime,
                DrawTime = a.DrawTime + b.DrawTime,
                Magazine = a.Magazine + b.Magazine,
                BlastRadius = a.BlastRadius + b.BlastRadius,
                Velocity = a.Velocity + b.Velocity,
                SwingSpeed = a.SwingSpeed + b.SwingSpeed,
                GuardResistance = a.GuardResistance + b.GuardResistance,
                GuardEfficiency = a.GuardEfficiency + b.GuardEfficiency,
                ChargeRate = a.ChargeRate + b.ChargeRate,
                AmmoCapacity = a.AmmoCapacity + b.AmmoCapacity,
                ShieldDuration = a.ShieldDuration + b.ShieldDuration
            };
        }

        /// <summary>
        /// Clone les valeurs.
        /// </summary>
        public WeaponBaseStats Clone()
        {
            return (WeaponBaseStats)this.MemberwiseClone();
        }
    }
}
