# 📐 Interfaces et Patterns - M2922 System

## 📋 Vue d'ensemble

Le système M2922 utilise des **interfaces comme documentation** pour définir les contrats API et maintenir une architecture claire. Cependant, **UdonSharp ne supporte pas l'héritage d'interface au runtime**.

### 🎯 Objectif des Interfaces

Les interfaces servent de **documentation vivante** et de **contrat API** pour:
- Définir clairement les propriétés et méthodes requises
- Faciliter la future compatibilité si UdonSharp supporte les interfaces
- Maintenir une architecture cohérente à travers le système
- Servir de référence pour les développeurs ajoutant des fonctionnalités

### ⚠️ Limitation UdonSharp

**UdonSharp ne supporte PAS** :
- `public class Foo : UdonSharpBehaviour, IBar` ❌
- Polymorphisme via interfaces
- Casting vers un type interface

**Solution appliquée** :
- Les classes implémentent **manuellement** les propriétés/méthodes des interfaces
- Les déclarations n'incluent PAS l'interface dans la signature de classe
- Un commentaire indique quel pattern est implémenté
- Tous les membres définis dans l'interface sont présents

---

## 🔷 IDamageable - Entités prenables de dégâts

### Définition (Fichier: `Core/Interfaces/IDamageable.cs`)

```csharp
public interface IDamageable
{
    // Propriétés
    float Health { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }
    bool CanTakeDamage { get; }
    
    // Méthodes
    void TakeDamage(float damage, int attackerId, DamageType damageType);
    void Heal(float amount);
    void Die(int killerId);
}
```

### Enum DamageType

```csharp
public enum DamageType
{
    Generic = 0,
    Bullet = 1,
    Melee = 2,
    Explosion = 3,
    Fire = 4,
    Poison = 5,
    Fall = 6,
    Drowning = 7,
    Environmental = 8
}
```

### Classes Implémentant IDamageable

#### ✅ M2922_HealthController

**Fichier:** `Combat/M2922_HealthController.cs`

**Implémentation:**
```csharp
// NOTE: Implémente les patterns IDamageable et IEntity
public class M2922_HealthController : M2922_Base
{
    // === DAMAGEABLE PROPERTIES (IDamageable pattern) ===
    public float Health => _health;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => _isAlive;
    public bool CanTakeDamage => _canTakeDamage && _isAlive;
    
    // === DAMAGEABLE METHODS ===
    public void TakeDamage(float damage, int attackerId, DamageType damageType) { ... }
    public void Heal(float amount) { ... }
    public void Die(int killerId) { ... }
}
```

**Utilisation:**
- Gère la santé des joueurs, véhicules, structures
- Event-driven (OnDamageTaken, OnHealed, OnDeath)
- Support du Friendly Fire avec multiplicateur configurable

---

## 🔷 IEntity - Identité et métadonnées

### Définition (Fichier: `Core/Interfaces/IEntity.cs`)

```csharp
public interface IEntity
{
    // Propriétés
    string EntityId { get; }
    string EntityName { get; }
    EntityType Type { get; }
    Transform EntityTransform { get; }
    bool IsActive { get; }
}
```

### Enum EntityType

```csharp
public enum EntityType
{
    None = 0,
    Player = 1,
    Vehicle = 2,
    Weapon = 3,
    Projectile = 4,
    Item = 5,
    Zone = 6,
    Spawner = 7,
    Structure = 8,
    NPC = 9
}
```

### Classes Implémentant IEntity

#### ✅ M2922_HealthController

**Implémentation:**
```csharp
// === ENTITY PROPERTIES (IEntity pattern) ===
public string EntityId => _entityId;
public string EntityName => _entityName;
public EntityType Type => _entityType;
public Transform EntityTransform => _entityTransform;
public bool IsActive => _isActive;
```

**Utilisation:**
- Identification unique des entités dans le système événementiel
- Tracking pour les logs, analytics, debugging
- Classification par type (Player, Vehicle, Weapon, etc.)

---

## 🔷 ITeamMember - Appartenance aux équipes

### Définition (Fichier: `Core/Interfaces/ITeamMember.cs`)

```csharp
public interface ITeamMember
{
    // Propriétés
    TeamId Team { get; }
    
    // Méthodes
    void SetTeam(TeamId newTeam);
    bool IsFriendly(TeamId otherTeam);
    bool IsEnemy(TeamId otherTeam);
}
```

### ⚠️ Note: Système N-Team

L'enum `TeamId` (legacy) supporte uniquement 4 équipes fixes:
```csharp
public enum TeamId
{
    None = 0,
    Red = 1,
    Blue = 2,
    Green = 3,
    Yellow = 4
}
```

