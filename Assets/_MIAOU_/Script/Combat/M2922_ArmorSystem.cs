using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    /// <summary>
    /// Système d'armure optionnel — absorbe une partie des dégâts AVANT HealthController.
    ///
    /// PIPELINE :
    ///   Source → [FF check] → [Buff DamageIn] → ArmorSystem.AbsorbDamage() → HealthController
    ///
    /// L'armure est un pool plat unique. Le multiplicateur de zone (tête, corps…)
    /// est géré par M2922_HitZone via _damageMultiplier — pas ici.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ArmorSystem : M2922_Base
    {
        [Header("=== ARMURE ===")]
        [Tooltip("Points d'armure maximum.")]
        [SerializeField] private float _maxArmor = 100f;
        
        [Tooltip("Part des dégâts absorbée par l'armure (0=aucune, 1=totale).")]
        [Range(0f, 1f)]
        [SerializeField] private float _absorptionRate = 0.75f;

        [Header("=== RÉSISTANCES ===")]
        [Tooltip("Résistance aux balles (1=normale, 0=inutile).")]
        [Range(0f, 1f)]
        [SerializeField] private float _bulletResistance = 1f;

        [Tooltip("Résistance aux explosions.")]
        [Range(0f, 1f)]
        [SerializeField] private float _explosionResistance = 0.5f;

        [Tooltip("Résistance au corps à corps.")]
        [Range(0f, 1f)]
        [SerializeField] private float _meleeResistance = 0.1f;

        [Tooltip("Résistance au feu / brûlures.")]
        [Range(0f, 1f)]
        [SerializeField] private float _fireResistance = 0.25f;

        private float _armor;

        // === LIFECYCLE ===

        private void Start() => RepairFull();

        // === ÉTAT ===

        public float ArmorPoints    => _armor;
        public float MaxArmorPoints => _maxArmor;
        public bool  HasArmor       => _armor > 0f;

        // === PIPELINE ===

        /// <summary>
        /// Absorbe une partie des dégâts. Réduit le pool d'armure en conséquence.
        /// Retourne les dégâts résiduels à transmettre au HealthController.
        /// </summary>
        public float AbsorbDamage(float incomingDamage, DamageType damageType)
        {
            if (incomingDamage <= 0f || _armor <= 0f) return incomingDamage;

            float resistance = GetResistanceForType(damageType);
            float absorbed   = incomingDamage * _absorptionRate * resistance;
            absorbed = Mathf.Min(absorbed, _armor);

            // L'armure se détériore proportionnellement à sa résistance
            _armor = Mathf.Max(0f, _armor - absorbed / Mathf.Max(0.01f, resistance));

            return Mathf.Max(0f, incomingDamage - absorbed);
        }

        // === RÉPARATION ===

        public void RepairArmor(float amount)
        {
            if (amount > 0f) _armor = Mathf.Min(_maxArmor, _armor + amount);
        }

        public void RepairFull() => _armor = _maxArmor;

        // === HELPERS ===

        private float GetResistanceForType(DamageType type)
        {
            switch (type)
            {
                case DamageType.Bullet:    return _bulletResistance;
                case DamageType.Explosion: return _explosionResistance;
                case DamageType.Melee:     return _meleeResistance;
                case DamageType.Fire:      return _fireResistance;
                default:                   return 1f;
            }
        }
    }
}
