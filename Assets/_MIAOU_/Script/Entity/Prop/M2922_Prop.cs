using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Combat;

namespace M2922.Entity.Prop
{
    /// <summary>
    /// Prop générique. Chaque capacité est optionnelle et activée indépendamment.
    ///
    /// ─── CAPACITÉS ────────────────────────────────────────────────────────────
    ///   _isGrabbable  : le prop peut être ramassé / lâché par un joueur (VRC_Pickup)
    ///   _isDamageable : le prop peut recevoir des dégâts (HealthSystem requis)
    ///
    /// ─── EXEMPLES ─────────────────────────────────────────────────────────────
    ///   Caisse explosive         → _isDamageable = true,  _isGrabbable = false
    ///   Objet ramassable inerte  → _isGrabbable  = true,  _isDamageable = false
    ///   Bombe portable           → _isGrabbable  = true,  _isDamageable = true
    ///   Déco statique            → les deux à false
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_Prop : M2922_Entity
    {
        // =====================================================================
        // CAPABILITIES
        // =====================================================================
        [Header("=== CAPABILITIES ===")]
        [Tooltip("Ce prop peut être ramassé et lâché par un joueur.\n" +
                 "Nécessite un composant VRC_Pickup sur ce GameObject.")]
        [SerializeField] private bool _isGrabbable = false;

        [Tooltip("Ce prop peut recevoir des dégâts.\n" +
                 "Nécessite un composant M2922_HealthSystem assigné.")]
        [SerializeField] private bool _isDamageable = false;

        // =====================================================================
        // SYSTEMS (optionnels)
        // =====================================================================
        [Header("=== SYSTEMS ===")]
        [Tooltip("Système de santé. Requis si _isDamageable est activé.")]
        public M2922_HealthSystem HealthSystem;

        [Tooltip("Système de buffs. Optionnel — buffs/debuffs appliqués à ce prop.")]
        public M2922_BuffSystem BuffSystem;

        // =====================================================================
        // GRAB CONFIG
        // =====================================================================
        [Header("=== GRAB CONFIG ===")]
        [Tooltip("Délai (sec) avant que le prop retourne à son origine après avoir été lâché.\n" +
                 "-1 = reste là où il est posé.")]
        [SerializeField] private float _returnDelay = 10f;

        // =====================================================================
        // RUNTIME
        // =====================================================================
        private VRC_Pickup _pickup;
        private bool  _isHeld;
        private float _dropTime;
        private bool  _pendingReturn;

        [UdonSynced] private Vector3    _originPosition;
        [UdonSynced] private Quaternion _originRotation;

        // =====================================================================
        // PROPERTIES
        // =====================================================================
        public bool IsGrabbable  => _isGrabbable;
        public bool IsDamageable => _isDamageable;
        public bool IsHeld       => _isHeld;
        public bool HasHealth    => HealthSystem != null;
        public bool HasBuffs     => BuffSystem   != null;

        // Damageable (effectif seulement si _isDamageable)
        public float Health        => (_isDamageable && HasHealth) ? HealthSystem.Health    : float.MaxValue;
        public float MaxHealth     => (_isDamageable && HasHealth) ? HealthSystem.MaxHealth : float.MaxValue;
        public bool  IsAlive       => (_isDamageable && HasHealth) ? HealthSystem.IsAlive   : true;
        public bool  CanTakeDamage => _isDamageable && HasHealth && HealthSystem.CanTakeDamage;

        // =====================================================================
        // DAMAGE API
        // =====================================================================
        public void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            if (!CanTakeDamage) return;
            if (HasBuffs && BuffSystem.GetStatMultiplier(StatType.Invincibility) > 0f) return;
            if (damage > 0f) HealthSystem.TakeDamage(damage, attackerId, damageType);
        }

        public void Heal(float amount)    { if (_isDamageable && HasHealth) HealthSystem.Heal(amount); }
        public void Die(int killerId)     { if (_isDamageable && HasHealth) HealthSystem.Die(killerId); }

        // =====================================================================
        // BUFF API
        // =====================================================================
        public bool HasActiveBuff => HasBuffs && BuffSystem.HasActiveBuff;

