# M2922 PvP System - Architecture

## 📦 Structure Modulaire

Le système M2922 est conçu avec une architecture **modulaire à niveaux** où vous choisissez exactement ce dont vous avez besoin.

```
M2922/
├── Core/           [REQUIS] Système de base (Manager + EventBus)
├── Combat/         [RECOMMANDÉ] Santé/Dégâts
├── Teams/          [OPTIONNEL] Pour modes team-based uniquement
├── Player/         [EXEMPLE] Contrôleur de joueur
├── GameModes/      [OPTIONNEL] Vos règles de jeu (rounds, scores, victoire)
├── Spawning/       [OPTIONNEL] Gestion des spawns
├── Scoring/        [OPTIONNEL] Système de score
└── Vehicle/        [OPTIONNEL] Support véhicules
```

---

## 🎯 Niveaux d'Utilisation

### NIVEAU 1 - Core Minimal (PvP Libre/Sandbox)

**Modules activés:**
- ✅ Core (Manager + EventBus)
- ✅ Combat (HealthController)
- ❌ Teams
- ❌ GameModes

**Résultat:**
- Combat fonctionnel
- Événements de kill/death
- **AUCUNE règle de jeu**
- Pas de rounds, pas de scores, pas de conditions de victoire
- Idéal pour: Sandbox PvP, zone de combat libre, tests

**Usage:**
```csharp
// Juste tirer et tuer, pas de logique de jeu
healthController.TakeDamage(50f, attackerId, DamageType.Bullet);
// → Événement OnPlayerKilled publié, c'est tout!
```

---

### NIVEAU 2 - Avec Teams (Combat en équipes, pas de règles)

**Modules activés:**
- ✅ Core
- ✅ Combat
- ✅ Teams (TeamManager)
- ❌ GameModes

**Résultat:**
- Combat en équipes
- Friendly fire configurable
- Team colors, team spawns
- **Toujours AUCUNE règle de jeu**
- Pas de rounds, pas de système de score automatique

**Usage:**
```csharp
// Combat avec équipes mais pas de règles
if (teamManager.CanDamage(attackerId, victimId))
{
    healthController.TakeDamage(damage);
}
// → Friendly fire géré, mais pas de score/victoire
```

---

### NIVEAU 3 - Avec GameMode (Règles de jeu complètes)

**Modules activés:**
- ✅ Core
- ✅ Combat
- ✅ ou ❌ Teams (selon le mode)
- ✅ **GameMode script** (VOS règles!)

**Résultat:**
- Règles de jeu spécifiques implémentées
- Gestion des rounds, scores, conditions de victoire
- Interface utilisateur pour le score
- Timer de round
- Logique de fin de partie

**Exemples:**

#### Free-for-All GameMode
```csharp
public class M2922_FreeForAll : M2922_Base
{
    public int KillsToWin = 20;
    public float MatchDuration = 600f;
    
    // Votre logique de rounds, scores, victoire
}
```

#### Team Deathmatch GameMode
```csharp
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_TeamDeathmatch : M2922_Base
{
    public int TeamKillsToWin = 50;
    public int RoundsToWin = 2;
    
    // Votre logique de team scoring, rounds, victoire
}
```

**📖 Guide complet**: [GameModes/GAMEMODE_GUIDE.md](GameModes/GAMEMODE_GUIDE.md)

---

## 🔧 Core System (REQUIS)

### M2922_Manager
**Responsabilités CORE (MINIMAL):**
- Gestion de l'état du jeu (Waiting, Starting, InProgress, Ending) via `SetGameState()`
- Suivi des joueurs actifs (`PlayerCount`, `OnPlayerJoined`, `OnPlayerLeft`)
- Variables d'état publiques pour GameModes (`CurrentState`, `CurrentRound`, `RoundTimeRemaining`)
- Point central de référence pour tous les modules
- EventBus pour communication inter-modules

