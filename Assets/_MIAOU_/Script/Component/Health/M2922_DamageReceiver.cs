using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Modifier;

namespace M2922.Component.Health
{
    /// <summary>
    /// Pipeline de réception des dégâts : invincibility → shield → armor → health.
    /// Intègre le ModifierContainer pour les buffs/debuffs (invincibilité, armor+, etc.).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DamageReceiver : M2922_Base
    {
        [Header("=== REFERENCES (auto-détectées) ===")]
        [SerializeField] private M2922_ShieldComponent _shield;
        [SerializeField] private M2922_ArmorComponent _armor;
        [SerializeField] private M2922_HealthComponent _health;

        [Header("=== MODIFIERS ===")]
        [SerializeField] private M2922_ModifierContainer _modifierContainer;

        [Header("=== DAMAGE MULTIPLIERS ===")]
        [SerializeField] private float _globalMultiplier = 1f;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_shield  == null) _shield  = GetComponent<M2922_ShieldComponent>();
            if (_armor   == null) _armor   = GetComponent<M2922_ArmorComponent>();
            if (_health  == null) _health  = GetComponent<M2922_HealthComponent>();
            if (_modifierContainer == null) _modifierContainer = GetComponent<M2922_ModifierContainer>();
        }

        /// <summary>
        /// Applique des dégâts : invincibility check → shield → armor → health.
        /// </summary>
        public void ApplyDamage(float rawDamage, VRCPlayerApi source = null, bool isCrit = false)
        {
            // === INVINCIBILITY CHECK ===
            if (_modifierContainer != null && _modifierContainer.HasModifier(ModifierType.Invincibility))
            {
                this.Log($"Damage ignored: invincibility active");
                return;
            }

            float damage = rawDamage * _globalMultiplier;

            // === SHIELD (avec modificateur multiplicatif) ===
            if (_shield != null && _shield.IsActive)
            {
                float shieldMult = _modifierContainer != null
                    ? _modifierContainer.GetMultiplicativeTotal(ModifierType.Shield)
                    : 1f;
                float shieldAdd = _modifierContainer != null
                    ? _modifierContainer.GetAdditiveTotal(ModifierType.Shield)
                    : 0f;

                // Applique le multiplicateur au shield max (géré par ShieldComponent)
                damage = _shield.AbsorbDamage(damage);
            }

            // === ARMOR (avec modificateur additif) ===
            if (_armor != null && damage > 0f)
            {
                float armorAdd = _modifierContainer != null
                    ? _modifierContainer.GetAdditiveTotal(ModifierType.Armor)
                    : 0f;
                float armorMult = _modifierContainer != null
                    ? _modifierContainer.GetMultiplicativeTotal(ModifierType.Armor)
                    : 1f;

                float baseReduced = _armor.ReduceDamage(damage);
                // Bonus d'armor des modifiers : réduction supplémentaire
                damage = Mathf.Max(0f, baseReduced - armorAdd) * (1f / armorMult);
            }

            // === HEALTH (avec modificateur multiplicatif) ===
            if (_health != null && damage > 0f)
            {
                float hpMult = _modifierContainer != null
                    ? _modifierContainer.GetMultiplicativeTotal(ModifierType.MaxHealth)
                    : 1f;
                _health.TakeDamage(damage); // HealthComponent gère son propre maxHP
            }

            string sourceName = source != null ? source.displayName : "world";
            string critStr = isCrit ? " [CRIT]" : "";
            this.Log($"Damage{critStr}: raw={rawDamage:F1}, final={damage:F1}, source={sourceName}");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            bool invincible = _modifierContainer != null && _modifierContainer.HasModifier(ModifierType.Invincibility);
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Shield", _shield != null ? "ON" : "OFF", _shield != null ? Color.cyan : Color.gray),
                new M2922_GizmoDisplayInfo("Armor", _armor != null ? "ON" : "OFF", _armor != null ? Color.cyan : Color.gray),
                new M2922_GizmoDisplayInfo("Health", _health != null ? "ON" : "OFF", _health != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Modifiers", _modifierContainer != null ? "Linked" : "None", _modifierContainer != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Invincible", invincible ? "YES" : "NO", invincible ? Color.red : Color.gray),
            };
        }
#endif
    }
}
