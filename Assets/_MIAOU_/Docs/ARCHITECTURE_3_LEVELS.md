# M2922 - Architecture à 3 Niveaux

## 🎯 Concept Principal

Le système M2922 PvP est conçu pour fonctionner à **3 NIVEAUX** d'utilisation. Vous choisissez le niveau dont vous avez besoin.

---

## NIVEAU 1️⃣ - Core Minimal (Sandbox PvP Libre)

### Ce qui est activé:
```
✅ Core (Manager + EventBus)
✅ Combat (HealthController)
❌ Teams
❌ GameMode
```

### Résultat:
**Combat fonctionnel SANS règles de jeu**

- ✅ Les joueurs peuvent se tirer dessus
- ✅ Système de santé/dégâts fonctionne
- ✅ Événements OnPlayerKilled, OnPlayerDied publiés
- ❌ Pas de rounds
- ❌ Pas de scores
- ❌ Pas de timer
- ❌ Pas de conditions de victoire

### Utilisation:
- Zone de combat ouverte
- Sandbox PvP
- Tests de gameplay
- Prototype initial

### Exemple de code:
```csharp
// C'est tout ce dont vous avez besoin!
healthController.TakeDamage(50f, attackerId, DamageType.Bullet);

// → Joueur meurt
// → Événement OnPlayerKilled publié
// → PAS de logique de jeu supplémentaire
```

### Setup:
1. Wizard: ✅ Core, ✅ Combat, ❌ Teams, ❌ GameModes
2. Créer Setup
3. **C'est tout!** Combat fonctionne

---

## NIVEAU 2️⃣ - Combat en Équipes (Sandbox Team-Based)

### Ce qui est activé:
```
✅ Core (Manager + EventBus)
✅ Combat (HealthController)
✅ Teams (TeamManager)
❌ GameMode
```

### Résultat:
**Combat en équipes SANS règles de jeu**

- ✅ Tout du Niveau 1, PLUS:
- ✅ Gestion d'équipes (2-16 teams)
- ✅ Friendly fire configurable
- ✅ Team colors automatiques
- ✅ Team spawns
- ✅ Événements OnTeamScoreChanged (si AddTeamScore appelé manuellement)
- ❌ Toujours pas de rounds
- ❌ Toujours pas de système de score automatique
- ❌ Toujours pas de timer/victoire

### Utilisation:
- Zone de combat par équipes
- Sandbox team-based
- Tests multi-équipes
- Prototype team gameplay

### Exemple de code:
```csharp
// Vérification friendly fire
if (teamManager.CanDamage(attackerId, victimId))
{
    healthController.TakeDamage(50f, attackerId, DamageType.Bullet);
}

// Optionnel: Ajouter score manuellement
teamManager.AddTeamScore(killerTeamIndex, 10);

// → Combat en équipes fonctionne
// → MAIS toujours pas de logique de jeu automatique
```

### Setup:
1. Wizard: ✅ Core, ✅ Combat, ✅ Teams (choisir nombre), ❌ GameModes
2. Créer Setup
3. **C'est tout!** Combat en équipes fonctionne

---

## NIVEAU 3️⃣ - Avec GameMode (Règles Complètes)

### Ce qui est activé:
```
✅ Core (Manager + EventBus)
✅ Combat (HealthController)
✅ ou ❌ Teams (selon le mode)
✅ VOTRE GAMEMODE SCRIPT
```

### Résultat:
**Mode de jeu complet avec TOUTES les règles**

- ✅ Tout des niveaux précédents, PLUS:
- ✅ **Gestion de rounds** (début/fin, réinitialisation)
- ✅ **Système de score automatique**
- ✅ **Timer de round**
- ✅ **Conditions de victoire** (premier à X kills, timer, etc.)
- ✅ **UI de score**
- ✅ **Logique de fin de partie**
- ✅ **Règles custom illimitées**

### Utilisation:
- Modes de jeu structurés
- Production
- Vrais modes PvP complets

### Exemples de GameModes:

#### Free-for-All (FFA)
```csharp
// GameModes/M2922_FreeForAll.cs
public class M2922_FreeForAll : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public int KillsToWin = 20;
    public float MatchDuration = 600f;
    
    private int[] _playerKills;
    
    protected override void Start()
    {
        base.Start();
        Manager.EventBus.Subscribe(EventType.OnPlayerKilled, this);
    }
    
    public void OnPlayerKilled()
    {
        var data = Manager.EventBus.GetLastEventData(EventType.OnPlayerKilled);
        _playerKills[data.KillerId]++;
        
        // Vérifier victoire
        if (_playerKills[data.KillerId] >= KillsToWin)
        {
            EndGameWithWinner(data.KillerId);
        }
    }
    
    // Votre logique de timer, rounds, UI, etc.
}
```

