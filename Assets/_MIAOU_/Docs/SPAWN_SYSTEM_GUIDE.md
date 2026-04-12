# 🎯 Système de Spawn - Guide Complet

## Vue d'ensemble

Le système de spawn M2922 gère la génération et le respawn des joueurs dans votre monde VRChat. Il supporte à la fois le **mode Team-based** (spawns par équipe) et le **mode FFA** (spawns génériques).

---

## 📦 Composants

### M2922_SpawnManager
Gestionnaire central du système de spawn.

**Responsabilités:**
- Sélection des points de spawn
- Téléportation des joueurs
- Gestion du respawn automatique
- Protection de spawn (invincibilité temporaire)
- Cooldown des spawn points

**Stratégies de spawn:**
- `Random`: Spawn aléatoire parmi les points disponibles
- `Sequential`: Rotation séquentielle entre les points
- `LeastRecent`: Spawn au point le moins récemment utilisé
- `Farthest`: Spawn au point le plus éloigné des ennemis (TODO)

### M2922_SpawnPoint
Marque un point de spawn individuel.

**Propriétés:**
- `TeamIndex`: Index de l'équipe (-1 pour FFA)
- `Priority`: Priorité de ce spawn
- `IsActive`: Ce spawn est-il actif?
- Tracking interne (dernier spawn, nombre de spawns)

**Visuel:**
- Gizmos dans l'éditeur pour visualiser position et direction
- Couleur personnalisable par équipe
- Label avec infos quand sélectionné

---

## ⚙️ Configuration

### Via le Setup Wizard

1. Ouvrir `M2922 > Setup > Project Setup Wizard`
2. Cocher **"Système de Spawn"**
3. Configurer le nombre de spawns:
   - **Mode Team**: Nombre de spawns par équipe (1-10)
   - **Mode FFA**: Nombre total de spawns (4-20)
4. Créer le setup

Le wizard créera automatiquement:
- `SpawnManager` GameObject avec composant M2922_SpawnManager
- Points de spawn avec composants M2922_SpawnPoint
- Configuration automatique des références

### Mode Team-based

```
[M2922_System]
├── Manager
├── EventBus
├── TeamManager
├── SpawnManager ← Créé automatiquement
└── SpawnPoints/
    ├── Spawns_Red/
    │   ├── Spawn_Red_1 (M2922_SpawnPoint, teamIndex=0)
    │   ├── Spawn_Red_2 (M2922_SpawnPoint, teamIndex=0)
    │   └── ...
    ├── Spawns_Blue/
    │   ├── Spawn_Blue_1 (M2922_SpawnPoint, teamIndex=1)
    │   └── ...
    └── ...
```

### Mode FFA

```
[M2922_System]
├── Manager
├── EventBus
├── SpawnManager ← Créé automatiquement
└── SpawnPoints/
    ├── SpawnPoint_1 (M2922_SpawnPoint, teamIndex=-1)
    ├── SpawnPoint_2 (M2922_SpawnPoint, teamIndex=-1)
    └── ...
```

---

## 💻 Utilisation dans le Code

### Spawner un joueur manuellement

```csharp
using M2922.Spawning;

public class MyGameMode : M2922_Base
{
    [SerializeField] private M2922_SpawnManager _spawnManager;
    
    protected override void Start()
    {
        base.Start();
        
        // Trouver automatiquement si pas assigné
        if (_spawnManager == null)
        {
            _spawnManager = GameObject.FindObjectOfType<M2922_SpawnManager>();
        }
    }
    
    public void SpawnAllPlayers()
    {
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        
        foreach (VRCPlayerApi player in players)
        {
            _spawnManager.SpawnPlayer(player);
        }
    }
    
    public void SpawnLocalPlayer()
    {
        _spawnManager.SpawnLocalPlayer();
    }
}
```

### Obtenir un spawn point pour une équipe

```csharp
// Obtenir un spawn aléatoire pour l'équipe 0 (Red)
Transform spawnTransform = _spawnManager.GetRandomTeamSpawn(0);

if (spawnTransform != null)
{
    player.TeleportTo(spawnTransform.position, spawnTransform.rotation);
}
```

### Planifier un respawn automatique

```csharp
// Le joueur mourra et respawnera après 5 secondes
_spawnManager.ScheduleRespawn(player, 5f);
```

### Activer/Désactiver des spawns d'équipe

```csharp
// Désactiver tous les spawns de l'équipe 1 (Blue)
_spawnManager.SetTeamSpawnsActive(1, false);

// Réactiver plus tard
_spawnManager.SetTeamSpawnsActive(1, true);
```

---

## 🎨 Personnalisation

### Ajouter des spawn points manuellement

1. Créer un GameObject vide
2. Ajouter le composant `M2922_SpawnPoint` (via Add Component > UdonSharp > M2922.Spawning)
3. Configurer:
   - `Team Index`: Index de l'équipe (-1 pour FFA)
   - `Priority`: Priorité (optionnel)
   - `Gizmo Color`: Couleur de visualisation

### Créer une stratégie de spawn custom

Actuellement, les stratégies sont enum dans `M2922_SpawnManager`. Pour une logique custom complexe:

```csharp
public class MyCustomSpawnManager : M2922_SpawnManager
{
    // Override SelectSpawnPoint pour logique custom
    protected M2922_SpawnPoint MyCustomSelection(int teamIndex)
    {
        // Votre logique ici
        // Ex: Spawn le plus proche d'un objectif
        // Ex: Spawn avec le plus d'alliés à proximité
    }
}
```

### Spawn Protection

Le SpawnManager supporte la protection de spawn (invincibilité temporaire):

```csharp
// Dans l'inspecteur du SpawnManager
Spawn Protection Duration: 3 secondes (défaut)
```

