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

        [Header("=== RELOAD ===")]
        [Tooltip("Style de rechargement par défaut pour cette frame.")]
        public ReloadStyle ReloadStyle = ReloadStyle.Default;

        [Header("=== PROJECTILE EXPLOSION ===")]
        [Tooltip("Quand le projectile explose : impact, mort, ou les deux. Default = déterminé par le type d'arme.")]
        public ExplosionTrigger ExplosionTrigger = ExplosionTrigger.Default;
        [Tooltip("Délai avant explosion après le déclencheur (secondes). -1 = défaut par type, 0 = instantané.")]
        [Range(-1f, 10f)]
        public float ExplosionDelay = -1f;

        // ===== HELPERS =====
        public string FullName => $"{FrameName} ({WeaponType})";

        public override string ToString()
        {
            return $"Frame: {FrameName} | Type: {WeaponType}";
        }
    }
}
