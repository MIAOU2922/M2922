using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Classe de base pour toute entité du monde PvP.
    /// Regroupe les composants combat communs et exécute le pipeline de dégâts.
    ///
    /// HIÉRARCHIE :
    ///   M2922_Base
    ///   └── M2922_Entity              ← cette classe
    ///       └── M2922_PlayerController  ← override avec FF check + team + stats
    ///
    /// UTILISATION DIRECTE (sans sous-classe) :
    ///   Prop, véhicule, tourelle, NPC, boss, objet destructible…
    ///   Ajouter M2922_Entity + M2922_HealthController sur le root.
    ///   Ajouter M2922_ArmorSystem / M2922_BuffSystem si nécessaire.
    ///
    /// PIPELINE TakeDamage :
    ///   ① BuffSystem.DamageIn  (invincibilité, réduction temporaire)
    ///   ② ArmorSystem          (absorption par zone)
    ///   ③ HealthController     (HP finaux)
    ///
    /// PIPELINE JOUEUR (override PlayerController) :
    ///   ① TeamManager FF check → ② BuffSystem → ③ Armor → ④ HealthController
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Entity : M2922_Base
    {
        [Header("=== ENTITY INFO ===")]
        [SerializeField] protected int _entityId = -1;
        [SerializeField] protected string _entityName = "Entity";

        [Header("=== COMBAT MODULES (auto-détectés si vides) ===")]
        [Tooltip("HealthController — requis pour subir des dégâts.")]
        [SerializeField] protected M2922.Combat.M2922_HealthController _healthController;

        [Tooltip("ArmorSystem — absorption des dégâts avant HP. Optionnel.")]
        [SerializeField] protected M2922.Combat.M2922_ArmorSystem _armorSystem;

        [Tooltip("BuffSystem — buffs/debuffs temporaires. Optionnel.")]
        [SerializeField] protected M2922.Combat.M2922_BuffSystem _buffSystem;

        // === LIFECYCLE ===

        protected override void Start()
        {
            base.Start();

            if (_healthController == null)
                _healthController = GetComponentInParent<M2922.Combat.M2922_HealthController>();
            if (_armorSystem == null)
                _armorSystem = GetComponentInParent<M2922.Combat.M2922_ArmorSystem>();
            if (_buffSystem == null)
                _buffSystem = GetComponentInParent<M2922.Combat.M2922_BuffSystem>();
        }

        // === ÉTAT ===

        public int   EntityId    => _entityId;
        public string EntityName => _entityName;

        public bool  IsAlive       => _healthController != null && _healthController.IsAlive;
        public float Health        => _healthController != null ? _healthController.Health    : 0f;
        public float MaxHealth     => _healthController != null ? _healthController.MaxHealth : 0f;
        public float HealthPercent => MaxHealth > 0f ? Health / MaxHealth : 0f;

        // === PIPELINE DÉGÂTS (virtual → surchargé dans PlayerController) ===

        /// <summary>
        /// Reçoit des dégâts sur la zone Body (générique).
        /// Pipeline : Buff(DamageIn) → Armor(Body) → HealthController
        /// </summary>
        public virtual void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            if (_healthController == null || damage <= 0f) return;

            float remaining = damage;

            // ① Buff : réduction / invincibilité
            if (_buffSystem != null)
            {
                float mult = _buffSystem.GetStatMultiplier(StatType.DamageIn);
                if (mult == 0f) return;
                remaining *= mult;
            }

            // ② Armure
            if (_armorSystem != null)
                remaining = _armorSystem.AbsorbDamage(remaining, damageType);

            // ③ HP
            _healthController.TakeDamage(remaining, attackerId, damageType);
        }

        /// <summary>Soigne l'entité du montant indiqué.</summary>
        public void Heal(float amount)
        {
            if (_healthController != null) _healthController.Heal(amount);
        }

        // === RESET (respawn générique) ===

        /// <summary>
        /// Remet l'entité à son état initial : HP max, armure pleine, buffs effacés.
        /// Appelé au respawn. Surchargé dans les sous-classes pour du comportement additionnel.
        /// </summary>
        public virtual void ResetModules()
        {
            if (_healthController != null) _healthController.Respawn();
            if (_armorSystem != null)      _armorSystem.RepairFull();
            if (_buffSystem != null)       _buffSystem.ClearAllBuffs();
        }

        // === BUFFS (délèguent au BuffSystem, no-op si absent) ===

        public void ApplyBuff(BuffType buffType, float magnitude, float duration)
        {
            if (_buffSystem != null) _buffSystem.ApplyBuff(buffType, magnitude, duration);
        }

        public void RemoveBuff(BuffType buffType)
        {
            if (_buffSystem != null) _buffSystem.RemoveBuff(buffType);
        }

        public bool HasBuff(BuffType buffType)
            => _buffSystem != null && _buffSystem.HasBuff(buffType);

        public float GetStatMultiplier(StatType statType)
            => _buffSystem != null ? _buffSystem.GetStatMultiplier(statType) : 1f;

        public bool IsStunned()    => _buffSystem != null && _buffSystem.HasBuff(BuffType.Stun);
        public bool IsInvincible() => _buffSystem != null && _buffSystem.HasBuff(BuffType.Invincibility);

        public float GetMoveSpeedMultiplier()   => GetStatMultiplier(StatType.Speed);
        public float GetDamageOutMultiplier()   => GetStatMultiplier(StatType.DamageOut);
        public float GetReloadSpeedMultiplier() => GetStatMultiplier(StatType.ReloadSpeed);

        // === ARMURE (délèguent au ArmorSystem, no-op si absent) ===

        public float GetArmorPoints()    => _armorSystem != null ? _armorSystem.ArmorPoints    : 0f;
        public float GetMaxArmorPoints() => _armorSystem != null ? _armorSystem.MaxArmorPoints : 0f;

        public void RepairArmor(float amount)
        {
            if (_armorSystem != null) _armorSystem.RepairArmor(amount);
        }

        // === ACCESSEURS MODULES ===

        public M2922.Combat.M2922_HealthController GetHealthController() => _healthController;
        public M2922.Combat.M2922_ArmorSystem      GetArmorSystem()      => _armorSystem;
        public M2922.Combat.M2922_BuffSystem        GetBuffSystem()       => _buffSystem;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_healthController == null)
                _healthController = GetComponentInParent<M2922.Combat.M2922_HealthController>();
            if (_armorSystem == null)
                _armorSystem = GetComponentInParent<M2922.Combat.M2922_ArmorSystem>();
            if (_buffSystem == null)
                _buffSystem = GetComponentInParent<M2922.Combat.M2922_BuffSystem>();
        }
#endif
    }
}