TODO: Implémenter l'application réelle de l'invincibilité au joueur spawné.

---

## 🔧 Paramètres Avancés

### SpawnManager Settings

| Paramètre | Description | Défaut |
|-----------|-------------|--------|
| `Spawn Strategy` | Stratégie de sélection | Random |
| `Spawn Protection Duration` | Durée d'invincibilité post-spawn | 3s |
| `Spawn Cooldown` | Délai minimum entre spawns au même point | 2s |
| `Min Enemy Distance` | Distance minimum des ennemis (0 = désactivé) | 5m |
| `Auto Respawn` | Activer respawn automatique | ✓ |
| `Respawn Delay` | Délai avant respawn auto | 5s |
| `Auto Discover Spawn Points` | Trouver automatiquement les SpawnPoints | ✓ |

### SpawnPoint Settings

| Paramètre | Description | Défaut |
|-----------|-------------|--------|
| `Team Index` | Index équipe (-1 pour FFA) | -1 |
| `Priority` | Priorité (plus élevé = préféré) | 0 |
| `Is Active` | Point actif? | ✓ |
| `Show Gizmo` | Afficher dans l'éditeur | ✓ |
| `Gizmo Color` | Couleur du gizmo | Green |
| `Gizmo Size` | Taille du gizmo | 0.5 |

---

## 🎮 Workflow Typique

### Mode Free-for-All (FFA)

1. Setup wizard avec "Système de Spawn" activé, Teams désactivé
2. Le wizard crée X spawn points génériques (teamIndex = -1)
3. SpawnManager spawn les joueurs aléatoirement parmi tous les points

### Mode Team Deathmatch (TDM)

1. Setup wizard avec "Système d'Équipes" + "Système de Spawn" activés
2. Le wizard crée X spawns par équipe avec teamIndex approprié
3. SpawnManager filtre automatiquement par équipe du joueur
4. Spawn uniquement aux points de l'équipe appropriée

### Mode Capture the Flag (CTF)

1. Setup comme TDM
2. Optionnel: Désactiver temporairement les spawns d'une base capturée:
   ```csharp
   if (baseIsCaptured)
   {
       _spawnManager.SetTeamSpawnsActive(capturedTeamIndex, false);
   }
   ```

### Mode Battle Royale

1. Setup en mode FFA
2. Tous les joueurs spawnent simultanément au début
3. Désactiver le respawn automatique:
   ```csharp
   // Dans l'inspecteur du SpawnManager
   Auto Respawn: ☐ (décoché)
   ```

---

## 🐛 Debugging

### Visualiser les spawns dans l'éditeur

Les `M2922_SpawnPoint` affichent des gizmos:
- **Sphère wireframe**: Position du spawn
- **Flèche**: Direction de spawn (forward)
- **Couleur**: Vert = actif, Gris = inactif, Couleur custom = équipe
- **Label** (quand sélectionné): Team index + Priority

### Logs

Activer le debug sur SpawnManager:
```csharp
// Dans M2922_Base
DEBUG = true; // Logs détaillés
```

Logs typiques:
```
[M2922 SpawnManager] SpawnManager initialized. Mode: Team-based, Strategy: Random
[M2922 SpawnManager] Spawned player PlayerName at Spawn_Red_3
[M2922 SpawnPoint] Spawn recorded. Total spawns: 5
```

### Problèmes courants

**"No valid spawn point found"**
- Vérifiez que des spawns existent pour cette équipe
- Vérifiez que les spawns sont actifs (`IsActive`)
- Vérifiez le cooldown (temps depuis dernier spawn)

**"No spawn points found"**
- Le wizard n'a pas créé de spawns (setupSpawning désactivé?)
- Activer `Auto Discover Spawn Points` dans SpawnManager
- Ou assigner manuellement `Manual Spawn Points`

**Joueurs spawnent tous au même endroit**
- Vérifiez la stratégie de spawn (doit être Random, pas Sequential avec bug)
- Vérifiez que plusieurs spawns existent pour l'équipe

---

## 📚 API Reference

### M2922_SpawnManager

```csharp
// Spawning
void SpawnPlayer(VRCPlayerApi player)
void SpawnLocalPlayer()
void ScheduleRespawn(VRCPlayerApi player, float delay)

// Spawn Point Queries
M2922_SpawnPoint[] GetTeamSpawnPoints(int teamIndex)
Transform GetRandomTeamSpawn(int teamIndex)

// Control
void SetTeamSpawnsActive(int teamIndex, bool active)
```

### M2922_SpawnPoint

```csharp
// Properties (read-only)
int TeamIndex { get; }
int Priority { get; }
bool IsActive { get; }
Vector3 Position { get; }
Quaternion Rotation { get; }

// Methods
void RecordSpawn()
float GetTimeSinceLastSpawn()
void SetActive(bool active)
void SetTeamIndex(int teamIndex)
```

---

## 🚀 Prochaines Étapes

- Implémenter la stratégie `Farthest` (spawn loin des ennemis)
- Implémenter la protection de spawn (invincibilité réelle)
- Ajouter des événements de spawn (OnPlayerSpawned, OnSpawnBlocked)
- Ajouter des zones de spawn (spawn area au lieu de point unique)
- Ajouter des spawns dynamiques (créer/détruire spawns runtime)

---

## 💡 Tips

1. **Performance**: `Auto Discover` est pratique mais lent. Pour production, assignez `Manual Spawn Points`
2. **Level Design**: Espacez les spawns d'au moins 3-5m pour éviter overlap
3. **Balance**: Plus de spawns = plus de variété, moins de spawn camping
4. **Direction**: Orientez les spawns face au centre de la map ou vers l'action
5. **Cooldown**: Ajustez selon gameplay (2s = rapide, 5s = spawn protection)
