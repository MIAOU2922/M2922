# 🎯 Guide Friendly Fire - Système de Dégâts Réduits

## Vue d'ensemble

Le système M2922 supporte maintenant le **Friendly Fire avec dégâts configurables**. Vous pouvez régler le pourcentage de dégâts infligés aux alliés de 0% (aucun dégât) à 100% (dégâts complets).

---

## ⚙️ Configuration dans le Setup Wizard

### Via M2922_ProjectSetup

1. Ouvrir le wizard: `M2922 > Setup > Project Setup Wizard`
2. Activer **"Système d'Équipes"**
3. Cocher **"Friendly Fire autorisé"**
4. Ajuster le slider **"% Dégâts FF"** (0-100%)
   - **0%** = Aucun dégât aux alliés (FF bloqué)
   - **50%** = Moitié des dégâts normaux
   - **100%** = Dégâts complets (comme si c'était un ennemi)

La configuration sera automatiquement appliquée au TeamManager lors de la création.

---

## 🔧 Configuration Manuelle

### Dans l'Inspecteur Unity

Sélectionner le `TeamManager` GameObject:

```
Game Settings:
├── Autoriser le friendly fire: ☑️
└── Friendly Fire Damage Multiplier: 0.5 (slider 0-1)
```

- **0.0** = Aucun dégât
- **0.5** = 50% des dégâts
- **1.0** = 100% des dégâts

---

## 💻 Utilisation dans le Code

### Méthode 1: Utiliser GetDamageMultiplier (Recommandé)

```csharp
using M2922.Teams;

public class MyWeapon : UdonSharpBehaviour
{
    [SerializeField] private M2922_TeamManager _teamManager;
    
    public void DealDamage(int attackerId, int victimId, float baseDamage)
    {
        // Obtenir le multiplicateur de dégâts
        float multiplier = _teamManager.GetDamageMultiplier(attackerId, victimId);
        
        // multiplier = 0.0 si FF désactivé et alliés
        // multiplier = 0.0-1.0 si FF activé et alliés (ex: 0.5 = 50%)
        // multiplier = 1.0 pour ennemis ou joueurs différents
        
        if (multiplier == 0f)
        {
            Debug.Log("Friendly fire blocked!");
            return;
        }
        
        float finalDamage = baseDamage * multiplier;
        
        // Log si dégâts réduits
        if (multiplier < 1f)
        {
            Debug.Log($"Friendly fire: {baseDamage} -> {finalDamage} ({multiplier * 100}%)");
        }
        
        // Appliquer les dégâts
        ApplyDamageToPlayer(victimId, finalDamage);
    }
}
```

### Méthode 2: CanDamage (Legacy - Boolean)

Si vous voulez juste bloquer complètement le FF:

```csharp
if (!_teamManager.CanDamage(attackerId, victimId))
{
    Debug.Log("Cannot damage ally!");
    return;
}

ApplyDamageToPlayer(victimId, damage);
```

**Note:** Cette méthode ne supporte PAS les dégâts réduits, seulement tout ou rien.

---

## 📋 Exemple Complet: Arme avec FF

```csharp
using UdonSharp;
using UnityEngine;
using M2922.Core;
using M2922.Teams;

public class M2922_ExampleWeapon : M2922_Base
{
    [Header("References")]
    [SerializeField] private M2922_TeamManager _teamManager;
    
    [Header("Weapon Settings")]
    [SerializeField] private float _baseDamage = 25f;
    [SerializeField] private float _headshotMultiplier = 2f;
    
    private int _localPlayerId;
    
    protected override void Start()
    {
        base.Start();
        
        if (_teamManager == null)
        {
            _teamManager = GameObject.FindObjectOfType<M2922_TeamManager>();
        }
        
        // Get local player ID
        var localPlayer = Networking.LocalPlayer;
        if (localPlayer != null)
        {
            _localPlayerId = localPlayer.playerId;
        }
    }
    
    public void Fire(RaycastHit hit)
    {
        // Trouver le joueur touché
        var playerController = hit.collider.GetComponentInParent<M2922_PlayerController>();
        if (playerController == null) return;
        
        int victimId = playerController.PlayerId;
        float damage = _baseDamage;
        
        // Headshot detection
        if (hit.collider.name.Contains("Head"))
        {
            damage *= _headshotMultiplier;
            this.Log("HEADSHOT!");
        }
        
        // Appliquer multiplicateur FF (si TeamManager existe)
        if (_teamManager != null)
        {
            float damageMultiplier = _teamManager.GetDamageMultiplier(_localPlayerId, victimId);
            
            if (damageMultiplier == 0f)
            {
                this.VerboseLog("Friendly fire blocked - same team");
                // Optionnel: Afficher feedback visuel "Can't shoot allies!"
                return;
            }
            
            damage *= damageMultiplier;
            
            // Log pour debug
            if (damageMultiplier < 1f)
            {
                this.Log($"Friendly fire hit: {damageMultiplier * 100}% damage");
            }
        }
        
        // Appliquer les dégâts
        playerController.TakeDamage(damage, _localPlayerId);
        
        this.Log($"Hit player {victimId} for {damage} damage");
    }
}
```

---

## 🎮 Cas d'Usage Typiques

### Hardcore (FF complet)
```
Friendly Fire: ☑️
FF Damage Multiplier: 1.0 (100%)
```
- Les alliés prennent 100% des dégâts
- Requis coordination maximale
- Mode réaliste/hardcore

### Balanced (FF réduit)
```
Friendly Fire: ☑️
FF Damage Multiplier: 0.5 (50%)
```
- Les alliés prennent 50% des dégâts
- Pénalité pour mauvais tir, mais pas punitive
- Bon compromis pour gameplay compétitif

### Casual (FF minimal)
```
Friendly Fire: ☑️
FF Damage Multiplier: 0.1 (10%)
```
- Les alliés prennent seulement 10% des dégâts
- Presque pas de pénalité
- Pour parties casual/fun

### No FF (FF désactivé)
```
Friendly Fire: ☐
FF Damage Multiplier: N/A
```
- Les alliés ne prennent AUCUN dégât
- Mode arcade/casual
- Simplifie le gameplay

---

## ⚠️ Notes Importantes

### Comportement du Multiplicateur

La méthode `GetDamageMultiplier(attackerId, victimId)` retourne:

| Situation | Multiplicateur | Dégâts |
|-----------|---------------|---------|
| Attaque sur ennemi | `1.0` | 100% (normaux) |
| Attaque sur soi-même | `1.0` | 100% (suicide possible) |
| FF désactivé + allié | `0.0` | 0% (bloqué) |
| FF activé + allié | `0.0-1.0` | Selon config (ex: 0.5 = 50%) |
| Pas de TeamManager (FFA) | `1.0` | 100% (tout le monde est ennemi) |

### Mode FFA (Sans TeamManager)

En mode Free-for-All:
- `GetDamageMultiplier()` n'est pas disponible (TeamManager absent)
- Tous les joueurs retournent multiplicateur 1.0 (ennemis)
- Gérez directement les dégâts sans vérification FF

### Compatibilité avec l'Exemple PlayerController

Le `M2922_PlayerController` inclus utilise automatiquement `GetDamageMultiplier()`:

```csharp
// Dans M2922_PlayerController.cs
public void TakeDamage(float damage, int attackerId)
{
    float multiplier = _teamManager.GetDamageMultiplier(attackerId, _playerId);
    float finalDamage = damage * multiplier;
    
    // ... applique finalDamage
}
```

Vous pouvez utiliser ce pattern dans vos propres scripts!

---

## 🔄 Migration depuis l'Ancienne API

Si vous utilisiez `CanDamage()`:

### Ancien code (Boolean)
```csharp
if (!teamManager.CanDamage(attackerId, victimId))
{
    return; // Bloque complètement
}
ApplyDamage(damage);
```

### Nouveau code (Multiplicateur)
```csharp
float multiplier = teamManager.GetDamageMultiplier(attackerId, victimId);
if (multiplier == 0f) return; // Bloqué

float finalDamage = damage * multiplier;
ApplyDamage(finalDamage);
```

**Note:** `CanDamage()` reste disponible pour compatibilité, mais ne supporte pas les dégâts réduits.

---

## 📚 Références

- **TeamManager API**: Voir `README.md` section Teams Module
- **PlayerController Example**: `Assets/_MIAOU_/Script/Player/M2922_PlayerController.cs`
- **Setup Wizard**: `M2922 > Setup > Project Setup Wizard`
