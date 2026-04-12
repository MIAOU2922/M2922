using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour toute entité pouvant recevoir des buffs et debuffs temporaires.
    /// Le BuffSystem expose des multiplicateurs consultés par les autres systèmes.
    /// 
    /// RÈGLE : BuffSystem ne modifie PAS directement les champs des autres scripts.
    ///         Il expose des multiplicateurs que HealthController, PlayerController etc. lisent.
    /// 
    /// Implémenté par : M2922_BuffSystem
    /// </summary>
    public interface IBuffable
    {
        /// <summary>Au moins un buff/debuff est-il actif ?</summary>
        bool HasActiveBuff { get; }

        /// <summary>
        /// Applique un buff ou debuff temporaire.
        /// Si le buff est déjà actif et non stackable, renouvelle la durée.
        /// </summary>
        /// <param name="buffType">Type de modification</param>
        /// <param name="magnitude">Force du modificateur (ex: 1.5 = +50%, 0.5 = -50%)</param>
        /// <param name="duration">Durée en secondes (0 = permanent jusqu'à RemoveBuff)</param>
        void ApplyBuff(BuffType buffType, float magnitude, float duration);

        /// <summary>
        /// Retire immédiatement un buff actif.
        /// </summary>
        void RemoveBuff(BuffType buffType);

        /// <summary>
        /// Le buff du type indiqué est-il actuellement actif ?
        /// </summary>
        bool HasBuff(BuffType buffType);

        /// <summary>
        /// Retourne le multiplicateur actuel pour une stat donnée.
        /// Prend en compte tous les buffs actifs affectant cette stat.
        /// Ex: GetStatMultiplier(StatType.Speed) peut retourner 1.5 si SpeedBoost actif.
        /// </summary>
        float GetStatMultiplier(StatType statType);

        /// <summary>
        /// Retire tous les buffs actifs (utilisé au respawn ou fin de partie).
        /// </summary>
        void ClearAllBuffs();
    }

    /// <summary>
    /// Types de buff/debuff disponibles.
    /// </summary>
    public enum BuffType
    {
        SpeedBoost       = 0,
        SlowDown         = 1,
        DamageBoost      = 2,   // Augmente les dégâts infligés
        DamageReduction  = 3,   // Réduit les dégâts reçus
        HealthRegen      = 4,   // Régénération de HP accélérée
        ShieldRegen      = 5,   // Régénération d'armure accélérée
        Invincibility    = 6,   // Aucun dégât reçu (spawn protection)
        Invisibility     = 7,   // Non ciblable par l'IA, réduit détection
        Poison           = 8,   // Dégâts continus (DamageType.Poison)
        Burn             = 9,   // Dégâts continus (DamageType.Fire)
        Stun             = 10,  // Empêche le mouvement/l'attaque
        ReloadSpeed      = 11,  // Accélère le rechargement
    }

    /// <summary>
    /// Stats pouvant être modifiées par les buffs.
    /// Utilisées dans GetStatMultiplier().
    /// </summary>
    public enum StatType
    {
        Speed        = 0,
        MaxHealth    = 1,
        DamageOut    = 2,   // Dégâts infligés
        DamageIn     = 3,   // Dégâts reçus
        ReloadSpeed  = 4,
        JumpHeight   = 5,
    }
}
