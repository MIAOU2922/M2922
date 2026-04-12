using UdonSharp;
using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour tout ce qui peut recevoir des dégâts
    /// (joueurs, véhicules, objets destructibles)
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Santé actuelle</summary>
        float Health { get; }
        
        /// <summary>Santé maximum</summary>
        float MaxHealth { get; }
        
        /// <summary>Est vivant/actif ?</summary>
        bool IsAlive { get; }
        
        /// <summary>Peut être endommagé actuellement ? (invincibilité, etc.)</summary>
        bool CanTakeDamage { get; }
        
        /// <summary>
        /// Appliquer des dégâts à cette entité
        /// </summary>
        /// <param name="damage">Montant des dégâts</param>
        /// <param name="attackerId">ID de l'attaquant (-1 si environnement)</param>
        /// <param name="damageType">Type de dégât</param>
        void TakeDamage(float damage, int attackerId, DamageType damageType);
        
        /// <summary>
        /// Soigner cette entité
        /// </summary>
        /// <param name="amount">Montant de soin</param>
        void Heal(float amount);
        
        /// <summary>
        /// Tuer/détruire cette entité
        /// </summary>
        /// <param name="killerId">ID du tueur (-1 si suicide/environnement)</param>
        void Die(int killerId);
    }
    
    public enum DamageType
    {
        Generic = 0,
        Bullet = 1,
        Melee = 2,
        Explosion = 3,
        Fire = 4,
        Fall = 5,
        Drowning = 6,
        Collision = 7,
        Environmental = 8
    }
}
