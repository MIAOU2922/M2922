# 🏗️ Guide d'Architecture Modulaire - M2922 PvP System

> **Date:** 10 avril 2026  
> **Architecture:** Modulaire avec Core central et EventBus

---

## 📋 Vue d'Ensemble

Votre projet est structuré pour être **100% modulaire** :
- **Core** = Tout le monde en dépend, mais ne dépend de personne
- **Modules** (Combat, Player, Vehicle, Teams, etc.) = Dépendent de Core uniquement
- **Communication** = Via interfaces et EventBus (pas de dépendances directes)

---

## 📂 Structure Actuelle

```
Assets/_MIAOU_/Script/
├── Core/                           ← LE CŒUR DU SYSTÈME
│   ├── M2922_Base.cs              ✅ Classe de base pour tous vos scripts
│   ├── M2922_Manager.cs           ✅ Gestionnaire principal du jeu
│   ├── M2922_Debug.cs             ✅ Système de debug centralisé
│   ├── Interfaces/
│   │   ├── IEntity.cs             ✅ Interface pour toute entité
│   │   ├── IDamageable.cs         ✅ Interface pour tout ce qui prend des dégâts
│   │   ├── ITeamMember.cs         ✅ Interface pour appartenance à une équipe
│   │   ├── IInteractable.cs       ✅ Interface pour objets interactifs
│   │   └── IWeapon.cs             ✅ Interface pour armes
│   └── Events/
│       ├── M2922_EventBus.cs      ✅ Système d'événements UdonSharp
│       └── EventDataStructs.cs    ✅ Structures de données pour événements
│
├── Combat/                         ← MODULE COMBAT (à créer)
│   ├── Health/
│   ├── Damage/
│   └── Weapons/
│
├── Player/                         ← MODULE JOUEUR (à créer)
├── Vehicle/                        ← MODULE VÉHICULES (à créer)
├── Teams/                          ← MODULE ÉQUIPES (à créer)
└── [autres modules...]
```

---

## 🎯 Règles d'Or

### ✅ À FAIRE
1. **Hériter de `M2922_Base`** pour tous vos scripts UdonSharp
2. **Utiliser les interfaces de Core** pour définir les contrats
3. **Passer par l'EventBus** pour la communication inter-modules
4. **Un namespace par module** (`M2922.Core`, `M2922.Combat`, `M2922.Player`, etc.)

### ❌ À ÉVITER
1. **PAS d'import direct** entre modules au même niveau (ex: Combat n'importe pas Player)
2. **PAS de GameObject.Find()** sauf pour trouver le Manager
3. **PAS de références directes** entre modules différents

---

## 💡 Comment Créer un Nouveau Module

### Exemple : Module Combat - HealthSystem

**Étape 1 : Créer le script dans le bon dossier**
```
Script/Combat/Health/M2922_HealthController.cs
```

**Étape 2 : Utiliser le bon namespace et implémenter les interfaces**
```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;  // ← Import du Core uniquement !

namespace M2922.Combat  // ← Namespace pour le module
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HealthController : M2922_Base, IDamageable, IEntity
    {
        // === INTERFACE IEntity ===
        public int EntityId => _entityId;
        public string EntityName => gameObject.name;
        public EntityType Type => EntityType.Player;
        public Transform EntityTransform => transform;
        public bool IsActive => IsAlive;
        
        // === INTERFACE IDamageable ===
        [UdonSynced] private float _currentHealth = 100f;
        public float Health => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _currentHealth > 0f;
        public bool CanTakeDamage => IsAlive && !_isInvincible;
        
        // === SETTINGS ===
        [Header("=== HEALTH SETTINGS ===")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private bool _regenerateHealth = false;
        [SerializeField] private float _regenRate = 5f; // HP/seconde
        [SerializeField] private float _regenDelay = 3f; // Délai après dégât
        
        // === PRIVATE ===
        private int _entityId = -1;
        private bool _isInvincible = false;
        private float _lastDamageTime = 0f;
        
        protected override void Start()
        {
            base.Start();
            
            // Générer un ID unique
            _entityId = gameObject.GetInstanceID();
            _currentHealth = _maxHealth;
            
            // S'abonner aux événements si besoin
            if (Manager != null && Manager.EventBus != null)
            {
                // On pourrait s'abonner à des événements ici si nécessaire
            }
            
            this.Log($"HealthController initialized. ID: {_entityId}");
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
        }
        
        // === INTERFACE METHODS ===
        
        public void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            if (!CanTakeDamage || damage <= 0f) return;
            
            float oldHealth = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            _lastDamageTime = Time.time;
            
            this.Log($"Took {damage} damage from {attackerId}. Health: {oldHealth} -> {_currentHealth}");
            
            // Sync réseau
            RequestSerialization();
            
            // Publier événement de dégât
            if (Manager != null && Manager.EventBus != null)
            {
                var damageData = new DamageEventData(_entityId, attackerId, damage, damageType);
                damageData.RemainingHealth = _currentHealth;
                Manager.EventBus.PublishNetwork(EventType.OnPlayerDamaged, damageData);
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
                this.VerboseLog($"Healed {amount}. Health: {oldHealth} -> {_currentHealth}");
                
                RequestSerialization();
                
                // Publier événement de soin
                if (Manager != null && Manager.EventBus != null)
                {
                    Manager.EventBus.Publish(EventType.OnPlayerHealed, this);
                }
            }
        }
        
        public void Die(int killerId)
        {
            if (!IsAlive) return;
            
            _currentHealth = 0f;
            
            this.Log($"Died. Killed by: {killerId}");
            
            RequestSerialization();
            
            // Publier événement de mort
            if (Manager != null && Manager.EventBus != null)
            {
                var killData = new KillEventData(_entityId, killerId);
                Manager.EventBus.PublishNetwork(EventType.OnPlayerDied, killData);
            }
            
            // Désactiver le joueur (ou autre logique)
            // TODO: Trigger death animation, ragdoll, etc.
        }
        
        // === PUBLIC METHODS ===
        
        public void Respawn()
        {
            _currentHealth = _maxHealth;
            _isInvincible = false;
            
            RequestSerialization();
            
            this.Log("Respawned");
            
            // Publier événement
            if (Manager != null && Manager.EventBus != null)
            {
                Manager.EventBus.Publish(EventType.OnPlayerRespawned, this);
            }
        }
        
        public void SetInvincible(bool invincible)
        {
            _isInvincible = invincible;
            this.VerboseLog($"Invincibility: {invincible}");
        }
    }
}
```

