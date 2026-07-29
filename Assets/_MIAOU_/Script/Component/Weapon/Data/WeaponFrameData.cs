using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Définit une frame d'arme (archétype) avec ses stats de base.
    /// Clic-droit → Create → M2922 → Weapons → Frame
    /// </summary>
    [CreateAssetMenu(fileName = "Frame_", menuName = "M2922/Weapons/Frame", order = 10)]
    public class WeaponFrameData : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [Tooltip("Nom de la frame (ex: 'High-Impact Frame', 'Precision Frame')")]
        public string FrameName;

        [Tooltip("Type d'arme auquel cette frame appartient")]
        public WeaponType WeaponType;

        [TextArea(2, 4)]
        [Tooltip("Description de la frame")]
        public string Description;

        [Header("=== STATS DE BASE ===")]
        [Tooltip("Stats brutes de la frame (les perks/mods/masterwork s'ajoutent par-dessus)")]
        public WeaponBaseStats BaseStats;

        // ===== HELPERS =====
        public string FullName => $"{FrameName} ({WeaponType})";

        public override string ToString()
        {
            return $"Frame: {FrameName} | Type: {WeaponType}";
        }
    }
}
