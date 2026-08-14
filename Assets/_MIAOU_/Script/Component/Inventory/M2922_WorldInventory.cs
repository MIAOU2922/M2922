using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Inventaire MONDE : coffre / stockage PARTAGÉ placé dans la scène.
    /// Tous les joueurs voient et modifient le MÊME contenu.
    ///
    /// ⚠ SETUP RÉSEAU : ce GameObject ne DOIT PAS avoir de VRCObjectSync —
    /// le SDK interdit VRCObjectSync + UdonBehaviour MANUAL sur le même objet
    /// (erreur "Object Sync cannot share an object with a manually synchronized
    /// Udon Behaviour" au build). Le comportement Manual EST lui-même un objet
    /// réseau : Networking.SetOwner fonctionne directement dessus et synchronise
    /// StoredKeys. Le menu reste en place (il ne suit pas le joueur).
    ///
    /// Pourquoi MANUAL ? StoredKeys est une liste de chaînes mise à jour
    /// ponctuellement : Manual envoie la valeur EXACTE sur demande
    /// (RequestSerialization, ~280 Ko max). Continuous serait limité à ~200 octets
    /// par envoi et conçu pour des valeurs changeant en continu.
    ///
    /// ⚠ Items : seuls les M2922_InventoryItemSynced sont supportés
    /// (un item local ne synchronise pas son état avec les autres joueurs).
    /// Aucun "tracking" nécessaire : chaque item s'enregistre LUI-MÊME auprès du
    /// M2922_Manager au Start ; le coffre résout les clés via le Manager.
    /// Tout item synced de la map peut donc aller dans n'importe quel coffre.
    /// (1 item = 1 coffre à la fois.)
    ///
    /// Architecture :
    ///   - [UdonSynced] StoredKeys : clés des items rangés ("key1,key2").
    ///   - Le joueur qui range/retire un item prend l'ownership du coffre
    ///     (SetOwner), applique l'action, puis RequestSerialization.
    ///   - Tous les clients reconstruisent leur liste locale dans OnDeserialization.
    ///   - L'ownership de l'item est transférée avant de synchroniser son état
    ///     (Active / StoredInWorld) — avec retry tant que le transfert n'est pas effectif.
    ///
    /// PERF : événementiel + deserialization (aucun travail par frame en plus
    /// du Update hérité : touche I + check de distance).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/World Inventory")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_WorldInventory : M2922_Inventory
    {
        // Aucun tableau d'items à assigner : chaque M2922_InventoryItem s'enregistre
        // lui-même auprès du M2922_Manager au Start. Les clés sont résolues via
        // Manager.GetInventoryItemByKey().

        [Header("=== ÉTAT SYNCHRONISÉ ===")]
        [Tooltip("Clés des items rangés, séparées par des virgules.")]
        [UdonSynced] public string StoredKeys = "";

        private const int MAX_RETRIES = 5;
        private string _pendingAction = "";
        private int _pendingActionRetries = 0;
        private string _pendingItemKey = "";
        private int _pendingItemRetries = 0;

        /// <summary>Le menu du coffre reste à sa place dans le monde (il ne suit pas le joueur).</summary>
        protected override bool _MenuFollowsPlayer()
        {
            return false;
        }

        // ============================================================
        // POINTS D'ENTRÉE (appelés par Proxy / Giver / UI)
        // ============================================================

        public override void _AddItem(M2922_InventoryItem item)
        {
            if (item == null) return;

            if (!item._IsNetworked())
            {
                this.Warning($"[WorldInventory] '{item.ItemName}' est LOCAL : seuls les items synchronisés peuvent être partagés.");
                return;
            }

            // Refus rapide si le coffre est déjà plein (le check autoritaire est dans _ApplyStore).
            if (MaxWeight >= 0 && _GetTotalWeight() + item.Weight > MaxWeight)
            {
                this.Warning($"[WorldInventory] Coffre plein ({_GetTotalWeight()}/{MaxWeight}) : '{item.ItemName}' refusé.");
                return;
            }

            // SELF-HEAL : si l'item n'a pas pu s'enregistrer au Start (Manager pas
            // encore prêt à ce moment-là), on l'enregistre maintenant — on a ici
            // la référence directe via le proxy.
            if (Manager != null && Manager.GetInventoryItemByKey(item.Key) == null)
                Manager.RegisterInventoryItem(item);

            if (Inserter != null)
                Inserter._Highlight(false);

            _RequestAction("ADD:" + item.Key);
        }

        public override void _SpawnItem()
        {
            if (ItemList.Count == 0) return;
            if (_selectedItem == null) return;

            M2922_InventoryItem item = (M2922_InventoryItem)_selectedItem[ID_ITEM].Reference;
            _RequestAction("REMOVE:" + item.Key);
        }

        /// <summary>Cliquer le coffre (interactable VRC) ouvre/ferme son menu.</summary>
        public override void Interact()
        {
            _ToggleMenu();
        }

        /// <summary>Spawn un item du coffre par sa CLÉ (Key) — sans passer par l'UI.</summary>
        public void _SpawnItemByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_IsKeyStored(key)) return;

            _RequestAction("REMOVE:" + key);
        }

        /// <summary>Spawn le premier item du coffre portant ce NOM — sans passer par l'UI.</summary>
        public void _SpawnItemByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return;

            string key = _FindKeyByName(itemName);
            if (string.IsNullOrEmpty(key))
            {
                this.Warning($"[WorldInventory] Aucun item '{itemName}' rangé dans ce coffre.");
                return;
            }

            _RequestAction("REMOVE:" + key);
        }

        private string _FindKeyByName(string itemName)
        {
            if (string.IsNullOrEmpty(StoredKeys)) return "";

            string[] keys = StoredKeys.Split(',');
            for (int i = 0; i < keys.Length; i++)
            {
                if (Manager == null) break;

                M2922_InventoryItem item = Manager.GetInventoryItemByKey(keys[i]);
                if (item != null && item.ItemName == itemName) return keys[i];
            }
            return "";
        }

        // ============================================================
        // RELAY D'ACTIONS (ownership du coffre)
        // ============================================================

        private void _RequestAction(string action)
        {
            if (Networking.IsOwner(gameObject))
            {
                _ApplyAction(action);
                return;
            }

            // On met l'action en attente et on demande l'ownership du coffre.
            _pendingAction = string.IsNullOrEmpty(_pendingAction) ? action : _pendingAction + "|" + action;
            _pendingActionRetries = 0;
            Networking.SetOwner(_localPlayer, gameObject);
            SendCustomEventDelayedSeconds("_RetryPendingAction", 2f);
        }

        /// <summary>Retry (appelé par SendCustomEventDelayedSeconds) : re-tente l'ownership puis applique.</summary>
        public void _RetryPendingAction()
        {
            if (string.IsNullOrEmpty(_pendingAction)) return;

            if (Networking.IsOwner(gameObject))
            {
                _FlushPendingActions();
                return;
            }

            _pendingActionRetries++;
            if (_pendingActionRetries > MAX_RETRIES)
            {
                this.Warning($"[WorldInventory] Action abandonnée (ownership impossible) : {_pendingAction}");
                _pendingAction = "";
                _pendingActionRetries = 0;
                return;
            }

            Networking.SetOwner(_localPlayer, gameObject);
            SendCustomEventDelayedSeconds("_RetryPendingAction", 2f);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            // Le coffre vient de NOUS être transféré : on applique les actions en attente.
            if (!player.isLocal) return;

            if (!string.IsNullOrEmpty(_pendingAction))
                _FlushPendingActions();
        }

        private void _FlushPendingActions()
        {
            string pending = _pendingAction;
            _pendingAction = "";
            _pendingActionRetries = 0;

            string[] actions = pending.Split('|');
            for (int i = 0; i < actions.Length; i++)
            {
                _ApplyAction(actions[i]);
            }
        }

        private void _ApplyAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return;

            if (action.StartsWith("ADD:"))
            {
                _ApplyStore(action.Substring(4));
            }
            else if (action.StartsWith("REMOVE:"))
            {
                _ApplyRemove(action.Substring(7));
            }
        }

        // ============================================================
        // APPLY (le coffre NOUS appartient ici)
        // ============================================================

        private void _ApplyStore(string key)
        {
            M2922_InventoryItem item = _FindItemByKey(key);
            if (item == null)
            {
                this.Warning($"[WorldInventory] Item inconnu (clé {key}) — détruit ou non enregistré auprès du Manager.");
                return;
            }
            if (_IsKeyStored(key)) return;

            // Anti-doublon : un item déjà rangé dans un AUTRE coffre est refusé.
            if (item._IsWorldStored())
            {
                this.Warning($"[WorldInventory] '{item.ItemName}' est déjà rangé dans un autre inventaire monde.");
                return;
            }

            // Limite de poids (autoritaire : seul le propriétaire du coffre applique).
            if (MaxWeight >= 0 && _GetTotalWeight() + item.Weight > MaxWeight)
            {
                this.Warning($"[WorldInventory] Coffre plein ({_GetTotalWeight()}/{MaxWeight}) : '{item.ItemName}' refusé.");
                return;
            }

            // 1. Liste synchronisée du coffre.
            StoredKeys = string.IsNullOrEmpty(StoredKeys) ? key : StoredKeys + "," + key;
            RequestSerialization();

            // 2. Item : on prend l'ownership pour pouvoir synchroniser Active / StoredInWorld.
            Networking.SetOwner(_localPlayer, item.gameObject);
            _pendingItemKey = key;
            _pendingItemRetries = 0;
            _TryApplyItemHide();

            _RebuildFromStoredKeys();
        }

        private void _ApplyRemove(string key)
        {
            M2922_InventoryItem item = _FindItemByKey(key);
            if (item == null) return;
            if (!_IsKeyStored(key)) return;

            // 1. Retire la clé de la liste synchronisée.
            StoredKeys = _RemoveKey(StoredKeys, key);
            RequestSerialization();

            // 2. Item : ownership nécessaire pour synchroniser le spawn.
            Networking.SetOwner(_localPlayer, item.gameObject);
            _pendingItemKey = key;
            _pendingItemRetries = 0;
            _TryApplyItemSpawn();

            _RebuildFromStoredKeys();
        }

        /// <summary>Cache l'item rangé une fois qu'on possède l'item (retry auto).</summary>
        public void _TryApplyItemHide()
        {
            if (string.IsNullOrEmpty(_pendingItemKey)) return;

            M2922_InventoryItem item = _FindItemByKey(_pendingItemKey);
            if (item == null)
            {
                _pendingItemKey = "";
                return;
            }

            if (Networking.IsOwner(item.gameObject))
            {
                _pendingItemKey = "";
                item._SetWorldStored(true);
                item._Hide();
            }
            else
            {
                _pendingItemRetries++;
                if (_pendingItemRetries > MAX_RETRIES)
                {
                    _pendingItemKey = "";
                    this.Warning("[WorldInventory] Ownership de l'item impossible, rangement annulé.");
                    return;
                }
                Networking.SetOwner(_localPlayer, item.gameObject);
                SendCustomEventDelayedSeconds("_TryApplyItemHide", 1f);
            }
        }

        /// <summary>Fait réapparaître l'item une fois qu'on possède l'item (retry auto).</summary>
        public void _TryApplyItemSpawn()
        {
            if (string.IsNullOrEmpty(_pendingItemKey)) return;

            M2922_InventoryItem item = _FindItemByKey(_pendingItemKey);
            if (item == null)
            {
                _pendingItemKey = "";
                return;
            }

            if (Networking.IsOwner(item.gameObject))
            {
                _pendingItemKey = "";
                item._SetWorldStored(false);
                item._Spawn(SpawnPoint != null ? SpawnPoint : transform);
            }
            else
            {
                _pendingItemRetries++;
                if (_pendingItemRetries > MAX_RETRIES)
                {
                    _pendingItemKey = "";
                    this.Warning("[WorldInventory] Ownership de l'item impossible, spawn annulé.");
                    return;
                }
                Networking.SetOwner(_localPlayer, item.gameObject);
                SendCustomEventDelayedSeconds("_TryApplyItemSpawn", 1f);
            }
        }

        // ============================================================
        // SYNC / REBUILD LOCAL
        // ============================================================

        public override void OnDeserialization()
        {
            _RebuildFromStoredKeys();
        }

        private void _RebuildFromStoredKeys()
        {
            // Conserver la clé sélectionnée si possible.
            string selectedKey = "";
            if (_selectedItem != null)
                selectedKey = ((M2922_InventoryItem)_selectedItem[ID_ITEM].Reference).Key;

            // Nettoyer la liste locale (boutons + données).
            for (int i = 0; i < ItemList.Count; i++)
            {
                GameObject button = (GameObject)ItemList[i].DataDictionary[ID_BUTTON].Reference;
                if (button != null) Destroy(button);
            }
            ItemList.Clear();
            ClearMenu();

            if (string.IsNullOrEmpty(StoredKeys)) return;

            string[] keys = StoredKeys.Split(',');
            for (int i = 0; i < keys.Length; i++)
            {
                M2922_InventoryItem item = _FindItemByKey(keys[i]);
                if (item == null) continue;

                GameObject buttonObj = Instantiate(ButtonPrefab, ButtonParent);
                buttonObj.name = $"{item.name} Button";

                DataDictionary itemDictionary = new DataDictionary();
                itemDictionary[ID_BUTTON] = buttonObj;
                itemDictionary[ID_ITEM] = item;
                ItemList.Add(itemDictionary);

                M2922_InventoryButtonUI button = buttonObj.GetComponent<M2922_InventoryButtonUI>();
                if (button != null) button._Init(this, itemDictionary);
            }

            _SortList();

            // Resélectionner l'item précédent, sinon le premier.
            _SelectStoredKey(selectedKey);
            _RefreshWeightText();
        }

        private void _SelectStoredKey(string key)
        {
            if (ItemList.Count == 0) return;

            for (int i = 0; i < ItemList.Count; i++)
            {
                M2922_InventoryItem item = (M2922_InventoryItem)ItemList[i].DataDictionary[ID_ITEM].Reference;
                if (item.Key == key)
                {
                    _SelectItem(ItemList[i].DataDictionary);
                    return;
                }
            }

            // Item précédent disparu : sélectionner le premier.
            _SelectItem(ItemList[0].DataDictionary);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        /// <summary>
        /// Résout une clé d'item via le registre du Manager (items auto-enregistrés au Start).
        /// </summary>
        private M2922_InventoryItem _FindItemByKey(string key)
        {
            if (Manager == null) return null;
            return Manager.GetInventoryItemByKey(key);
        }

        private bool _IsKeyStored(string key)
        {
            return StoredKeys == key || StoredKeys.Contains(key + ",") || StoredKeys.Contains("," + key);
        }

        private string _RemoveKey(string keys, string key)
        {
            string result = "";
            string[] split = keys.Split(',');
            for (int i = 0; i < split.Length; i++)
            {
                if (split[i] == key) continue;
                result = string.IsNullOrEmpty(result) ? split[i] : result + "," + split[i];
            }
            return result;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Items", ItemList.Count.ToString()),
                new M2922_GizmoDisplayInfo("Poids", MaxWeight < 0 ? $"{_GetTotalWeight()} / ∞" : $"{_GetTotalWeight()} / {MaxWeight}"),
                new M2922_GizmoDisplayInfo("Clés", string.IsNullOrEmpty(StoredKeys) ? "-" : StoredKeys),
            };
        }
#endif
    }
}
