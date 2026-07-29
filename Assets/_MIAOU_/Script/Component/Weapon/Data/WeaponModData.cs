using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Définit un mod d'arme avec ses effets.
    /// Clic-droit → Create → M2922 → Weapons → Mod
    /// </summary>
    [CreateAssetMenu(fileName = "Mod_", menuName = "M2922/Weapons/Mod", order = 40)]
    public class WeaponModData : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [Tooltip("Nom du mod")]
        public string ModName;

        [TextArea(2, 4)]
        [Tooltip("Description de l'effet du mod")]
        public string Description;

        [Tooltip("Type d'armes compatibles (laisser vide = toutes)")]
        public WeaponType[] CompatibleWeaponTypes;

        [Header("=== STAT MODIFIERS ===")]
        [Tooltip("Bonus/malus de stats apportés par ce mod")]
        public WeaponBaseStats StatModifiers;

        [Header("=== SPECIAL ===")]
        [Tooltip("Multiplicateur de dégâts contre les Bosses/Véhicules (ex: 1.077 = +7.7%)")]
        public float BossDamageMultiplier = 1f;

        [Tooltip("Multiplicateur de dégâts contre les ennemis Puissants/Champions")]
        public float MajorDamageMultiplier = 1f;

        [Tooltip("Multiplicateur de dégâts contre les ennemis de base")]
        public float MinorDamageMultiplier = 1f;

        [Tooltip("Multiplicateur de dégâts contre les joueurs (PvP)")]
        public float PlayerDamageMultiplier = 1f;

        [Header("=== VISUAL ===")]
        [Tooltip("Icône optionnelle du mod")]
        public Sprite Icon;

        // ===== HELPERS =====
        public bool IsCompatibleWith(WeaponType weaponType)
        {
            if (CompatibleWeaponTypes == null || CompatibleWeaponTypes.Length == 0)
                return true; // Compatible avec tout

            foreach (var t in CompatibleWeaponTypes)
            {
                if (t == weaponType) return true;
            }
            return false;
        }

        public override string ToString()
        {
            return $"Mod: {ModName}";
        }
    }
}