**N'INCLUT PAS:**
- ❌ Règles de jeu (rounds, scores, victoire)
- ❌ Logique de GameMode
- ❌ Conditions de fin de partie
- ❌ Timers ou compteurs automatiques

**Pour ajouter des règles de jeu:**
- Créez votre propre script GameMode dans `GameModes/`
- Utilisez les variables d'état publiques du Manager
- Implémentez vos propres règles, timers, et conditions de victoire

### M2922_EventBus
**Responsabilités:**
- Système d'événements décentralisé (Subscribe/Publish)
- Communication entre modules sans dépendances directes
- Support réseau pour événements globaux

### M2922_Base
**Responsabilités:**
- Classe de base pour tous les scripts M2922
- Gestion automatique du debug
- Helper methods (Log, Warning, Error)
- Référence automatique au Manager

---

## ⚔️ Teams Module (OPTIONNEL)

### M2922_TeamManager

**Quand l'utiliser:**
- ✅ Modes team-based: TDM, CTF, Domination, etc.
- ✅ Vous avez besoin de 2+ équipes fixes
- ✅ Scores par équipe
- ✅ Spawns par équipe
- ✅ Gestion du friendly fire

**Quand NE PAS l'utiliser:**
- ❌ Mode FFA (Free-for-all/Chacun pour soi)
- ❌ Battle Royale (solo)
- ❌ Modes sans équipes fixes

**Fonctionnalités:**
- Support de 2 à 16 équipes dynamiques (TeamConfig[])
- Auto-balance des équipes  
- Gestion des scores par équipe
- Couleurs et spawns configurables
- **Friendly Fire avec % de dégâts configurable** (0-100%)
- Méthodes de compatibilité avec TeamId enum (legacy 4 teams)

**API Principales:**
```csharp
// Attribution
int teamIndex = teamManager.AssignPlayerAutoBalance(playerId);
teamManager.AssignPlayerToTeam(playerId, teamIndex);

// Queries
int teamIndex = teamManager.GetPlayerTeamIndex(playerId);
bool canDamage = teamManager.CanDamage(attackerId, victimId);

// Friendly Fire avec dégâts réduits
float multiplier = teamManager.GetDamageMultiplier(attackerId, victimId);
// Retourne: 0.0 si FF désactivé, 0.0-1.0 si FF activé (ex: 0.5 = 50% dégâts), 1.0 pour ennemis

// Scores
teamManager.AddTeamScore(teamIndex, points);
int score = teamManager.GetTeamScore(teamIndex);
int winningTeam = teamManager.GetWinningTeamIndex();

// Spawns & Info
Transform spawn = teamManager.GetRandomSpawnPoint(teamIndex);
Color color = teamManager.GetTeamColor(teamIndex);
string name = teamManager.GetTeamName(teamIndex);
```

---

## 💥 Combat Module

### M2922_HealthController
**Responsabilités:**
- Gestion de la santé/dégâts pour toute entité
- Régénération automatique (optionnel)
- Système de respawn
- Support headshot
- Statistiques de dégâts

**Usage:**
```csharp
healthController.TakeDamage(50f, attackerId, DamageType.Bullet);
healthController.Heal(25f);
healthController.Respawn();
```

