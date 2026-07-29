using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Définition complète d'une arme : type + frame + pools de perks/masterworks/mods.
    /// Chaque pool contient les choix possibles ; au runtime, un élément est tiré aléatoirement.
    /// 
    /// POUR AJOUTER UNE ARME SUR LA MAP :
    /// 1. Créez cet asset (clic-droit → M2922 → Weapons → Weapon Definition)
    /// 2. Remplissez les champs (type, frame, pools de perks/masterworks/mods)
    /// 3. Ajoutez un GameObject avec M2922_Weapon dans la scène
    /// 4. Glissez cet asset dans le champ WeaponDefinition du M2922_Weapon
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "M2922/Weapons/Weapon Definition", order = 0)]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [Tooltip("Nom unique de l'arme")]
        public string WeaponName;

        [Tooltip("Type d'arme")]
        public WeaponType WeaponType;

        [Tooltip("Type de dégât élémentaire")]
        public DamageType DamageType = DamageType.Kinetic;

        [Tooltip("Emplacement d'arme (Kinetic/Energy/Power)")]
        public WeaponSlot Slot = WeaponSlot.Kinetic;

        [Tooltip("Rareté de l'arme")]
        public WeaponRarity Rarity = WeaponRarity.Legendary;

        [TextArea(2, 4)]
        [Tooltip("Description / Lore de l'arme")]
        public string Description;

        [Header("=== FRAME (Archétype) ===")]
        [Tooltip("Frame/archétype de l'arme (définit les stats de base)")]
        public WeaponFrameData Frame;

        [Header("=== PERK POOLS (un par colonne) ===")]
        [Tooltip("Pool de perks colonne 1 : Barrel / Sight / Launch. Un sera tiré au hasard au runtime.")]
        public WeaponPerkData[] PerkColumn1Pool;

        [Tooltip("Pool de perks colonne 2 : Magazine / Battery / Grip. Un sera tiré au hasard au runtime.")]
        public WeaponPerkData[] PerkColumn2Pool;

        [Tooltip("Pool de perks colonne 3 : Trait utilitaire. Un sera tiré au hasard au runtime.")]
        public WeaponPerkData[] PerkColumn3Pool;

        [Tooltip("Pool de perks colonne 4 : Trait de dégâts. Un sera tiré au hasard au runtime.")]
        public WeaponPerkData[] PerkColumn4Pool;

        [Header("=== MASTERWORK POOL ===")]
        [Tooltip("Pool de masterworks possibles. Un sera tiré au hasard au runtime.")]
        public WeaponMasterworkData[] MasterworkPool;

        [Header("=== MOD POOL ===")]
        [Tooltip("Pool de mods possibles. Un sera tiré au hasard au runtime.")]
        public WeaponModData[] ModPool;

        [Header("=== VISUAL ===")]
        [Tooltip("Icône de l'arme pour l'UI")]
        public Sprite Icon;

        [Tooltip("Prefab / Modèle 3D de l'arme")]
        public GameObject WeaponPrefab;

        // ===== COMPUTED STATS =====

        /// <summary>
        /// Calcule les stats FINALES pour une combinaison spécifique.
        /// Utilisé par le baking éditeur pour pré-calculer chaque élément de pool.
        /// </summary>
        public WeaponBaseStats ComputeStatsFor(WeaponPerkData p1, WeaponPerkData p2,
            WeaponPerkData p3, WeaponPerkData p4, WeaponMasterworkData mw, WeaponModData mod)
        {
            WeaponBaseStats final = new WeaponBaseStats();

            if (Frame != null) final += Frame.BaseStats;
            if (p1 != null) final += p1.StatModifiers;
            if (p2 != null) final += p2.StatModifiers;
            if (p3 != null) final += p3.StatModifiers;
            if (p4 != null) final += p4.StatModifiers;
            if (mw != null) final += mw.GetStatModifiers();
            if (mod != null) final += mod.StatModifiers;

            return final;
        }

        // ===== HELPERS =====

        public bool IsFrameCompatible()
        {
            if (Frame == null) return false;
            return Frame.WeaponType == WeaponType;
        }

        /// <summary>
        /// Vérifie qu'un pool de perks a la bonne colonne.
        /// </summary>
        public bool IsPoolValid(WeaponPerkData[] pool, PerkColumn expectedColumn)
        {
            if (pool == null || pool.Length == 0) return true; // pool vide = OK
            foreach (var p in pool)
            {
                if (p != null && p.Column != expectedColumn) return false;
            }
            return true;
        }

        /// <summary>
        /// Vérifie que tous les pools de perks sont dans les bonnes colonnes.
        /// </summary>
        public bool AreAllPoolsValid()
        {
            if (!IsPoolValid(PerkColumn1Pool, PerkColumn.Column1_Barrel_Sight)) return false;
            if (!IsPoolValid(PerkColumn2Pool, PerkColumn.Column2_Magazine_Battery)) return false;
            if (!IsPoolValid(PerkColumn3Pool, PerkColumn.Column3_Utility)) return false;
            if (!IsPoolValid(PerkColumn4Pool, PerkColumn.Column4_Damage)) return false;
            return true;
        }

        /// <summary>
        /// Retourne le nombre total de combinaisons possibles.
        /// </summary>
        public int TotalCombinations()
        {
            int c1 = (PerkColumn1Pool != null && PerkColumn1Pool.Length > 0) ? PerkColumn1Pool.Length : 1;
            int c2 = (PerkColumn2Pool != null && PerkColumn2Pool.Length > 0) ? PerkColumn2Pool.Length : 1;
            int c3 = (PerkColumn3Pool != null && PerkColumn3Pool.Length > 0) ? PerkColumn3Pool.Length : 1;
            int c4 = (PerkColumn4Pool != null && PerkColumn4Pool.Length > 0) ? PerkColumn4Pool.Length : 1;
            int cmw = (MasterworkPool != null && MasterworkPool.Length > 0) ? MasterworkPool.Length : 1;
            int cmd = (ModPool != null && ModPool.Length > 0) ? ModPool.Length : 1;
            return c1 * c2 * c3 * c4 * cmw * cmd;
        }

        public override string ToString()
        {
            int combos = TotalCombinations();
            return $"Weapon: {WeaponName} | {WeaponType} | Frame: {(Frame != null ? Frame.FrameName : "None")} | {combos} combos";
        }

        // ===== VALIDATION EDITOR =====
        private void OnValidate()
        {
            // Auto-corriger le type si la frame est assignée
            if (Frame != null && Frame.WeaponType != WeaponType)
            {
                Debug.LogWarning($"[{WeaponName}] Le type de la frame ({Frame.WeaponType}) ne correspond pas au type d'arme ({WeaponType}). Correction automatique.");
                // Ne pas auto-corriger pour éviter les surprises, juste avertir
            }
        }
    }
}
