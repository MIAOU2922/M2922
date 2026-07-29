using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Définit un masterwork (amélioration) avec bonus de stats.
    /// Clic-droit → Create → M2922 → Weapons → Masterwork
    /// </summary>
    [CreateAssetMenu(fileName = "Masterwork_", menuName = "M2922/Weapons/Masterwork", order = 30)]
    public class WeaponMasterworkData : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [Tooltip("Nom du masterwork")]
        public string MasterworkName;

        [Tooltip("Stat principale boostée (+10 pour standard, +10/+3 pour Adept)")]
        public WeaponStat PrimaryStat;

        [Tooltip("Valeur du boost principal")]
        [Range(1, 15)]
        public int PrimaryBoost = 10;

        [Tooltip("Boost secondaire (Adept uniquement, +3 sur toutes les autres stats)")]
        [Range(0, 5)]
        public int SecondaryBoost = 0;

        [TextArea(2, 4)]
        [Tooltip("Description du masterwork")]
        public string Description;

        [Header("=== TYPE ===")]
        [Tooltip("Cochez si c'est un masterwork Adept (boost secondaire)")]
        public bool IsAdept = false;

        // ===== HELPERS =====
        public string FullName => IsAdept ? $"{MasterworkName} (Adept)" : MasterworkName;

        /// <summary>
        /// Retourne les stats modifiées par ce masterwork.
        /// </summary>
        public WeaponBaseStats GetStatModifiers()
        {
            var mods = new WeaponBaseStats();

            // Appliquer le boost principal
            ApplyStatBoost(ref mods, PrimaryStat, PrimaryBoost);

            // Appliquer le boost secondaire (Adept) sur toutes les stats SAUF la principale
            if (IsAdept && SecondaryBoost > 0)
            {
                ApplySecondaryBoost(ref mods);
            }

            return mods;
        }

        private void ApplyStatBoost(ref WeaponBaseStats stats, WeaponStat stat, int value)
        {
            switch (stat)
            {
                case WeaponStat.Range: stats.Range += value; break;
                case WeaponStat.Stability: stats.Stability += value; break;
                case WeaponStat.Handling: stats.Handling += value; break;
                case WeaponStat.ReloadSpeed: stats.ReloadSpeed += value; break;
                case WeaponStat.AimAssistance: stats.AimAssistance += value; break;
                case WeaponStat.BlastRadius: stats.BlastRadius += value; break;
                case WeaponStat.Velocity: stats.Velocity += value; break;
                case WeaponStat.DrawTime: stats.DrawTime -= value; break;
                case WeaponStat.ChargeTime: stats.ChargeTime -= value; break;
                case WeaponStat.Impact_Sword: stats.Impact += value; break;
            }
        }

        private void ApplySecondaryBoost(ref WeaponBaseStats stats)
        {
            // Toutes les stats sauf la principale reçoivent +SecondaryBoost
            if (PrimaryStat != WeaponStat.Range) stats.Range += SecondaryBoost;
            if (PrimaryStat != WeaponStat.Stability) stats.Stability += SecondaryBoost;
            if (PrimaryStat != WeaponStat.Handling) stats.Handling += SecondaryBoost;
            if (PrimaryStat != WeaponStat.ReloadSpeed) stats.ReloadSpeed += SecondaryBoost;
            if (PrimaryStat != WeaponStat.AimAssistance) stats.AimAssistance += SecondaryBoost;
            if (PrimaryStat != WeaponStat.BlastRadius) stats.BlastRadius += SecondaryBoost;
            if (PrimaryStat != WeaponStat.Velocity) stats.Velocity += SecondaryBoost;
            if (PrimaryStat != WeaponStat.DrawTime) stats.DrawTime -= SecondaryBoost;
            if (PrimaryStat != WeaponStat.ChargeTime) stats.ChargeTime -= SecondaryBoost;
        }

        public override string ToString()
        {
            return $"Masterwork: {MasterworkName} | Stat: {PrimaryStat} | +{PrimaryBoost}";
        }
    }

    /// <summary>
    /// Statistiques éligibles pour un masterwork.
    /// </summary>
    public enum WeaponStat
    {
        Range,
        Stability,
        Handling,
        ReloadSpeed,
        AimAssistance,
        BlastRadius,
        Velocity,
        DrawTime,
        ChargeTime,
        Impact_Sword
    }
}
