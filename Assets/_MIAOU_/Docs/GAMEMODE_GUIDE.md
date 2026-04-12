# M2922 GameModes - Guide de Création

## 🎯 Comprendre l'Architecture à Niveaux

Le système M2922 fonctionne à **3 NIVEAUX** d'utilisation. Les GameModes sont le **NIVEAU 3** (optionnel).

### NIVEAU 1 - Core Minimal (PAS de GameMode)
```
Core (Manager + EventBus) + Combat
= Combat fonctionnel SANS règles
```

**Utilisation:**
- PvP libre / Sandbox
- Zone de combat ouverte
- Tests de gameplay
- **AUCUNE règle de jeu requise**

**Caractéristiques:**
- ✅ Combat fonctionne
- ✅ Événements OnPlayerKilled, OnPlayerDied publiés
- ❌ Pas de rounds
- ❌ Pas de scores
- ❌ Pas de conditions de victoire
- ❌ Pas de timer

**Exemple d'usage:**
```csharp
// Juste du combat, rien d'autre
healthController.TakeDamage(50f, attackerId, DamageType.Bullet);
// → Joueur meurt
// → Événement OnPlayerKilled publié
// → C'est tout! Pas de logique de jeu
```

---

### NIVEAU 2 - Avec Teams (PAS de GameMode)
```
Core + Combat + TeamManager
= Combat en équipes SANS règles
```

**Utilisation:**
- Combat en équipes libre
- Sandbox team-based
- Tests de gameplay multi-équipes
- **AUCUNE règle de jeu requise**

**Caractéristiques:**
- ✅ Combat en équipes
- ✅ Friendly fire configurable
- ✅ Team colors, team spawns
- ✅ Événements OnTeamScoreChanged (si vous appelez AddTeamScore manuellement)
- ❌ Pas de rounds
- ❌ Pas de système de score automatique
- ❌ Pas de conditions de victoire
- ❌ Pas de timer

---

### NIVEAU 3 - Avec GameMode (VOTRE CODE)
```
Core + Combat + (Teams optionnel) + VOTRE GAMEMODE
= Règles de jeu complètes
```

**C'est ICI que vous implémentez vos GameModes!**

Les GameModes ajoutent:
- ✅ Rounds
- ✅ Scores automatiques
- ✅ Conditions de victoire
- ✅ Timers
- ✅ UI de score
- ✅ Logique de fin de partie
- ✅ Règles spécifiques à votre mode

---

## Quand Créer un GameMode?

### ✅ Créez un GameMode SI:
- Vous voulez des **rounds** (début/fin, réinitialisation)
- Vous voulez un **système de score** automatique
- Vous voulez des **conditions de victoire** (premier à X kills, timer, etc.)
- Vous voulez un **timer de round**
- Vous voulez une **interface utilisateur** de score
- Vous voulez une **logique de fin de partie** (victoire/défaite)

### ❌ NE créez PAS de GameMode SI:
- Vous voulez juste un **combat libre** (sandbox)
- Vous n'avez **pas besoin de règles**
- Vous testez le **gameplay de base**
- Vous construisez une **zone de combat ouverte** sans structure

---

## Architecture Modulaire

Les GameModes NE SONT PAS obligatoires. Voici ce dont vous avez besoin selon votre objectif:

| Objectif | Core | Combat | Teams | GameMode |
|----------|------|--------|-------|----------|
| Sandbox PvP | ✅ | ✅ | ❌ | ❌ |
| Sandbox Team | ✅ | ✅ | ✅ | ❌ |
| FFA structuré | ✅ | ✅ | ❌ | ✅ |
| Team Deathmatch | ✅ | ✅ | ✅ | ✅ |
| Capture the Flag | ✅ | ✅ | ✅ | ✅ |
| Battle Royale | ✅ | ✅ | ❌ | ✅ |

---

## ⚙️ Manager MINIMAL + GameModes

### Le Manager Core est MINIMAL

Le `M2922_Manager` est volontairement **MINIMAL** et **NE CONTIENT AUCUNE RÈGLE DE JEU**:

```csharp
public class M2922_Manager : M2922_Base
{
    // Variables d'état (lecture/écriture par GameModes)
    [UdonSynced] public GameState CurrentState;  
    [UdonSynced] public int CurrentRound;
    [UdonSynced] public float RoundTimeRemaining;
    [UdonSynced] public int PlayerCount;
    
    // Méthode pour changer l'état
    public void SetGameState(GameState newState)
}
```

**Ce que fait le Manager:**
- ✅ Gère l'état du jeu (Waiting, Starting, InProgress, Ending)
- ✅ Suit les joueurs (PlayerCount, OnPlayerJoined, OnPlayerLeft)
- ✅ Expose des variables publiques pour vos GameModes
- ✅ Publie des événements via EventBus

**Ce que le Manager NE fait PAS:**
- ❌ Timers automatiques
- ❌ Système de rounds
- ❌ Logique de score
- ❌ Conditions de victoire

