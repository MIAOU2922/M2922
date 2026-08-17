# M2922 — Système d'inventaire

Portage du système "Pickup Inventory" de Vowgan vers les conventions M2922
(`M2922_Base` / `M2922_Tickable`, `AddComponentMenu`, debug `this.Log`, perf événementielle).

## Deux types d'inventaire

| Type | Classe | Accès | Sync | Menu |
|---|---|---|---|---|
| **Personnel** | `M2922_Inventory` | Seul le joueur voit SA liste | `None` (local) | Se téléporte devant le joueur |
| **Monde** (coffre partagé) | `M2922_WorldInventory` | Tous les joueurs voient/modifient le MÊME contenu | `Manual` (`[UdonSynced] StoredKeys`) | Reste en place sur le coffre |

## Fichiers

| Fichier | Rôle | Sync | Base |
|---|---|---|---|
| `M2922_Inventory.cs` | Inventaire PERSONNEL : liste, recherche, tri, spawn | `None` (local par joueur) | `M2922_Tickable` |
| `M2922_WorldInventory.cs` | Inventaire MONDE : coffre partagé synchronisé | `Manual` | `M2922_Inventory` |
| `M2922_InventoryItem.cs` | Item rangeable **local** | `None` | `M2922_Base` |
| `M2922_InventoryItemSynced.cs` | Item rangeable **synchronisé** | `Manual` | `M2922_InventoryItem` |
| `M2922_InventoryProxy.cs` | Relay Pickup/Drop sur le collider de l'objet | `None` | `M2922_Base` |
| `M2922_InventoryInserter.cs` | Zone de dépôt (trigger) | `None` | `M2922_Base` |
| `M2922_InventoryInserterUI.cs` | Zone de dépôt avec feedback couleur | `None` | `M2922_InventoryInserter` |
| `M2922_InventoryButtonUI.cs` | Bouton d'un item dans la liste | `None` | `M2922_Base` |
| `M2922_MapItemMenu.cs` | **Menu admin de la map** : inventaire de TOUS les items (comme le coffre) | `None` | `M2922_Inventory` |
| `M2922_InventoryItemGiver.cs` | Interactable qui **donne** des items (1×/monde) | `Manual` | `M2922_Base` |
| `M2922_InventoryItemRequester.cs` | Interactable qui **consomme** des items | `None` | `M2922_Base` |

## Setup pas à pas

### 1. Le menu (M2922_Inventory)
- Créer (ou reprendre) un GameObject "Inventory System" avec le canvas du menu.
- Ajouter `M2922_Inventory` (menu **M2922 → Inventory → Inventory**).
- Assigner : `ButtonPrefab`, `ButtonParent` (contenu ScrollView), `SpawnPoint`,
  `Inserter`, et toutes les références UI (`MenuContainer` = racine du menu,
  `SearchingField` (`TMP_InputField`), `SortingDropdown` (`TMP_Dropdown`),
  ItemIcon, ItemName, ItemDescription, SpawnButton).
- Lier les events Unity UI :
  - `SearchingField.onValueChanged` → `_SearchForItems`
  - `SortingDropdown.onValueChanged` → `_SetSorting`
  - `SpawnButton.onClick` → `_SpawnItem`
