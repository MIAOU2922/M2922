using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using CoreEventType = M2922.Core.EventType;

namespace M2922.Combat
{
    /// <summary>
    /// Contrôleur de santé pour toute entité pouvant prendre des dégâts
    /// Utilisable sur: Joueurs, Véhicules, Objets destructibles
    /// 
    /// NOTE: Implémente les patterns IDamageable et IEntity (voir Docs/INTERFACES.md)
    /// UdonSharp ne supporte pas les interfaces, mais on maintient l'API compatible
    /// pour future compatibilité.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HealthController : M2922_Base
    {
        // === ENTITY PROPERTIES (IEntity pattern) ===
        public int EntityId => _entityId;
        public string EntityName => _entityName;
        public void SetEntityName(string name) { _entityName = name; }
        public EntityType Type => _entityType;
        public Transform EntityTransform => transform;
        public bool IsActive => IsAlive;
        
        // === DAMAGEABLE PROPERTIES (IDamageable pattern) ===
        [UdonSynced] private float _currentHealth = 100f;
        public float Health => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _currentHealth > 0f;
        public bool CanTakeDamage => IsAlive && !_isInvincible && _canTakeDamage;
        public Collider HeadshotCollider => _headshotCollider;
        
        // === SETTINGS ===
        [Header("=== ENTITY SETTINGS ===")]
        [SerializeField] private string _entityName = "Entity";
        [SerializeField] private EntityType _entityType = EntityType.Player;
        
        [Header("=== HEALTH SETTINGS ===")]
        [SerializeField] private float _maxHealth = 100f;
        [Tooltip("Régénération automatique de santé ?")]
        [SerializeField] private bool _regenerateHealth = false;
        [Tooltip("Vitesse de régénération (HP/seconde)")]
        [SerializeField] private float _regenRate = 5f;
        [Tooltip("Délai avant régénération après dégât (secondes)")]
        [SerializeField] private float _regenDelay = 3f;
        
        [Header("=== DAMAGE SETTINGS ===")]
        [Tooltip("Multiplicateur de dégâts pour headshot")]
        [SerializeField] private float _headshotMultiplier = 2f;
        [Tooltip("Zone de headshot (optionnel)")]
        [SerializeField] private Collider _headshotCollider;
        
        [Header("=== DEATH SETTINGS ===")]
        [Tooltip("Délai avant respawn automatique (0 = désactivé)")]
        [SerializeField] private float _autoRespawnDelay = 5f;
        [Tooltip("Désactiver le GameObject à la mort ?")]
        [SerializeField] private bool _disableOnDeath = true;
        
        [Header("=== VISUAL FEEDBACK ===")]
        [Tooltip("Prefab d'effet visuel pour les dégâts")]
        [SerializeField] private GameObject _damageEffectPrefab;
        [Tooltip("Prefab d'effet visuel pour la mort")]
        [SerializeField] private GameObject _deathEffectPrefab;
        
        // === PRIVATE ===
        private int _entityId = -1;
        private bool _isInvincible = false;
        private bool _canTakeDamage = true;
        private float _lastDamageTime = 0f;
        private float _deathTime = 0f;
        private int _lastAttackerId = -1;
        
        // Stats tracking
        private float _totalDamageTaken = 0f;
        private int _timesDamaged = 0;
        private int _deaths = 0;
        
        protected override void Start()
        {
            base.Start();
            
            // Générer un ID unique basé sur l'instance
            _entityId = gameObject.GetInstanceID();
            _currentHealth = _maxHealth;
            
            this.Log($"HealthController initialized. Entity: {_entityName} (ID: {_entityId})");
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Régénération de santé
            if (_regenerateHealth && IsAlive && _currentHealth < _maxHealth)
            {
                if (Time.time - _lastDamageTime >= _regenDelay)
                {
                    Heal(_regenRate * Time.deltaTime);
                }
            }
            
            // Auto-respawn
            if (!IsAlive && _autoRespawnDelay > 0f)
            {
                if (Time.time - _deathTime >= _autoRespawnDelay)
                {
                    Respawn();
                }
            }
        }
        
        // === INTERFACE METHODS ===
        
        public void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            if (!CanTakeDamage || damage <= 0f)
            {
                this.VerboseLog($"Cannot take damage. CanTakeDamage: {CanTakeDamage}, Damage: {damage}");
                return;
            }
            
            float finalDamage = damage;
            float oldHealth = _currentHealth;
            
            // Appliquer les dégâts
            _currentHealth = Mathf.Max(0f, _currentHealth - finalDamage);
            _lastDamageTime = Time.time;
            _lastAttackerId = attackerId;
            
            // Stats
            _totalDamageTaken += finalDamage;
            _timesDamaged++;
            
            this.Log($"Took {finalDamage} {damageType} damage from {attackerId}. Health: {oldHealth:F1} -> {_currentHealth:F1}");
            
            // Sync réseau
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
            
            // Effets visuels
            if (_damageEffectPrefab != null)
            {
                Instantiate(_damageEffectPrefab, transform.position, Quaternion.identity);
            }
            
            // Publier événement de dégât
            if (Manager != null && Manager.EventBus != null)
            {
                var slot = Manager.EventBus.RentSlot();
                if (slot != null)
                {
                    slot.VictimId = _entityId;
                    slot.AttackerId = attackerId;
                    slot.KillerId = attackerId;
                    slot.Damage = finalDamage;
                    slot.DamageType = (int)damageType;
                    slot.RemainingHealth = _currentHealth;
                    slot.IsHeadshot = false;
                    Manager.EventBus.PublishNetwork(CoreEventType.OnPlayerDamaged, slot);
                }
            }
            
            // Check death
            if (_currentHealth <= 0f && oldHealth > 0f)
            {
                Die(attackerId);
            }
        }
        
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            
            float oldHealth = _currentHealth;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            
            if (oldHealth != _currentHealth)
            {
                this.VerboseLog($"Healed {amount:F1}. Health: {oldHealth:F1} -> {_currentHealth:F1}");
                
                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                }
                