### Créer un GameMode pour ajouter des règles

Pour avoir des règles de jeu (rounds, scores, timers), créez un GameMode dédié:

```csharp
public class M2922_MyGameMode : M2922_Base
{
    [Header("=== CUSTOM RULES ===")]
    public int KillsToWin = 20;
    public float RoundDuration = 300f;
    public bool EnablePowerups = true;
    public int LivesPerPlayer = 3;
    
    private M2922_Manager _manager;
    
    protected override void Start()
    {
        base.Start();
        _manager = TryFindManager();
        
        // Votre logique d'initialisation
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Gérer le timer de round
        if (_manager.CurrentState == GameState.InProgress)
        {
            _manager.RoundTimeRemaining -= Time.deltaTime;
            if (_manager.RoundTimeRemaining <= 0)
            {
                EndRound();
            }
        }
    }
}
```

**Avantages:**
- ✅ Contrôle total sur les règles
- ✅ Logique custom illimitée
- ✅ Séparation claire core/gameplay
- ✅ Extensible et maintenable
- ✅ Architecture modulaire propre

**Usage recommandé:** Production, vrais modes de jeu, logique complexe

---

## Modes de Jeu Recommandés

### 🔴 Free-for-All (FFA) - Sans TeamManager
**Concept**: Chacun pour soi, dernier survivant gagne

**Implémentation**:
```csharp
public class M2922_FreeForAll : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public int KillsToWin = 20;
    public float RoundDuration = 600f; // 10 minutes
    
    private int[] _playerKills;
    
    // Pas besoin de TeamManager!
    // Gérez les kills, scores et conditions de victoire ici
}
```

**Ne nécessite PAS**: TeamManager

---

### 🔵 Team Deathmatch (TDM) - Avec TeamManager
**Concept**: 2+ équipes s'affrontent, première à X kills gagne

**Implémentation**:
```csharp
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_TeamDeathmatch : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public int TeamKillsToWin = 50;
    public int MaxRounds = 3;
    
    private M2922_TeamManager _teamManager;
    
    protected override void Start()
    {
        base.Start();
        _teamManager = GetComponent<M2922_TeamManager>();
        
        // S'abonner aux événements de kill
        Manager.EventBus.Subscribe(EventType.OnPlayerKilled, this);
    }
    
    public void OnPlayerKilled()
    {
        var killData = Manager.EventBus.GetLastEventData(EventType.OnPlayerKilled);
        
        // Récupérer l'équipe du tueur
        int killerTeam = _teamManager.GetPlayerTeamIndex(killData.KillerId);
        
        // Ajouter points à l'équipe
        _teamManager.AddTeamScore(killerTeam, 1);
        
        // Vérifier victoire
        if (_teamManager.GetTeamScore(killerTeam) >= TeamKillsToWin)
        {
            EndRoundWithWinner(killerTeam);
        }
    }
}
```

**Nécessite**: TeamManager

---

### 🚩 Capture the Flag (CTF) - Avec TeamManager
**Concept**: Capturer le drapeau ennemi et le ramener à sa base

**Implémentation**:
```csharp
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_CaptureTheFlag : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public int CaptureScoreToWin = 3;
    public float RoundDuration = 900f; // 15 minutes
    
    [Header("=== FLAG SETUP ===")]
    public GameObject[] TeamFlags; // Un flag par équipe
    public Transform[] TeamBases;
    
    private M2922_TeamManager _teamManager;
    private int[] _teamCaptures;
    
    // Logique de capture de drapeau
}
```

**Nécessite**: TeamManager

---

### 📍 Domination - Avec TeamManager
**Concept**: Contrôler des zones pour marquer des points

**Implémentation**:
```csharp
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_Domination : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public int ScoreToWin = 200;
    public float PointsPerSecondPerZone = 1f;
    
    [Header("=== ZONE SETUP ===")]
    public M2922_CaptureZone[] CaptureZones;
    
    private M2922_TeamManager _teamManager;
    
    protected override void Update()
    {
        base.Update();
        
        // Ajouter des points basés sur les zones contrôlées
        foreach (var zone in CaptureZones)
        {
            int controllingTeam = zone.GetControllingTeam();
            if (controllingTeam >= 0)
            {
                float points = PointsPerSecondPerZone * Time.deltaTime;
                _teamManager.AddTeamScore(controllingTeam, (int)points);
            }
        }
    }
}
```

**Nécessite**: TeamManager

---

### ⚔️ Battle Royale - Sans TeamManager
**Concept**: Zone de jeu qui rétrécit, dernier survivant gagne

**Implémentation**:
```csharp
public class M2922_BattleRoyale : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public float ZoneShrinkInterval = 60f; // 1 minute
    public float DamageOutsideZone = 5f;
    
    [Header("=== ZONE SETUP ===")]
    public Transform SafeZone;
    public float InitialZoneRadius = 100f;
    public float MinZoneRadius = 10f;
    
    // Pas besoin de TeamManager - Mode solo
}
```

