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

        [Header("=== MODIFIERS ===")]
        [SerializeField] private M2922_ModifierContainer _modifierContainer;

        [Header("=== DAMAGE MULTIPLIERS ===")]
        [SerializeField] private float _globalMultiplier = 1f;

        // ===================================================
        // NETWORK DAMAGE (tireur → cible)
        // ===================================================
        [Header("=== NETWORK DAMAGE ===")]
        [UdonSynced] private float _syncedDamage = 0f;
        [UdonSynced] private int _syncedDamageType = 0;
        [UdonSynced] private int _syncedSourceID = -1;
        [UdonSynced] private bool _hasPendingDamage = false;

        protected override void Start()
        {
            base.Start();

            // S'enregistrer auprès du Manager si on est sur un joueur
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (owner != null && owner.isLocal && Manager != null)
                Manager.RegisterReceiver(owner.playerId, this);
        }

        protected override void AutoDetectReferences()
        {
            if (_shield  == null) _shield  = GetComponent<M2922_ShieldComponent>();
            if (_armor   == null) _armor   = GetComponent<M2922_ArmorComponent>();
            if (_health  == null) _health  = GetComponent<M2922_HealthComponent>();
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
        // NETWORK DAMAGE
        // ===================================================

        /// <summary>
        /// Appelé par le TIREUR pour transmettre les dégâts en réseau.
        /// Prend brièvement ownership, écrit les variables synced,
        /// et la cible les applique dans OnDeserialization().
        /// </summary>
        public void SendDamage(float damage, int damageType, VRCPlayerApi source)
        {
            // TOUJOURS appliquer localement d'abord
            ApplyTypedDamage(damage, damageType, source);

            // Puis networker pour les AUTRES clients
            if (Networking.LocalPlayer == null) return;

            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            _syncedDamage = damage;
            _syncedDamageType = damageType;
            _syncedSourceID = source != null ? source.playerId : -1;
            _hasPendingDamage = true;
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (_hasPendingDamage)
            {
                float dmg = _syncedDamage;
                int type = _syncedDamageType;
                int srcID = _syncedSourceID;

                _hasPendingDamage = false;
                _syncedDamage = 0f;

                // Skip si on est le tireur (déjà appliqué localement)
                if (Networking.LocalPlayer != null && Networking.LocalPlayer.playerId == srcID)
                    return;

                // Ré-assert ownership pour que le HealthComponent
                // puisse sync ses propres HP ensuite
                if (!Networking.IsOwner(gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);

                VRCPlayerApi source = srcID >= 0 ? VRCPlayerApi.GetPlayerById(srcID) : null;
                ApplyTypedDamage(dmg, type, source);
            }
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