                // Publier événement de soin
                if (Manager != null && Manager.EventBus != null)
                {
                    var slot = Manager.EventBus.RentSlot();
                    if (slot != null) slot.VictimId = _entityId;
                    Manager.EventBus.Publish(CoreEventType.OnPlayerHealed, slot);
                }
            }
        }
        
        public void Die(int killerId)
        {
            if (!IsAlive)
            {
                this.VerboseWarning("Already dead");
                return;
            }
            
            _currentHealth = 0f;
            _deathTime = Time.time;
            _deaths++;
            
            this.Log($"Died. Killed by: {killerId}");
            
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
            
            // Effets visuels
            if (_deathEffectPrefab != null)
            {
                Instantiate(_deathEffectPrefab, transform.position, Quaternion.identity);
            }
            
            // Désactiver si configuré
            if (_disableOnDeath)
            {
                gameObject.SetActive(false);
            }
            
            // Publier événement de mort
            if (Manager != null && Manager.EventBus != null)
            {
                var slot = Manager.EventBus.RentSlot();
                if (slot != null)
                {
                    slot.VictimId = _entityId;
                    slot.KillerId = killerId;
                    slot.IsSuicide = (killerId == _entityId);
                    slot.Position = transform.position;
                    Manager.EventBus.PublishNetwork(CoreEventType.OnPlayerDied, slot);
                    Manager.EventBus.PublishNetwork(CoreEventType.OnPlayerKilled, slot);
                }
            }
        }
        
        // === PUBLIC METHODS ===
        
        /// <summary>
        /// Respawn l'entité avec santé complète
        /// </summary>
        public void Respawn()
        {
            _currentHealth = _maxHealth;
            _isInvincible = false;
            _canTakeDamage = true;
            _lastDamageTime = 0f;
            
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
            
            if (_disableOnDeath)
            {
                gameObject.SetActive(true);
            }
            
            this.Log($"Respawned. Health: {_currentHealth}/{_maxHealth}");
            
            // Publier événement
            if (Manager != null && Manager.EventBus != null)
            {
                var slot = Manager.EventBus.RentSlot();
                if (slot != null) slot.VictimId = _entityId;
                Manager.EventBus.Publish(CoreEventType.OnPlayerRespawned, slot);
            }
        }
        
        /// <summary>
        /// Activer/désactiver l'invincibilité
        /// </summary>
        public void SetInvincible(bool invincible)
        {
            _isInvincible = invincible;
            this.VerboseLog($"Invincibility: {invincible}");
        }
        
        /// <summary>
        /// Activer/désactiver la capacité de prendre des dégâts (différent de l'invincibilité)
        /// </summary>
        public void SetCanTakeDamage(bool canTakeDamage)
        {
            _canTakeDamage = canTakeDamage;
            this.VerboseLog($"CanTakeDamage: {canTakeDamage}");
        }
        
        /// <summary>
        /// Restaurer la santé complète
        /// </summary>
        public void FullHeal()
        {
            Heal(_maxHealth);
        }
        
        /// <summary>
        /// Tuer instantanément (ignorant l'invincibilité)
        /// </summary>
        public void Kill()
        {
            bool wasInvincible = _isInvincible;
            _isInvincible = false;
            TakeDamage(_currentHealth + 1000f, -1, DamageType.Generic);
            _isInvincible = wasInvincible;
        }
        
        // === GETTERS ===
        
        public float GetHealthPercentage()
        {
            return _maxHealth > 0f ? (_currentHealth / _maxHealth) : 0f;
        }
        
        public bool IsLowHealth(float threshold = 0.25f)
        {
            return GetHealthPercentage() <= threshold;
        }
        
        public float GetTotalDamageTaken()
        {
            return _totalDamageTaken;
        }
        
        public int GetTimesDamaged()
        {
            return _timesDamaged;
        }
        
        public int GetDeaths()
        {
            return _deaths;
        }
        
        public int GetLastAttacker()
        {
            return _lastAttackerId;
        }
        
        // === DEBUG ===
        
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!DEBUG) return;
            
            // Afficher la santé au-dessus de l'entité
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{_entityName}\nHP: {_currentHealth:F0}/{_maxHealth:F0}\n({GetHealthPercentage() * 100f:F0}%)"
            );
            
            // Couleur selon la santé
            if (IsAlive)
            {
                if (IsLowHealth())
                    Gizmos.color = Color.red;
                else if (GetHealthPercentage() < 0.5f)
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.black;
            }
            
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
#endif
    }
}