**Pour 2-16 équipes dynamiques**, utilisez:
```csharp
M2922_TeamManager.GetPlayerTeamIndex(playerId) → int teamIndex (0-15)
M2922_TeamManager.TeamIdToIndex(TeamId) → int
M2922_TeamManager.IndexToTeamId(int) → TeamId (pour teams 0-3 uniquement)
```

### Classes Implémentant ITeamMember

#### ✅ M2922_PlayerController

**Fichier:** `Player/M2922_PlayerController.cs`

**Implémentation:**
```csharp
// NOTE: Implémente le pattern ITeamMember
public class M2922_PlayerController : M2922_Base
{
    // === TEAM MEMBERSHIP (ITeamMember pattern) ===
    [UdonSynced] private int _currentTeam = (int)TeamId.None;
    public TeamId Team => (TeamId)_currentTeam;
    
    // === TEAM METHODS ===
    public void SetTeam(TeamId newTeam) { ... }
    public bool IsFriendly(TeamId otherTeam) { ... }
    public bool IsEnemy(TeamId otherTeam) { ... }
}
```

**Utilisation:**
- Attribution automatique d'équipe (auto-balance)
- Vérification friendly fire
- Spawn par équipe
- Scores par équipe

**Mode FFA (Free-for-all):**
- Si `_teamManager` est null, le système fonctionne en mode FFA
- `Team` reste à `TeamId.None`
- Pas de friendly fire check, pas de team score

---

## 🔷 IWeapon - Système d'armes

### Définition (Fichier: `Core/Interfaces/IWeapon.cs`)

```csharp
public interface IWeapon
{
    // Propriétés
    string WeaponName { get; }
    float Damage { get; }
    float FireRate { get; }
    int AmmoCapacity { get; }
    int CurrentAmmo { get; }
    bool CanFire { get; }
    DamageType WeaponDamageType { get; }
    
    // Méthodes
    void Fire(Vector3 direction);
    void Reload();
    void Equip(int playerId);
    void Unequip();
}
```

### Classes Implémentant IWeapon

**⚠️ À IMPLÉMENTER** - Exemples à venir dans les futures versions du système.

**Concepts:**
- Armes à feu (Rifle, SMG, Sniper, Shotgun)
- Armes de mêlée (Sword, Knife, Hammer)
- Explosifs (Grenade, Rocket Launcher, C4)
- Système d'ammo et rechargement
- Hit detection (Raycast vs Projectile)

---

## 🔷 IInteractable - Objets interactifs

### Définition (Fichier: `Core/Interfaces/IInteractable.cs`)

```csharp
public interface IInteractable
{
    // Propriétés
    string InteractPrompt { get; }
    bool CanInteract { get; }
    float InteractDistance { get; }
    
    // Méthodes
    void OnInteract(int playerId);
    void OnInteractStart(int playerId);
    void OnInteractEnd(int playerId);
}
```

### Classes Implémentant IInteractable

**⚠️ À IMPLÉMENTER** - Exemples à venir.

**Concepts:**
- Portes, boutons, leviers
- Pickup d'items/armes
- Zones de capture
- Véhicules (entry points)
- Vendor/shop stations

---

## 🛠️ Comment Implémenter un Pattern

### 1️⃣ Lire l'Interface

Examinez le fichier interface dans `Core/Interfaces/` pour connaître:
- Toutes les propriétés requises (nom, type, getter/setter)
- Toutes les méthodes requises (signature complète)
- Les enums associés (DamageType, EntityType, etc.)

### 2️⃣ Déclarer la Classe (SANS Interface)

```csharp
// ❌ FAUX (UdonSharp ne supporte pas)
public class MyClass : M2922_Base, IDamageable
{
}

// ✅ CORRECT
public class MyClass : M2922_Base
{
}
```

### 3️⃣ Ajouter un Commentaire de Documentation

```csharp
/// <summary>
/// Ma classe custom qui gère la santé
/// 
/// NOTE: Implémente le pattern IDamageable (voir Docs/INTERFACES.md)
/// UdonSharp ne supporte pas les interfaces au runtime - les propriétés/méthodes sont
/// implémentées manuellement pour maintenir la compatibilité future.
/// </summary>
public class MyClass : M2922_Base
{
}
```

### 4️⃣ Implémenter TOUTES les Propriétés

```csharp
// === DAMAGEABLE PROPERTIES (IDamageable pattern) ===
[SerializeField] private float _maxHealth = 100f;
private float _health;
private bool _isAlive = true;

public float Health => _health;
public float MaxHealth => _maxHealth;
public bool IsAlive => _isAlive;
public bool CanTakeDamage => _isAlive;
```

### 5️⃣ Implémenter TOUTES les Méthodes