**Fonctionne:**
- ✅ Avec TeamManager (vérification friendly fire dans PlayerController)
- ✅ Sans TeamManager (mode FFA - tout le monde s'attaque)

---

## 🎮 GameModes Module (VOTRE CODE)

**C'est ICI que vous implémentez vos règles de jeu!**

### Exemples de GameModes:

#### Free-for-All (FFA)
```csharp
// Pas besoin de TeamManager!
public class M2922_FreeForAll : M2922_Base
{
    public int KillsToWin = 20;
    // Gérez kills, scores, victoire ici
}
```

#### Team Deathmatch (TDM)
```csharp
// Nécessite TeamManager
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_TeamDeathmatch : M2922_Base
{
    private M2922_TeamManager _teamManager;
    public int TeamKillsToWin = 50;
    // Gérez team kills, team scores, rounds ici
}
```

#### Capture the Flag (CTF)
```csharp
// Nécessite TeamManager
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_CaptureTheFlag : M2922_Base
{
    private M2922_TeamManager _teamManager;
    public GameObject[] TeamFlags;
    // Gérez captures, flags, bases ici
}
```

**📖 Voir**: `GameModes/GAMEMODE_GUIDE.md` pour guide complet

---

## 🎯 Player Module (EXEMPLE)

### M2922_PlayerController
**Exemple d'implémentation** combinant:
- HealthController
- ITeamMember (optionnel)
- Event subscription
- Stats (kills/deaths/score)

**Fonctionne en mode:**
- ✅ FFA (sans TeamManager) → Team = None, pas de friendly fire check
- ✅ Team-based (avec TeamManager) → Team assigon automatique, team colors, team spawns

**Usage:**
```csharp
playerController.TakeDamage(50f, attackerId);
playerController.Respawn(position, rotation);
playerController.RespawnAtTeamSpawn(); // Si TeamManager existe
```

---

## 🛠️ Setup Wizard

### M2922 > Setup > Project Setup Wizard

**Configuration sauvegardée automatiquement (EditorPrefs)**

**Modules disponibles:**
- ✅ Core (Manager + EventBus) - **REQUIS**
- ✅ Team System - **OPTIONNEL** (décocher pour FFA)
- ✅ Combat System
- ✅ Spawning System
- ✅ Autres modules (Score, Vehicles, GameModes)

**Paramètres configurables:**
- Nombre d'équipes (2-16)
- Spawns par équipe
- Durée de round, Score max, etc.
- Auto-balance, Friendly fire
- Mode Debug

---

## 🔌 EventBus - Communication Décentralisée

**Événements disponibles:**
```csharp
EventType.OnPlayerJoined
EventType.OnPlayerLeft
EventType.OnPlayerKilled
EventType.OnPlayerDied
EventType.OnPlayerRespawned
EventType.OnDamageTaken
EventType.OnTeamChanged
EventType.OnTeamScoreChanged
EventType.OnGameStarted
EventType.OnGameEnded
EventType.OnRoundStarted
EventType.OnRoundEnded
// etc.
```

**Usage:**
```csharp
// S'abonner
Manager.EventBus.Subscribe(EventType.OnPlayerKilled, this);

// Publier
var killData = new KillEventData(killerId, victimId, weaponId);
Manager.EventBus.Publish(EventType.OnPlayerKilled, killData);

// Callback
public void OnPlayerKilled()
{
    var data = Manager.EventBus.GetLastEventData(EventType.OnPlayerKilled);
    // Traiter l'événement
}

// Se désabonner
Manager.EventBus.Unsubscribe(EventType.OnPlayerKilled, this);
```

---

## 📝 Interfaces (Documentation)

**IMPORTANT**: UdonSharp ne supporte PAS l'héritage d'interface au runtime!
Ces interfaces sont pour la **documentation** uniquement.

### ITeamMember
- Team (TeamId)
- SetTeam(TeamId)
- IsFriendly(TeamId)
- IsEnemy(TeamId)

### IDamageable
- Health, MaxHealth
- IsAlive, CanTakeDamage
- TakeDamage(damage, attackerId, damageType)
- Heal(amount)
- Die()

### IEntity
- EntityId, EntityName, Type
- EntityTransform, IsActive

---

## 🚀 Quick Start

### NIVEAU 1 - PvP Libre/Sandbox (Sans règles):
1. Ouvrir **M2922 > Setup > Project Setup Wizard**
2. ✅ Core Systems
3. ❌ Team System: DÉSACTIVÉ
4. ✅ Combat System
5. ❌ **Ne PAS créer de GameMode**
6. Créer Setup
7. **C'est tout!** Vous avez un système de combat fonctionnel sans règles

**Résultat**: Combat libre, pas de rounds, pas de scores - juste tirer et tuer

---

### NIVEAU 2 - Combat en Équipes (Sans règles):
1. Ouvrir **M2922 > Setup > Project Setup Wizard**
2. ✅ Core Systems
3. ✅ **Team System: ACTIVÉ** (configurer N équipes)
4. ✅ Combat System
5. ❌ **Ne PAS créer de GameMode**
6. Créer Setup
7. **C'est tout!** Combat en équipes sans règles de jeu

**Résultat**: Combat en équipes, team colors, friendly fire - mais pas de système de score/victoire

---

### NIVEAU 3 - Mode de Jeu Complet (Avec règles):
1. Setup comme Niveau 1 ou 2 (selon si vous voulez des équipes)
2. **Créer votre GameMode**:
   ```csharp
   // Dans GameModes/M2922_MyGameMode.cs
   public class M2922_MyGameMode : M2922_Base
   {
       public int KillsToWin = 20;
       public float RoundDuration = 600f;
       
       // Votre logique ici
   }
   ```
3. Ajouter le script GameMode au scene
4. Configurer vos règles
5. **C'est tout!** Vous avez un mode de jeu complet

**Résultat**: Système de jeu complet avec rounds, scores, conditions de victoire

---

### Génération des Udon Assets:
**M2922 > Udon > Generate Program Assets**

---

## 🎓 Philosophie de Design

### Modularité à Niveaux
Le système fonctionne à **3 niveaux** d'utilisation:
1. **Core minimal** - Juste combat (sandbox)
2. **Core + Teams** - Combat en équipes (sandbox team)
3. **Core + GameMode** - Règles de jeu complètes

Vous choisissez le niveau dont vous avez besoin.

### Séparation des Responsabilités
- **Core**: Infrastructure, état, événements
- **Teams**: Gestion d'équipes (optionnel)
- **Combat**: Dégâts/santé (générique)
- **GameModes**: VOS règles de jeu

### Extensibilité
Créez vos propres modules en héritant de `M2922_Base`.

### Communication Décentralisée
Utilisez l'EventBus au lieu de références directes.

---

## ⚠️ Limitations UdonSharp

1. **Pas d'interfaces au runtime** → Utilisez implementation manuelle
2. **Pas de champs statiques** sur custom types → Utilisez GameObject.Find()
3. **Pas de typeof()** sur custom types → Utilisez GameObject.Find() + GetComponent()
4. **Manual sync** requis → Utilisez [UdonSynced] + RequestSerialization()

---

## 📚 Documentation Complète

### Guides Essentiels

- **[INTERFACES.md](INTERFACES.md)** - Patterns d'interfaces (IDamageable, IEntity, ITeamMember)
- **[ARCHITECTURE_3_LEVELS.md](ARCHITECTURE_3_LEVELS.md)** - Architecture modulaire à 3 niveaux
- **[GAMEMODE_GUIDE.md](GAMEMODE_GUIDE.md)** - Création de modes de jeu personnalisés

### Guides de Fonctionnalités

- **[SPAWN_SYSTEM_GUIDE.md](SPAWN_SYSTEM_GUIDE.md)** - Système de spawn (Team + FFA)
- **[FRIENDLY_FIRE_GUIDE.md](FRIENDLY_FIRE_GUIDE.md)** - Configuration du Friendly Fire

### Référence API

- **Voir commentaires XML** dans chaque script pour la documentation détaillée

---

## 🤝 Contribution

Pour ajouter un nouveau module:

1. Créer dossier `Assets/_MIAOU_/Script/[NomModule]/`
2. Hériter de `M2922_Base`
3. Utiliser EventBus pour communication
4. Documenter les dépendances (optionnel/requis)
5. Ajouter au Setup Wizard si nécessaire

---

**Remember**: M2922 est un système **modulaire** - activez seulement ce dont vous avez besoin!