- Dropdown de tri (TMP) : **0 = Latest, 1 = AZ** (ordre de l'enum).

### 2. Le bouton d'item (M2922_InventoryButtonUI)
- Prefab "Item Button" : racine avec un `Button` Unity + une `Image` (icône) + un TMP (nom).
- Ajouter `M2922_InventoryButtonUI`, assigner ItemIcon + ItemName.
- Lier `Button.onClick` → `_OnClick`.

### 3. La zone d'insertion (M2922_InventoryInserterUI)
- GameObject avec un **Collider en Trigger** (pas de Rigidbody nécessaire sur
  la zone : le pickup déposé en a déjà un, un trigger statique suffit).
- Ajouter `M2922_InventoryInserterUI`, assigner `SpriteImage`, `IdleColor`, `DroppingColor`.
- Assigner cette zone à `Inventory.Inserter`.

### 4. Les objets rangeables
**Item local** (`M2922_InventoryItem`) :
- Sur le même GameObject que le `VRCPickup`.
- Assigner `Icon`, `ItemName`, `ItemDescription` (`Pickup` auto-détecté).
- Sur le **collider** du pickup : ajouter `M2922_InventoryProxy`.
  `Item` est auto-détecté (GetComponentInParent) et `Inventory` est assigné
  automatiquement par la zone d'insertion — **aucun réglage sur l'item**.

**Item synchronisé** (`M2922_InventoryItemSynced`) :
- Hiérarchie RECOMMANDÉE (encapsulation d'un pickup existant) :
  ```
  Root   → M2922_InventoryItemSynced (ce script, TOUJOURS actif)
    └─ Pickup → VRCPickup + VRCObjectSync + M2922_InventoryProxy (+ scripts du prop)
        └─ Contenu (mesh, colliders, renderers, particles, …)
  ```
- Le script vit sur la **racine** (qui reste active) et désactive/réactive le
  sous-arbre du `VRCPickup` : la sync `Active` continue de fonctionner
  (un UdonBehaviour désactivé ne reçoit plus `OnDeserialization`).
- Mode de sync **Manual** : AUCUN envoi par tick (maps 500-5000 items) —
  `Active`/`StoredInWorld` ne partent que sur `RequestSerialization()` (appelé
  par `_Spawn`/`_Hide`/`_SetWorldStored`). ⚠ Le SDK refuse `VRCObjectSync` +
  Udon **Manual** sur le MÊME GameObject → encapsulation OBLIGATOIRE
  (`VRCObjectSync` sur le pickup enfant, jamais sur la racine du script).
- Le `Pickup` est auto-détecté (y compris dans les enfants) ; le Proxy est placé
  sur le même GameObject que le `VRCPickup`.
- Le setup **à plat** (script sur le même GameObject que le pickup) n'est PLUS
  supporté (Error au Start) : l'encapsulation est obligatoire en Manual.

**Enregistrement automatique** : chaque `M2922_InventoryItem` s'enregistre
LUI-MÊME auprès du `M2922_Manager` au Start (retry si le Manager n'est pas encore
prêt). Les inventaires MONDE résolvent les clés via `Manager.GetInventoryItemByKey()`
→ aucun "tracking" manuel, tout item de la map peut aller dans n'importe quel inventaire.

### 5. Optionnel — quêtes
- **Giver** : interactable qui ajoute des items au joueur (une fois par monde).
- **Requester** : interactable qui consomme des items (ex. `"Pomme", "Pomme", "Pomme"`).

### 6. Inventaire MONDE (coffre partagé)
1. Créer le coffre : GameObject avec `M2922_WorldInventory` — **SANS VRCObjectSync**
   (interdit avec un Udon Manual : erreur de build SDK). Le coffre synchronise ses
   données via son `[UdonSynced]` Manual ; l'ownership se transfère via `SetOwner`
   (le comportement Manual est lui-même un objet réseau).
2. Même setup UI que l'inventaire personnel (boutons, champ, dropdown…).
   Le menu reste **sur place** (il ne suit pas le joueur) : positionner le canvas sur le coffre.
3. **Aucune liste d'items à remplir** : tout `M2922_InventoryItemSynced` de la map est
   automatiquement utilisable (enregistrement automatique auprès du `M2922_Manager`).
4. La zone d'insertion du coffre (`M2922_InventoryInserterUI`) référence le coffre dans
   son champ `Inventory` → elle transmet cette référence au proxy de l'objet au runtime.
5. La clé `Key` est **régénérée au runtime par instance** (`UseInstanceKey`, activé par défaut) :
   deux préfabs identiques posés dans la scène obtiennent automatiquement des clés
   différentes (chaîne d'indices de siblings, identique sur tous les clients).
   Décoche `UseInstanceKey` uniquement si tu as besoin d'une clé fixe (et rends-la
   unique par instance dans ce cas).

⚠ Règles coffre :
- Un item ne peut être rangé que dans **UN seul coffre** à la fois (gardé par `StoredInWorld`).
- Les items rangés ne réapparaissent PAS à la déconnexion de leur propriétaire (le coffre les gère).
- Le premier joueur qui range/retire devient "propriétaire" du coffre le temps de l'opération (relay + retry).

### 7. Menu admin de la map (M2922_MapItemMenu) — inventaire de TOUS les items

Fonctionne COMME un `M2922_WorldInventory` (menu sur place, zone d'insertion,
bouton Spawn, recherche, tri, détail, poids) mais sa liste contient TOUS les
items enregistrés auprès du `M2922_Manager` — pas seulement ceux d'un coffre.

1. Même setup UI que l'inventaire personnel/coffre (sections 1-3) :
   `ButtonPrefab` = bouton `M2922_InventoryButtonUI` (ajouter OPTIONNELLEMENT
   un TMP `ItemStatus` pour l'état ✔/✘), `ButtonParent`, `SpawnPoint`,
   `Inserter` (zone de dépôt), `MenuContainer` (racine du menu),
   `SearchingField`, `SortingDropdown`, `ItemIcon/Name/Description`, `SpawnButton`.
2. Ajouter `M2922_MapItemMenu` sur le GameObject du menu, assigner les
   références ci-dessus + optionnels : `AdminSpawner` (pour `_RestoreAll` /
   `_ResetWorld`) et `StatusText` (résumé "X / Y items dans le monde").
3. Le menu reste SUR PLACE : clic (Interact) pour ouvrir/fermer ; la touche `I`
   et le regard bas (VR) fonctionnent aussi.
4. La liste contient TOUS les items de la map : ✔ = dans le monde, ✘ = rangé.
   Ranger un objet (zone d'insertion) le masque ; le bouton Spawn le fait
   réapparaître au `SpawnPoint` (ou sur le menu). L'item reste dans la liste
   dans les deux cas.
5. Actions : `_RestoreAll` (respawn des items non visibles), `_ResetWorld`
   (reset complet), `_RebuildMenu` (reconstruction manuelle de la liste).

- L'état vient des items eux-mêmes (Active synchronisé) : aucun `[UdonSynced]`
  sur ce script. Statuts rafraîchis à l'ouverture, après chaque action, et
  ≈1×/sec tant que le menu est ouvert.
- Seuls les `M2922_InventoryItemSynced` peuvent être rangés.

## Utilisation en jeu
- **PC** : touche `I` pour ouvrir/fermer le menu (inventaire personnel).
- **VR** : regarder vers le bas pour ouvrir/fermer.
- **Coffre MONDE** : cliquer le coffre (Interact) ouvre/ferme son menu sur place.
- Ramasser un objet → le déposer dans la zone → l'objet est rangé.
- Dans le menu : cliquer un bouton pour le détail, **Spawn** pour le faire réapparaître.

### Spawner un item depuis le coffre SANS UI
- `_SpawnItemByKey("clé")` — spawn l'item par sa clé.
- `_SpawnItemByName("Nom")` — spawn le premier item portant ce nom.
- Appelables depuis n'importe quel script Udon/interactable de la scène.

### Où spawn l'item ?
- Si `SpawnPoint` est assigné → l'item apparaît à ce point.
- Sinon → il apparaît à la position du coffre.
- L'item ressort kinematic, visible pour tous, prêt à être ramassé.

## Poids des items
- Chaque item a un **`Weight`** (int, défaut 1).
- Chaque inventaire (perso ou coffre) a un **`MaxWeight`** (int). **-1 = pas de limite**.
- Si l'ajout dépasserait la limite, l'item n'est **PAS rangé** (warning console) :
  - inventaire perso : refus dans `_AddItem` ;
  - coffre MONDE : refus rapide local + check autoritaire côté propriétaire du coffre.
- `WeightText` (TMP optionnel) : affiche `12 / 50` ou `12 / ∞`.
- API : `_GetTotalWeight()`, `_CanStoreWeight(item)`, `_RefreshWeightText()`.
- Le Giver pré-vérifie la place avant de se consommer (réutilisable si plein).

## Notes réseau (leçons du projet M2922)
- L'inventaire est **local par joueur** (`BehaviourSyncMode.None`) : chacun a sa liste.
- Les items **synced** sont visibles par tous ; si le propriétaire quitte avec un item
  rangé, le **Master** le fait réapparaître.
- Toujours **recompiler les programmes Udon** (VRChat SDK) après ajout de ces scripts.