#### Team Deathmatch (TDM)
```csharp
// GameModes/M2922_TeamDeathmatch.cs
[RequireComponent(typeof(M2922_TeamManager))]
public class M2922_TeamDeathmatch : M2922_Base
{
    [Header("=== GAME RULES ===")]
    public int TeamKillsToWin = 50;
    public int RoundsToWin = 2;
    public float RoundDuration = 300f;
    
    private M2922_TeamManager _teamManager;
    private int[] _teamRoundWins;
    
    protected override void Start()
    {
        base.Start();
        _teamManager = GetComponent<M2922_TeamManager>();
        Manager.EventBus.Subscribe(EventType.OnPlayerKilled, this);
    }
    
    public void OnPlayerKilled()
    {
        var data = Manager.EventBus.GetLastEventData(EventType.OnPlayerKilled);
        int killerTeam = _teamManager.GetPlayerTeamIndex(data.KillerId);
        
        // Ajouter score
        _teamManager.AddTeamScore(killerTeam, 1);
        
        // Vérifier victoire de round
        if (_teamManager.GetTeamScore(killerTeam) >= TeamKillsToWin)
        {
            EndRoundWithWinner(killerTeam);
            _teamRoundWins[killerTeam]++;
            
            // Vérifier victoire de match
            if (_teamRoundWins[killerTeam] >= RoundsToWin)
            {
                EndMatchWithWinner(killerTeam);
            }
        }
    }
    
    // Votre logique de rounds, timer, UI, etc.
}
```

### Setup:
1. Wizard: Setup comme Niveau 1 ou 2 (selon si vous voulez teams)
2. Créer Setup
3. **Créer votre script GameMode** dans `GameModes/`
4. Ajouter le GameMode à la scène
5. Configurer vos règles

---

## 🎓 Quelle Approche Choisir?

### Utilisez NIVEAU 1 si:
- ❓ Vous voulez juste du combat libre
- ❓ Vous testez le gameplay de base
- ❓ Vous construisez une zone PvP ouverte
- ❓ Vous n'avez pas besoin de règles

### Utilisez NIVEAU 2 si:
- ❓ Vous voulez du combat en équipes
- ❓ Mais toujours sans règles structurées
- ❓ Zone PvP team-based ouverte
- ❓ Tests multi-équipes

### Utilisez NIVEAU 3 si:
- ❓ Vous voulez un vrai mode de jeu
- ❓ Vous avez besoin de rounds/scores/timer
- ❓ Vous voulez des conditions de victoire
- ❓ Vous construisez pour production

---

## ⚙️ Variables d'État du Manager (Pour GameModes)

Le `M2922_Manager` expose des **variables d'état publiques** que vos GameModes peuvent utiliser:

```csharp
// Variables d'état (UdonSynced)
public GameState CurrentState;     // État actuel du jeu
public int CurrentRound;            // Numéro du round (géré par GameMode)
public float RoundTimeRemaining;   // Temps restant (géré par GameMode)
public int PlayerCount;             // Nombre de joueurs connectés

// Méthode pour changer l'état
public void SetGameState(GameState newState)
```

### ⚠️ IMPORTANT - Le Manager est MINIMAL:
Le Manager core **NE CONTIENT AUCUNE RÈGLE DE JEU**.

**Ce qu'il fait:**
- ✅ Gère l'état du jeu (Waiting, Starting, InProgress, Ending)
- ✅ Suit les joueurs (PlayerCount, OnPlayerJoined, OnPlayerLeft)
- ✅ Expose des variables d'état pour vos GameModes
- ✅ Publie des événements via EventBus

**Ce qu'il NE fait PAS:**
- ❌ AUCUN timer automatique
- ❌ AUCUN système de rounds
- ❌ AUCUNE logique de score
- ❌ AUCUNE condition de victoire

### 🎮 Créer vos règles de jeu:
Créez un script GameMode personnalisé dans `GameModes/` qui:
1. Lit/modifie les variables d'état publiques du Manager
2. Implémente vos règles de jeu (timers, rounds, scores)
3. Appelle `SetGameState()` pour changer l'état
4. Écoute les événements du EventBus

**Exemple:** Voir `GAMEMODE_GUIDE.md` pour des exemples complets.

---

## 📊 Comparaison

| Fonctionnalité | Niveau 1 | Niveau 2 | Niveau 3 |
|----------------|----------|----------|----------|
| Combat | ✅ | ✅ | ✅ |
| Équipes | ❌ | ✅ | ✅/❌ |
| Team Colors | ❌ | ✅ | ✅/❌ |
| Friendly Fire | ❌ | ✅ | ✅/❌ |
| Rounds | ❌ | ❌ | ✅ |
| Scores | ❌ | ❌ | ✅ |
| Timer | ❌ | ❌ | ✅ |
| Victoire | ❌ | ❌ | ✅ |
| UI Score | ❌ | ❌ | ✅ |
| Code requis | ❌ | ❌ | ✅ |

---

## 🚀 Quick Start

### Pour NIVEAU 1️⃣ (Sandbox PvP):
```
1. Setup Wizard
2. ✅ Core + Combat
3. ❌ Teams, ❌ GameModes
4. Créer Setup
5. ✅ TERMINÉ!
```

### Pour NIVEAU 2️⃣ (Sandbox Team):
```
1. Setup Wizard
2. ✅ Core + Combat + Teams
3. ❌ GameModes
4. Créer Setup
5. ✅ TERMINÉ!
```

### Pour NIVEAU 3️⃣ (Mode de Jeu):
```
1. Setup Wizard (comme Niveau 1 ou 2)
2. Créer Setup
3. Créer script GameMode dans GameModes/
4. Ajouter GameMode à la scène
5. Implémenter vos règles
6. ✅ TERMINÉ!
```

---

## 📚 Documentation Complète

- **Quick Start**: `README.md`
- **GameModes Guide**: `GAMEMODE_GUIDE.md`
- **Setup Wizard**: M2922 > Setup > Project Setup Wizard

---

**Rappel**: Le système M2922 fonctionne SANS GameMode! Les GameModes sont optionnels et ajoutent vos règles de jeu structurées.
