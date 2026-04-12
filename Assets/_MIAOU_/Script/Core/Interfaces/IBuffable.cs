using UnityEngine;

namespace M2922.Core
{
    /// Interface pour toute entité pouvant recevoir des buffs et debuffs temporaires.
    public interface IBuffable
    {
        bool HasActiveBuff { get; }
        void ApplyBuff(BuffType buffType, float magnitude, float duration);
        void RemoveBuff(BuffType buffType);
        bool HasBuff(BuffType buffType);
        float GetStatMultiplier(StatType statType);
        void ClearAllBuffs();
    }

    /// Types de buff/debuff disponibles.
    public enum BuffType
    {
        MoveSpeed     = 0,  // magnitude = multiplicateur de vitesse
        JumpHeight    = 1,  // magnitude = multiplicateur de saut
        HealthRegen   = 2,  // magnitude = HP régénérés par seconde
        MaxHealth     = 3,  // magnitude = multiplicateur de HP max
        PassiveDamage = 4,  // magnitude = dégâts par seconde (DamageType.Generic)
        ArmorRegen    = 5,  // magnitude = AP régénérés par seconde
        MaxArmor      = 6,  // magnitude = multiplicateur d'armor max
        Invincibility = 7,  // magnitude ignorée — immunité totale aux dégâts
        Stun          = 8,  // magnitude ignorée — bloque les actions
        Flash         = 9,  // magnitude ignorée — aveugle la cible
    }

    /// Statistique affectée par un buff/debuff.
    public enum StatType
    {
        Speed         = 0,  // vitesse de déplacement
        JumpHeight    = 1,  // hauteur de saut
        HealRate      = 2,  // HP régénérés par seconde
        MaxHealth     = 3,  // multiplicateur de HP max
        PassiveDamage = 4,  // dégâts passifs par seconde
        ArmorHealRate = 5,  // AP régénérés par seconde
        MaxArmor      = 6,  // multiplicateur d'armor max
        Invincibility = 7,  // > 0 = invincible
        Stun          = 8,  // > 0 = stun actif
        Flash         = 9,  // > 0 = flash actif
    }
}
