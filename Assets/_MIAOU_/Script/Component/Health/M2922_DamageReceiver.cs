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
    [AddComponentMenu("M2922/Health/Damage Receiver")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DamageReceiver : M2922_Base
    {
        [Header("=== REFERENCES (auto-détectées) ===")]
        [SerializeField] private M2922_ShieldComponent _shield;
        [SerializeField] private M2922_ArmorComponent _armor;
        [SerializeField] private M2922_HealthComponent _health;
        [SerializeField] private M2922_HitboxSystem _hitboxSystem;

        [Header("=== MODIFIERS ===")]
        [SerializeField] private M2922_ModifierContainer _modifierContainer;

        [Header("=== DAMAGE MULTIPLIERS ===")]
        [SerializeField] private float _globalMultiplier = 1f;

        /// <summary>HitboxSystem de cette entité (pour l'identification réseau).</summary>
        public M2922_HitboxSystem HitboxSystem => _hitboxSystem;

        protected override void Start()
        {
            base.Start();

            // S'enregistrer auprès du Manager via le HitboxSystem (pas via l'ownership réseau,
            // car le premier joueur qui rejoint est propriétaire de tous les objets de scène).
            if (_hitboxSystem != null && _hitboxSystem.BoundPlayerId >= 0 && Manager != null)
                Manager.RegisterReceiver(_hitboxSystem.BoundPlayerId, this);
        }

        protected override void AutoDetectReferences()
        {
            if (_shield  == null) _shield  = GetComponent<M2922_ShieldComponent>();
            if (_armor   == null) _armor   = GetComponent<M2922_ArmorComponent>();
            if (_health  == null) _health  = GetComponent<M2922_HealthComponent>();
            if (_hitboxSystem == null) _hitboxSystem = GetComponent<M2922_HitboxSystem>();
            if (_modifierContainer == null) _modifierContainer = GetComponent<M2922_ModifierContainer>();
        }

        /// <summary>
        /// Applique des dégâts avec type élémentaire.
        /// Le joueur calcule sa résistance avant d'appeler ApplyDamage.
        /// </summary>
        public void ApplyTypedDamage(float rawDamage, int damageTypeId, VRCPlayerApi source = null)
        {
            // === RÉSISTANCE ÉLÉMENTAIRE (calculée par le joueur) ===
            float resistanceMult = GetElementalResistance(damageTypeId);
            float typedDamage = rawDamage * resistanceMult;

            // Déléguer au pipeline standard
            ApplyDamage(typedDamage, source);
        }

        /// <summary>
        /// Calcule la résistance à un type de dégât élémentaire.
        /// Surchargeable : par défaut, lit depuis le ModifierContainer.
        /// </summary>
        private float GetElementalResistance(int damageTypeId)
        {
            // Par défaut : 1f (pas de résistance)
            // Les modificateurs Custom00-Custom04 peuvent être utilisés
            // pour stocker les résistances élémentaires
            if (_modifierContainer == null) return 1f;

            switch (damageTypeId)
            {
                case 0: return 1f; // Kinetic : pas de résistance spéciale
                case 1: return _modifierContainer.GetMultiplicativeTotal(ModifierType.Custom00); // Solar resist
                case 2: return _modifierContainer.GetMultiplicativeTotal(ModifierType.Custom01); // Arc resist
                case 3: return _modifierContainer.GetMultiplicativeTotal(ModifierType.Custom02); // Void resist
                case 4: return _modifierContainer.GetMultiplicativeTotal(ModifierType.Custom03); // Stasis resist
                case 5: return _modifierContainer.GetMultiplicativeTotal(ModifierType.Custom04); // Strand resist
                default: return 1f;
            }
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

        // ===================================================
        // DAMAGE APPLICATION
        // ===================================================

        /// <summary>
        /// Applique les dégâts LOCALEMENT. Appelé par le tireur (hitscan, projectile, beam).
        /// Le relai réseau est géré par le PickupRelay de l'arme du tireur.
        /// </summary>
        public void SendDamage(float damage, int damageType, VRCPlayerApi source)
        {
            ApplyTypedDamage(damage, damageType, source);
        }

        /// <summary>
        /// Applique les dégâts reçus via le réseau (relai PickupRelay).
        /// Appelé depuis PickupRelay.OnDeserialization() sur le client de la cible.
        /// On ne vérifie PAS l'ownership — le relai est déjà filtré par le PickupRelay.
        /// </summary>
        public void ApplyNetworkDamage(float damage, int damageType, VRCPlayerApi source)
        {
            ApplyTypedDamage(damage, damageType, source);
        }

        // NOTE: OnDeserialization() n'est plus utilisé pour le relai de dégâts.
        // Le relai est maintenant géré par M2922_PickupRelay (sur le root des armes).
        // Les HP/Shield sont synchronisés par HealthComponent/ShieldComponent
        // via leur ShouldSync() → IsNetworkingAuthority().

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            bool invincible = _modifierContainer != null && _modifierContainer.HasModifier(ModifierType.Invincibility);
            string hitboxStr = _hitboxSystem != null
                ? (_hitboxSystem.BoundPlayerId >= 0 ? $"Player {_hitboxSystem.BoundPlayerId}" : "NPC/World")
                : "Missing";
            Color hitboxColor = _hitboxSystem != null
                ? (_hitboxSystem.BoundPlayerId >= 0 ? Color.green : Color.grey)
                : Color.red;
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Shield", _shield != null ? "ON" : "OFF", _shield != null ? Color.cyan : Color.gray),
                new M2922_GizmoDisplayInfo("Armor", _armor != null ? "ON" : "OFF", _armor != null ? Color.cyan : Color.gray),
                new M2922_GizmoDisplayInfo("Health", _health != null ? "ON" : "OFF", _health != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("HitboxSys", hitboxStr, hitboxColor),
                new M2922_GizmoDisplayInfo("Modifiers", _modifierContainer != null ? "Linked" : "None", _modifierContainer != null ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Invincible", invincible ? "YES" : "NO", invincible ? Color.red : Color.gray),
            };
        }
#endif
    }
}
