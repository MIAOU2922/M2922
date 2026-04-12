using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour tout système d'armure absorbant les dégâts.
    /// Placé dans le pipeline APRÈS IHittable et AVANT HealthController.
    /// 
    /// PIPELINE : IHittable → IArmored.AbsorbDamage() → HealthController.TakeDamage()
    /// 
    /// Implémenté par : M2922_ArmorSystem
    /// </summary>
    public interface IArmored
    {
        /// <summary>Points d'armure globaux actuels (somme de toutes les zones)</summary>
        float ArmorPoints { get; }

        /// <summary>Points d'armure globaux maximum</summary>
        float MaxArmorPoints { get; }

        /// <summary>L'armure est-elle intacte (> 0) ?</summary>
        bool HasArmor { get; }

        /// <summary>
        /// Absorbe une partie des dégâts selon l'armure de la zone touchée.
        /// Dégrade les points d'armure de la zone en conséquence.
        /// </summary>
        /// <param name="incomingDamage">Dégâts reçus après multiplicateur de zone</param>
        /// <param name="damageType">Type de dégât (influence l'absorption)</param>
        /// <param name="zoneType">Zone anatomique touchée</param>
        /// <returns>Dégâts restants après absorption par l'armure</returns>
        float AbsorbDamage(float incomingDamage, DamageType damageType, HitZoneType zoneType);

        /// <summary>
        /// Répare l'armure du montant indiqué (dans la limite du maximum).
        /// </summary>
        /// <param name="amount">Points d'armure à restaurer</param>
        void RepairArmor(float amount);

        /// <summary>
        /// Répare intégralement toute l'armure (utilisé au respawn).
        /// </summary>
        void RepairFull();
    }

    /// <summary>
    /// Type de résistance d'une pièce d'armure.
    /// Définit contre quels types de dégâts l'armure est efficace.
    /// </summary>
    public enum ArmorType
    {
        Light     = 0,  // Légère : efficace contre Bullet, peu contre Explosion
        Heavy     = 1,  // Lourde : efficace contre tout, réduit la mobilité
        Explosive = 2,  // Anti-explosion : résiste aux dégâts de zone
        Energy    = 3,  // Énergie : résiste aux dégâts de feu/électrique
    }
}