**Ne nécessite PAS**: TeamManager

---

## Template de GameMode

```csharp
using UdonSharp;
using UnityEngine;
using M2922.Core;

namespace M2922.GameModes
{
    /// <summary>
    /// [NOM DU MODE] - [DESCRIPTION]
    /// 
    /// RÈGLES:
    /// - Règle 1
    /// - Règle 2
    /// 
    /// NÉCESSITE:
    /// - TeamManager: [OUI/NON]
    /// - Modules custom: [Liste]
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_[NomDuMode] : M2922_Base
    {
        [Header("=== GAME RULES ===")]
        // Vos règles et paramètres ici
        
        [Header("=== REFERENCES ===")]
        // TeamManager si nécessaire
        // Autres références
        
        // Synced variables pour le réseau
        [UdonSynced] private int _currentScore = 0;
        
        protected override void Start()
        {
            base.Start();
            
            // S'abonner aux événements nécessaires
            if (Manager != null && Manager.EventBus != null)
            {
                Manager.EventBus.Subscribe(EventType.OnPlayerKilled, this);
                Manager.EventBus.Subscribe(EventType.OnGameStarted, this);
                // etc.
            }
        }
        
        // Callbacks d'événements
        public void OnPlayerKilled()
        {
            // Gérer la logique de kill
        }
        
        public void OnGameStarted()
        {
            // Initialiser le mode de jeu
        }
        
        // Logique spécifique au mode
        private void CheckWinCondition()
        {
            // Vérifier les conditions de victoire
        }
    }
}
```

---

## Bonnes Pratiques

### ✅ À FAIRE:
- Implémenter les règles de jeu dans un script GameMode dédié
- Utiliser l'EventBus pour la communication
- Synchroniser les variables importantes avec `[UdonSynced]`
- Vérifier si TeamManager existe avant de l'utiliser (pour compatibilité FFA)
- Documenter clairement les règles et dépendances

### ❌ À ÉVITER:
- Mettre la logique de jeu dans M2922_Manager (c'est le système core!)
- Supposer que TeamManager existe toujours
- Hardcoder les valeurs - utilisez des champs serializables
- Oublier de synchroniser l'état du jeu sur le réseau

---

## Choisir TeamManager ou Non?

### ✅ Utilisez TeamManager SI:
- Votre mode a des équipes fixes (2+)
- Vous avez besoin de scores par équipe
- Vous avez besoin de spawns par équipe
- Vous voulez gérer le friendly fire
- Exemples: TDM, CTF, Domination, Payload, King of the Hill

### ❌ N'utilisez PAS TeamManager SI:
- Mode Free-for-all (chacun pour soi)
- Pas d'équipes fixes
- Équipes dynamiques/temporaires gérées différemment
- Exemples: FFA, Battle Royale, Gun Game, Infection (au début)

---

## Exemples de Configuration

### Setup FFA (Sans équipes):
1. Ouvrir M2922 Setup Wizard
2. ✅ Core Systems: Manager + EventBus
3. ❌ Team System: **DÉSACTIVÉ**
4. ✅ Combat System
5. ✅ Spawning System (spawns génériques, pas par équipe)

### Setup Team-based (Avec équipes):
1. Ouvrir M2922 Setup Wizard
2. ✅ Core Systems: Manager + EventBus
3. ✅ Team System: **ACTIVÉ** (configurer nombre d'équipes)
4. ✅ Combat System
5. ✅ Spawning System (spawns par équipe)

---

## Ressources

- **EventBus Events**: Voir `EventDataStructs.cs` pour tous les types d'événements
- **Architecture Guide**: Voir `/Assets/_MIAOU_/ARCHITECTURE_GUIDE.md`
- **Team System**: Voir `M2922_TeamManager.cs` pour API complète

---

## Contribution

Créez vos propres modes de jeu et partagez-les!

Structure recommandée:
```
GameModes/
  ├── FFA/
  │   └── M2922_FreeForAll.cs
  ├── TeamBased/
  │   ├── M2922_TeamDeathmatch.cs
  │   ├── M2922_CaptureTheFlag.cs
  │   └── M2922_Domination.cs
  └── Special/
      ├── M2922_BattleRoyale.cs
      └── M2922_Infection.cs
```

---

**Rappel Important**: 

Le système M2922 fonctionne à **3 NIVEAUX**:

1. **NIVEAU 1 - Core + Combat**: Fonctionne SANS GameMode (sandbox PvP libre)
2. **NIVEAU 2 - Core + Combat + Teams**: Fonctionne SANS GameMode (sandbox team-based)
3. **NIVEAU 3 - Avec GameMode**: Ajout de VOS règles de jeu (rounds, scores, victoire)

**Les GameModes sont OPTIONNELS** - Le système combat fonctionne parfaitement sans eux!

Ne mettez PAS la logique de jeu dans le système core - créez vos propres GameModes seulement si vous avez besoin de règles structurées!
