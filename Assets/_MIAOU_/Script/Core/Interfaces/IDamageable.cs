using UdonSharp;
using UnityEngine;

namespace M2922.Core
{
    /// Interface pour tout ce qui peut recevoir des dégâts
    /// (joueurs, véhicules, objets destructibles)
    public interface IDamageable
    {
        float Health { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        bool CanTakeDamage { get; }
        void TakeDamage(float damage, int attackerId, DamageType damageType);
        void Heal(float amount);
        void Die(int killerId);
    }

    /// Type de dégât — influe sur la résistance de l'armure et les effets visuels.
    public enum DamageType
    {
        Generic   = 0,
        Bullet    = 1,
        Explosion = 2,
        Melee     = 3,
        Fire      = 4,
        Energy    = 5,
        Fall      = 6,
    }
}