---

## 🔔 Comment Utiliser l'EventBus

### 1. S'abonner à un événement

```csharp
protected override void Start()
{
    base.Start();
    
    // S'abonner aux événements
    if (Manager != null && Manager.EventBus != null)
    {
        Manager.EventBus.Subscribe(EventType.OnPlayerKilled, this);
        Manager.EventBus.Subscribe(EventType.OnPlayerDamaged, this);
    }
}
```

### 2. Créer les méthodes de callback

**⚠️ IMPORTANT:** Le nom de la méthode DOIT correspondre exactement au nom de l'enum !

```csharp
// Cette méthode sera appelée automatiquement quand l'événement OnPlayerKilled est publié
public void OnPlayerKilled()
{
    // Récupérer les données de l'événement
    var killData = (KillEventData)Manager.EventBus.GetLastEventData(EventType.OnPlayerKilled);
    
    if (killData == null) return;
    
    this.Log($"Player {killData.VictimId} killed by {killData.KillerId}");
    
    // Votre logique ici
    // Ex: Afficher un message, mettre à jour le scoreboard, etc.
}

public void OnPlayerDamaged()
{
    var damageData = (DamageEventData)Manager.EventBus.GetLastEventData(EventType.OnPlayerDamaged);
    
    if (damageData == null) return;
    
    // Afficher un indicateur de dégât, jouer un son, etc.
}
```

### 3. Publier un événement

```csharp
// Événement local (seulement ce client)
Manager.EventBus.Publish(EventType.OnWeaponReloaded, weaponData);

// Événement réseau (tous les clients)
Manager.EventBus.PublishNetwork(EventType.OnPlayerKilled, killData);
```

---

## 🔗 Comment Les Modules Communiquent

### ❌ MAUVAIS : Dépendance directe
```csharp
// NE FAITES PAS ÇA !
using M2922.Player;  // ← Import d'un autre module !

namespace M2922.Weapon
{
    public class BadWeapon : M2922_Base
    {
        public PlayerController player;  // ← Référence directe !
        
        public void Fire()
        {
            player.TakeDamage(10f);  // ← Couplage fort !
        }
    }
}
```

### ✅ BON : Via interface
```csharp
// FAITES CECI À LA PLACE !
using M2922.Core;  // ← Seulement Core !

namespace M2922.Weapon
{
    public class GoodWeapon : M2922_Base
    {
        public void Fire()
        {
            // Raycast pour trouver une cible
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, 100f))
            {
                // Chercher un IDamageable (peu importe si c'est Player, Vehicle, etc.)
                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                
                if (damageable != null && damageable.CanTakeDamage)
                {
                    // Appliquer les dégâts via l'interface
                    damageable.TakeDamage(25f, _ownerId, DamageType.Bullet);
                }
            }
        }
    }
}
```

### ✅ BON : Via EventBus
```csharp
// Module Weapon publie un événement
var weaponData = new WeaponEventData(playerId, WeaponType.Rifle, "AK-47");
Manager.EventBus.PublishNetwork(EventType.OnWeaponFired, weaponData);

// Module UI écoute et affiche l'info
public void OnWeaponFired()
{
    var data = (WeaponEventData)Manager.EventBus.GetLastEventData(EventType.OnWeaponFired);
    UpdateAmmoDisplay(data.CurrentAmmo, data.ReserveAmmo);
}
```