```csharp
// === DAMAGEABLE METHODS ===

public void TakeDamage(float damage, int attackerId, DamageType damageType)
{
    if (!CanTakeDamage) return;
    
    _health -= damage;
    if (_health <= 0)
    {
        Die(attackerId);
    }
}

public void Heal(float amount)
{
    if (!_isAlive) return;
    _health = Mathf.Min(_health + amount, _maxHealth);
}

public void Die(int killerId)
{
    _isAlive = false;
    _health = 0;
    // Logic...
}
```

### 6️⃣ Validation

**Checklist de vérification:**
- [ ] Toutes les propriétés de l'interface sont implémentées
- [ ] Toutes les méthodes de l'interface sont implémentées
- [ ] Les signatures correspondent exactement (types, noms, paramètres)
- [ ] Commentaire expliquant le pattern implémenté
- [ ] Sections commentées avec `(IXxx pattern)` pour clarté
- [ ] Pas de `, IXxx` dans la déclaration de classe

---

## 🎯 Bonnes Pratiques

### ✅ DO

✔️ **Toujours implémenter l'interface complète**
- Si une propriété/méthode ne s'applique pas, fournir une implémentation par défaut

✔️ **Utiliser des sections commentées**
```csharp
// === DAMAGEABLE PROPERTIES (IDamageable pattern) ===
// === ENTITY PROPERTIES (IEntity pattern) ===
// === TEAM MEMBERSHIP (ITeamMember pattern) ===
```

✔️ **Garder les interfaces à jour**
- Si vous modifiez une classe, vérifiez si l'interface doit être mise à jour
- Si vous modifiez une interface, mettez à jour TOUTES les classes l'implémentant

✔️ **Documenter les patterns combinés**
```csharp
// NOTE: Implémente les patterns IDamageable et IEntity
```

### ❌ DON'T

✖️ **N'incluez JAMAIS l'interface dans la déclaration**
```csharp
// ❌ Ne compile pas avec UdonSharp
public class Foo : M2922_Base, IDamageable { }
```

✖️ **N'implémentez pas partiellement**
- Soit vous implémentez TOUTE l'interface, soit aucune

✖️ **Ne modifiez pas les signatures**
- Les noms, types, et paramètres doivent correspondre EXACTEMENT

---

## 🔄 Migration Future (Si UdonSharp supporte les interfaces)

Si UdonSharp ajoute le support des interfaces à l'avenir:

### Étape 1: Ajouter l'héritage
```csharp
// Avant:
public class M2922_HealthController : M2922_Base
{
}

// Après:
public class M2922_HealthController : M2922_Base, IDamageable, IEntity
{
}
```

### Étape 2: Tester la compilation
```bash
# Dans Unity, vérifier que tous les scripts compilent
# Vérifier qu'il n'y a pas de conflits de membres
```

### Étape 3: Activer le polymorphisme
```csharp
// Deviendra possible:
IDamageable target = GetComponent<IDamageable>();
if (target != null)
{
    target.TakeDamage(50f, playerId, DamageType.Bullet);
}
```

### Étape 4: Simplifier le code
```csharp
// Au lieu de chercher un composant spécifique:
M2922_HealthController health = hit.GetComponent<M2922_HealthController>();

// On pourra chercher par interface:
IDamageable damageable = hit.GetComponent<IDamageable>();
```

**Note:** Cette migration sera **100% rétrocompatible** car toutes les propriétés/méthodes existent déjà.

---

## 📚 Résumé

| Interface | Implémenté par | Status | Utilisation |
|-----------|---------------|--------|-------------|
| `IDamageable` | `M2922_HealthController` | ✅ Complet | Système de santé/dégâts |
| `IEntity` | `M2922_HealthController` | ✅ Complet | Identification entités |
| `ITeamMember` | `M2922_PlayerController` | ✅ Complet | Système d'équipes (optionnel) |
| `IWeapon` | *(à venir)* | 🔄 À implémenter | Armes/combat |
| `IInteractable` | *(à venir)* | 🔄 À implémenter | Interactions joueur |

### Références Croisées

- **Architecture:** Voir [ARCHITECTURE_3_LEVELS.md](ARCHITECTURE_3_LEVELS.md)
- **Teams:** Voir [GAMEMODE_GUIDE.md](GAMEMODE_GUIDE.md) section "Systèmes d'Équipes"
- **Combat:** Voir [FRIENDLY_FIRE_GUIDE.md](FRIENDLY_FIRE_GUIDE.md)
- **Spawns:** Voir [SPAWN_SYSTEM_GUIDE.md](SPAWN_SYSTEM_GUIDE.md)

---

**Questions ou problèmes?** Ce document évoluera avec le système. Gardez les interfaces et implémentations synchronisées! 🚀
