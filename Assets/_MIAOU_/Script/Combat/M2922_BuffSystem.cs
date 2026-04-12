using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    /// <summary>
    /// Système de buffs et debuffs temporaires — optionnel.
    /// Expose des multiplicateurs de stats consultés par les autres systèmes.
    /// 
    /// RÈGLE : BuffSystem ne modifie PAS directement les HP, la vitesse ou les dégâts.
    ///         Il expose des multiplicateurs que PlayerController et les autres scripts lisent.
    /// 
    /// UTILISATION :
    ///   Ajouter ce composant sur le même GameObject que M2922_PlayerController.
    ///   Si absent, tous les multiplicateurs retournent 1.0 (neutre).
    /// 
    /// BUFFS DISPONIBLES (voir BuffType enum dans IBuffable.cs) :
    ///   SpeedBoost, SlowDown, DamageBoost, DamageReduction, HealthRegen,
    ///   ShieldRegen, Invincibility, Invisibility, Poison, Burn, Stun, ReloadSpeed
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_BuffSystem : UdonSharpBehaviour
    {
        // Nombre de types de buffs (doit correspondre au nombre de valeurs dans BuffType)
        private const int BUFF_COUNT = 12;
        
        // Parallel arrays : index = (int)BuffType
        private bool[]  _isActive;       // Ce buff est-il actuellement actif ?
        private float[] _magnitude;      // Force du modificateur
        private float[] _endTime;        // Time.time auquel le buff expire (0 = permanent)
        
        [Header("=== REGEN SETTINGS ===")]
        [Tooltip("HP régénérés par seconde quand HealthRegen actif")]
        [SerializeField] private float _healthRegenRate = 5f;
        
        [Tooltip("Armure régénérée par seconde quand ShieldRegen actif")]
        [SerializeField] private float _shieldRegenRate = 10f;
        
        // Références aux systèmes qui subissent la regen
        // Assignées automatiquement au Start() ou manuellement depuis l'inspecteur
        [SerializeField] private M2922_HealthController _healthController;
        [SerializeField] private M2922_ArmorSystem _armorSystem;
        
        // Accesseurs publics pour compatibilité avec PlayerController
        public M2922_HealthController HealthControllerRef
        {
            get => _healthController;
            set => _healthController = value;
        }
        public M2922_ArmorSystem ArmorSystemRef
        {
            get => _armorSystem;
            set => _armorSystem = value;
        }
        
        // === LIFECYCLE ===
        
        private void Start()
        {
            _isActive  = new bool[BUFF_COUNT];
            _magnitude = new float[BUFF_COUNT];
            _endTime   = new float[BUFF_COUNT];
            
            // Auto-detect sur le même GameObject ou les parents
            if (_healthController == null)
                _healthController = GetComponentInParent<M2922_HealthController>();
            if (_armorSystem == null)
                _armorSystem = GetComponentInParent<M2922_ArmorSystem>();
        }
        
        public override void PostLateUpdate()
        {
            if (_isActive == null) return;
            
            float now = Time.time;
            
            for (int i = 0; i < BUFF_COUNT; i++)
            {
                if (!_isActive[i]) continue;
                
                // Expiration (endTime == 0 → permanent)
                if (_endTime[i] > 0f && now >= _endTime[i])
                {
                    _isActive[i] = false;
                    continue;
                }
                
                // Effets per-frame : regen
                float dt = Time.deltaTime;
                
                if (i == (int)BuffType.HealthRegen && HealthControllerRef != null)
                {
                    HealthControllerRef.Heal(_healthRegenRate * _magnitude[i] * dt);
                }
                else if (i == (int)BuffType.ShieldRegen && ArmorSystemRef != null)
                {
                    ArmorSystemRef.RepairArmor(_shieldRegenRate * _magnitude[i] * dt);
                }
                else if (i == (int)BuffType.Poison && HealthControllerRef != null)
                {
                    // Poison : magnitude = dégâts/s
                    HealthControllerRef.TakeDamage(_magnitude[i] * dt, -1, DamageType.Generic);
                }
                else if (i == (int)BuffType.Burn && HealthControllerRef != null)
                {
                    HealthControllerRef.TakeDamage(_magnitude[i] * dt, -1, DamageType.Fire);
                }
            }
        }
        
        // === INTERFACE : IBuffable ===
        
        /// <summary>Au moins un buff est-il actif ?</summary>
        public bool HasActiveBuff
        {
            get
            {
                if (_isActive == null) return false;
                for (int i = 0; i < BUFF_COUNT; i++)
                    if (_isActive[i]) return true;
                return false;
            }
        }
        
        /// <summary>
        /// Applique un buff/debuff. Si déjà actif, renouvelle la durée.
        /// </summary>
        /// <param name="buffType">Type de buff</param>
        /// <param name="magnitude">Force (ex: 1.5 = +50%, 0.5 = -50%)</param>
        /// <param name="duration">Durée en secondes. 0 = permanent.</param>
        public void ApplyBuff(BuffType buffType, float magnitude, float duration)
        {
            if (_isActive == null) return;
            
            int idx = (int)buffType;
            if (idx < 0 || idx >= BUFF_COUNT) return;
            
            _isActive[idx]  = true;
            _magnitude[idx] = magnitude;
            _endTime[idx]   = duration > 0f ? Time.time + duration : 0f;
        }
        
        /// <summary>Retire immédiatement un buff.</summary>
        public void RemoveBuff(BuffType buffType)
        {
            if (_isActive == null) return;
            int idx = (int)buffType;
            if (idx >= 0 && idx < BUFF_COUNT)
                _isActive[idx] = false;
        }
        
        /// <summary>Le buff est-il actuellement actif ?</summary>
        public bool HasBuff(BuffType buffType)
        {
            if (_isActive == null) return false;
            int idx = (int)buffType;
            return idx >= 0 && idx < BUFF_COUNT && _isActive[idx];
        }
        
        /// <summary>
        /// Retourne le multiplicateur combiné pour une stat.
        /// Plusieurs buffs peuvent affecter la même stat — ils sont multipliés.
        /// Retourne 1.0 si aucun buff actif sur cette stat.
        /// </summary>
        public float GetStatMultiplier(StatType statType)
        {
            if (_isActive == null) return 1f;
            
            float result = 1f;
            
            switch (statType)
            {
                case StatType.Speed:
                    if (_isActive[(int)BuffType.SpeedBoost])
                        result *= _magnitude[(int)BuffType.SpeedBoost];
                    if (_isActive[(int)BuffType.SlowDown])
                        result *= _magnitude[(int)BuffType.SlowDown];
                    if (_isActive[(int)BuffType.Stun])
                        result = 0f; // Stun bloque le mouvement
                    break;
                    
                case StatType.DamageOut:
                    if (_isActive[(int)BuffType.DamageBoost])
                        result *= _magnitude[(int)BuffType.DamageBoost];
                    if (_isActive[(int)BuffType.Stun])
                        result = 0f; // Stun empêche d'attaquer
                    break;
                    
                case StatType.DamageIn:
                    if (_isActive[(int)BuffType.DamageReduction])
                        result *= _magnitude[(int)BuffType.DamageReduction];
                    if (_isActive[(int)BuffType.Invincibility])
                        result = 0f; // Invincible = aucun dégât
                    break;
                    
                case StatType.ReloadSpeed:
                    if (_isActive[(int)BuffType.ReloadSpeed])
                        result *= _magnitude[(int)BuffType.ReloadSpeed];
                    if (_isActive[(int)BuffType.Stun])
                        result = 0f;
                    break;
                    
                case StatType.MaxHealth:
                    // MaxHealth n'est pas modifié dynamiquement par défaut
                    break;
                    
                case StatType.JumpHeight:
                    if (_isActive[(int)BuffType.SpeedBoost])
                        result *= _magnitude[(int)BuffType.SpeedBoost];
                    if (_isActive[(int)BuffType.Stun])
                        result = 0f;
                    break;
            }
            
            return result;
        }
        
        /// <summary>Retire tous les buffs actifs (respawn, fin de partie).</summary>
        public void ClearAllBuffs()
        {
            if (_isActive == null) return;
            for (int i = 0; i < BUFF_COUNT; i++)
                _isActive[i] = false;
        }
    }
}