        public void ApplyBuff(BuffType buffType, float magnitude, float duration)
            { if (HasBuffs) BuffSystem.ApplyBuff(buffType, magnitude, duration); }
        public void RemoveBuff(BuffType buffType)
            { if (HasBuffs) BuffSystem.RemoveBuff(buffType); }
        public bool HasBuff(BuffType buffType)
            => HasBuffs && BuffSystem.HasBuff(buffType);
        public float GetStatMultiplier(StatType statType)
            => HasBuffs ? BuffSystem.GetStatMultiplier(statType) : 1f;
        public void ClearAllBuffs() { if (HasBuffs) BuffSystem.ClearAllBuffs(); }

        // =====================================================================
        // GRAB API
        // =====================================================================

        /// <summary>
        /// Retourne le prop à sa position d'origine.
        /// Drop explicite si tenu, pour éviter MissingReferenceException dans VRC_Pickup.
        /// </summary>
        public void ReturnToOrigin()
        {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            if (_pickup != null && _isHeld)
                _pickup.Drop();

            transform.SetPositionAndRotation(_originPosition, _originRotation);
            this.VerboseLog($"[Prop] {_entityName} retourné à l'origine");
        }

        // =====================================================================
        // VRC_PICKUP CALLBACKS
        // =====================================================================
        public override void OnPickup()
        {
            if (!_isGrabbable) return;
            _isHeld        = true;
            _pendingReturn = false;
            this.VerboseLog($"[Prop] {_entityName} ramassé");
        }

        public override void OnDrop()
        {
            _isHeld = false;
            if (_isGrabbable && _returnDelay >= 0f)
            {
                _dropTime      = Time.time;
                _pendingReturn = true;
            }
            this.VerboseLog($"[Prop] {_entityName} lâché");
        }

        // =====================================================================
        // LIFECYCLE
        // =====================================================================
        protected override void Start()
        {
            base.Start();

            if (HealthSystem == null) HealthSystem = GetComponent<M2922_HealthSystem>();
            if (BuffSystem   == null) BuffSystem   = GetComponent<M2922_BuffSystem>();
            _pickup = GetComponent<VRC_Pickup>();

            // Active/désactive le pickup selon le flag
            if (_pickup != null)
                _pickup.pickupable = _isGrabbable;

            _originPosition = transform.position;
            _originRotation = transform.rotation;

            if (_isDamageable) ApplyBuffStaticEffects();

            if (Networking.IsOwner(gameObject))
                RequestSerialization();
        }

        protected override void Update()
        {
            base.Update();

            // Retour auto
            if (_isGrabbable && _pendingReturn && _returnDelay >= 0f
                && Time.time - _dropTime >= _returnDelay)
            {
                _pendingReturn = false;
                ReturnToOrigin();
            }

            // Buffs passifs
            if (!HasBuffs) return;
            float dt = Time.deltaTime;

            if (_isDamageable && HasHealth)
            {
                float healRate   = BuffSystem.GetStatMultiplier(StatType.HealRate);
                if (healRate > 0f)   HealthSystem.Heal(healRate * dt);

                float passiveDmg = BuffSystem.GetStatMultiplier(StatType.PassiveDamage);
                if (passiveDmg > 0f) TakeDamage(passiveDmg * dt, -1, BuffSystem.GetPassiveDamageType());
            }
        }

        // =====================================================================
        // INTERNALS
        // =====================================================================
        private void ApplyBuffStaticEffects()
        {
            if (!HasBuffs || !HasHealth) return;
            float maxHpMult = BuffSystem.GetStatMultiplier(StatType.MaxHealth);
            if (maxHpMult != 1f) HealthSystem.SetMaxHealth(HealthSystem.MaxHealth * maxHpMult);
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (HealthSystem == null) HealthSystem = GetComponent<M2922_HealthSystem>();
            if (BuffSystem   == null) BuffSystem   = GetComponent<M2922_BuffSystem>();

            // Avertissement : _isDamageable sans HealthSystem
            if (_isDamageable && HealthSystem == null)
                UnityEngine.Debug.LogWarning(
                    $"[M2922_Prop] {_entityName}: _isDamageable est coché mais aucun HealthSystem trouvé !", this);

            // Avertissement : _isGrabbable sans VRC_Pickup
            if (_isGrabbable && GetComponent<VRC_Pickup>() == null)
                UnityEngine.Debug.LogWarning(
                    $"[M2922_Prop] {_entityName}: _isGrabbable est coché mais aucun VRC_Pickup trouvé !", this);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;

            string caps = "";
            if (_isGrabbable)  caps += "Grabbable ";
            if (_isDamageable) caps += "Damageable";
            if (caps == "")    caps  = "Static";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 1.5f),
                $"[Prop] {_entityName}\n{caps}"
            );
        }
#endif
    }
}