using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Définit un perk (trait) avec ses bonus/malus de stats.
    /// Clic-droit → Create → M2922 → Weapons → Perk
    /// </summary>
    [CreateAssetMenu(fileName = "Perk_", menuName = "M2922/Weapons/Perk", order = 20)]
    public class WeaponPerkData : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [Tooltip("Nom du perk")]
        public string PerkName;

        [Tooltip("Colonne où ce perk peut être équipé")]
        public PerkColumn Column;

        [TextArea(2, 4)]
        [Tooltip("Description de l'effet du perk")]
        public string Description;

        [Header("=== STAT MODIFIERS ===")]
        [Tooltip("Bonus/malus de stats apportés par ce perk")]
        public WeaponBaseStats StatModifiers;

        [Header("=== VISUAL ===")]
        [Tooltip("Icône optionnelle du perk (affichée dans l'UI)")]
        public Sprite Icon;

        // ===== HELPERS =====
        public string FullName => $"[{Column}] {PerkName}";

        public override string ToString()
        {
            return $"Perk: {PerkName} | Column: {Column}";
        }
    }
}