---

## 📝 Conventions de Nommage

### Classes
- **Préfixe:** `M2922_`
- **Format:** `M2922_NomDuComposant`
- **Exemples:** `M2922_HealthController`, `M2922_WeaponBase`, `M2922_TeamManager`

### Interfaces
- **Préfixe:** `I`
- **Format:** `INom`
- **Exemples:** `IEntity`, `IDamageable`, `IWeapon`

### Namespaces
- **Format:** `M2922.ModuleName`
- **Exemples:** `M2922.Core`, `M2922.Combat`, `M2922.Player`, `M2922.Vehicle`

### EventTypes
- **Format:** `OnActionPastTense`
- **Exemples:** `OnPlayerKilled`, `OnWeaponFired`, `OnTeamScoreChanged`

---

## 🌳 Hiérarchie de Dépendances

```
Core (M2922.Core)
├─ Interfaces (IEntity, IDamageable, etc.)
├─ EventBus
├─ Base classes (M2922_Base, M2922_Manager)
└─ Data structures (EventData, Enums)
    ↑
    │ (dépend de)
    │
    ├─ Combat (M2922.Combat)
    ├─ Player (M2922.Player)
    ├─ Vehicle (M2922.Vehicle)
    ├─ Teams (M2922.Teams)
    ├─ Weapons (M2922.Weapons)
    └─ [autres modules...]
```

**Règle:** Les flèches vont TOUJOURS vers le haut (modules → Core), JAMAIS horizontalement (module → module).

---

## 🚀 Prochaines Étapes

### Pour continuer le développement :

1. **Créer le TeamManager** dans `Script/Teams/`
   - Gérer les équipes (Red, Blue, Green, Yellow)
   - Implémenter `ITeamMember`
   - Publier `EventType.OnTeamChanged`

2. **Créer le système d'armes** dans `Script/Combat/Weapons/`
   - Classe de base `M2922_WeaponBase`
   - Implémenter `IWeapon`
   - Sous-classes pour chaque type d'arme

3. **Créer le système de spawn** dans `Script/Spawning/`
   - Points de spawn par équipe
   - Respawn automatique
   - Publier `EventType.OnPlayerSpawned`

4. **Créer les UI** dans `Script/UI/` (ou `UI/Scripts/`)
   - HUD (santé, munitions)
   - Scoreboard
   - Kill feed

---

## 🔍 Exemple Complet : Kill System

**1. Weapon tire et touche → Module Combat**
```csharp
// Dans M2922_Weapon
damageable.TakeDamage(25f, shooterId, DamageType.Bullet);
```

**2. HealthController reçoit dégâts → Module Combat**
```csharp
// Dans M2922_HealthController
public void TakeDamage(float damage, int attackerId, DamageType damageType)
{
    _health -= damage;
    
    if (_health <= 0)
    {
        Die(attackerId);  // ← Publie OnPlayerKilled
    }
}
```

**3. EventBus notifie tous les listeners → Core**
```csharp
// Dans M2922_EventBus
EventBus.PublishNetwork(EventType.OnPlayerKilled, killData);
```

**4. Modules réagissent indépendamment**
```csharp
// Dans M2922_ScoreManager (Module Scoring)
public void OnPlayerKilled()
{
    var killData = (KillEventData)EventBus.GetLastEventData(EventType.OnPlayerKilled);
    AddScoreToPlayer(killData.KillerId, 100);
}

// Dans M2922_KillFeedUI (Module UI)
public void OnPlayerKilled()
{
    var killData = (KillEventData)EventBus.GetLastEventData(EventType.OnPlayerKilled);
    DisplayKillMessage(killData.KillerId, killData.VictimId, killData.WeaponUsed);
}

// Dans M2922_SpawnManager (Module Spawning)
public void OnPlayerKilled()
{
    var killData = (KillEventData)EventBus.GetLastEventData(EventType.OnPlayerKilled);
    ScheduleRespawn(killData.VictimId, 5f);  // Respawn dans 5 secondes
}
```

**Résultat:** 3 modules différents réagissent au même événement, sans se connaître !

---

## ✨ Avantages de Cette Architecture

✅ **Modulaire** : Ajoutez/retirez des modules sans casser le code  
✅ **Testable** : Testez chaque module indépendamment  
✅ **Maintenable** : Code organisé et facile à retrouver  
✅ **Scalable** : Ajoutez facilement de nouvelles features  
✅ **Découplé** : Pas de spaghetti code !  

---

**Dernière mise à jour:** 10 avril 2026  
**Prochaine étape:** Créer HealthController et TeamManager
