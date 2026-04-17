using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Combat;
using CoreEventType = M2922.Core.EventType;

namespace M2922.Entity.Prop
{
    //  Classe Prop de base, pouvant être endommagée et détruite. Peut être étendue pour des comportements spécifiques (portes, caisses, etc.)
    public class M2922_Prop : M2922_Entity
    {
        // === SYSTEMS (optionnels — assigner dans l'Inspector si présents) ===
        [Header("=== SYSTEMS ===")]

        public M2922_HealthSystem HealthSystem; // null = pas de santé (invincible)

        public M2922_BuffSystem BuffSystem;    // null = pas de buffs
        // === HELPERS ===
        public bool HasHealth => HealthSystem != null;
        public bool HasBuffs  => BuffSystem  != null;
        // === HELPER METHODS ===
        private void TryFindSystems()
        {
            if (HealthSystem == null) HealthSystem = GetComponent<M2922_HealthSystem>();
            if (BuffSystem  == null) BuffSystem  = GetComponent<M2922_BuffSystem>();
        }

        // === IDamageable (inline) ===
        public float Health       => HasHealth ? HealthSystem.Health    : float.MaxValue;
        public float MaxHealth    => HasHealth ? HealthSystem.MaxHealth : float.MaxValue;
        public bool IsAlive       => HasHealth ? HealthSystem.IsAlive   : true;
        public bool CanTakeDamage => HasHealth && HealthSystem.CanTakeDamage;

        public void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            if (!CanTakeDamage) return;
            if (HasBuffs && BuffSystem.GetStatMultiplier(StatType.Invincibility) > 0f) return;
            if (damage > 0f) HealthSystem.TakeDamage(damage, attackerId, damageType);
        }

        public void Heal(float amount)          { if (HasHealth) HealthSystem.Heal(amount); }
        public void Die(int killerId)           { if (HasHealth) HealthSystem.Die(killerId); } 

        // === IBuffable (inline) ===
        public bool HasActiveBuff => HasBuffs && BuffSystem.HasActiveBuff;
        public void ApplyBuff(BuffType buffType, float magnitude, float duration)
            { if (HasBuffs) BuffSystem.ApplyBuff(buffType, magnitude, duration); }
        public void RemoveBuff(BuffType buffType)
            { if (HasBuffs) BuffSystem.RemoveBuff(buffType); }
        public bool HasBuff(BuffType buffType)
            => HasBuffs && BuffSystem.HasBuff(buffType);
        public float GetStatMultiplier(StatType statType)
            => HasBuffs ? BuffSystem.GetStatMultiplier(statType) : (statType == StatType.Speed || statType == StatType.JumpHeight || statType == StatType.MaxHealth || statType == StatType.MaxArmor ? 1f : 0f);
        public void ClearAllBuffs()             { if (HasBuffs) BuffSystem.ClearAllBuffs(); }
         // === METHODE ===

        protected override void Start()
        {
            base.Start();
            TryFindSystems();
            ApplyBuffStaticEffects();
        }

        protected override void Update() {
            base.Update();

            if (!HasBuffs) return;

            float dt = Time.deltaTime;

            // HealthRegen
            float healRate = BuffSystem.GetStatMultiplier(StatType.HealRate);
            if (healRate > 0f && HasHealth) HealthSystem.Heal(healRate * dt);

            // PassiveDamage (dégâts sur soi-même — ex: poison, feu)
            // Passe par TakeDamage du controller pour que l'armure absorbe d'abord
            float passiveDmg = BuffSystem.GetStatMultiplier(StatType.PassiveDamage);
            if (passiveDmg > 0f && HasHealth)
                TakeDamage(passiveDmg * dt, -1, BuffSystem.GetPassiveDamageType());
        }
        
        /// Applique les effets de multiplicateurs (MaxHealth, MaxArmor) au démarrage ou lors d'un changement de buff.
        private void ApplyBuffStaticEffects()
        {
            if (!HasBuffs) return;

            if (HasHealth)
            {
                float maxHpMult = BuffSystem.GetStatMultiplier(StatType.MaxHealth);
                if (maxHpMult != 1f) HealthSystem.SetMaxHealth(HealthSystem.MaxHealth * maxHpMult);
            }
        }
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            TryFindSystems();
        }
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;
        }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
        }
#endif
    }
}