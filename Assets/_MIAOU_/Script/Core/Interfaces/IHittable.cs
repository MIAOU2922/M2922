using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour tout collider pouvant recevoir un impact physique.
    /// Point d'entrée des dégâts AVANT le HealthController.
    /// 
    /// PIPELINE : Source → IHittable.OnHit() → [Armor] → [Resistance] → HealthController.TakeDamage()
    /// 
    /// Implémenté par : M2922_HitZone
    /// </summary>
    public interface IHittable
    {
        /// <summary>
        /// Ce collider est-il actuellement actif (peut recevoir des hits) ?
        /// </summary>
        bool IsHittable { get; }

        /// <summary>
        /// Multiplicateur de dégâts de cette zone (ex: 2.0 pour headshot, 0.75 pour membre).
        /// </summary>
        float DamageMultiplier { get; }

        /// <summary>
        /// Identifiant de la zone (corps, tête, membre, critique…).
        /// Utilisé par ArmorSystem pour savoir quelle armure de zone consulter.
        /// </summary>
        HitZoneType ZoneType { get; }

        /// <summary>
        /// Appelé par une arme ou un projectile lors d'un impact sur ce collider.
        /// Applique le multiplicateur de zone, consulte l'armure et transmet au HealthController.
        /// </summary>
        /// <param name="rawDamage">Dégâts bruts de la source</param>
        /// <param name="attackerId">PlayerId ou EntityId de l'attaquant (-1 = environnement)</param>
        /// <param name="damageType">Type de dégât (Bullet, Explosion, Melee…)</param>
        /// <param name="hitPoint">Position mondiale de l'impact</param>
        /// <param name="hitNormal">Normale de surface au point d'impact</param>
        void OnHit(float rawDamage, int attackerId, DamageType damageType,
                   Vector3 hitPoint, Vector3 hitNormal);
    }

    /// <summary>
    /// Identifiant de zone anatomique pour le hit detection et l'armure.
    /// </summary>
    public enum HitZoneType
    {
        Body     = 0,   // Zone corps principal   ×1.0
        Head     = 1,   // Headshot               ×2.0 par défaut
        Critical = 2,   // Zone critique (dos…)   ×1.25 par défaut
        Limb     = 3,   // Bras / jambes          ×0.75 par défaut
        Armored  = 4,   // Zone protégée          ×0.5 par défaut (avant armure)
    }
}
