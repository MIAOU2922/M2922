using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace M2922.Core
{

    [AddComponentMenu("M2922/Core/Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_Manager : M2922_Tickable
    {
        [Header("=== PLAYER TRACKING ===")]
        [UdonSynced] public int PlayerCount = 0;
        private VRCPlayerApi[] _activePlayers;
        private int _maxPlayers = 80; // VRChat max
        // Private
        private bool _isHost = false;
        [UdonSynced] private string _hostPlayerName = "";

        [Header("=== DAMAGE RECEIVER REGISTRY ===")]
        private int[] _registeredPlayerIDs = new int[80];
        private M2922.Component.Health.M2922_DamageReceiver[] _registeredReceivers = new M2922.Component.Health.M2922_DamageReceiver[80];
        [SerializeField] private int _registryCount = 0;

        [Header("=== NPC REGISTRY ===")]
        [Tooltip("Taille du registre NPC. Renseignée automatiquement au build selon le nombre de NPCs présents dans la scène.")]
        [SerializeField] private int _npcMax = 200;
        private M2922.Component.Health.M2922_DamageReceiver[] _npcReceivers;
        [SerializeField] private int _npcCount = 0;

        [Header("=== INVENTORY ITEM REGISTRY ===")]
        [Tooltip("Taille du registre d'items. Renseignée automatiquement au build selon le nombre d'items présents dans la scène.")]
        [SerializeField] private int _inventoryItemMax = 500;
        private M2922.Component.Inventory.M2922_InventoryItem[] _inventoryItems;
        [SerializeField] private int _inventoryItemCount = 0;

        // === METHODE ===
        protected override void Start()
        {
            base.Start();
            _activePlayers = new VRCPlayerApi[_maxPlayers];

            // Tailles de registres ajustées au build (avec minimum de sécurité).
            if (_npcMax < 8) _npcMax = 8;
            if (_inventoryItemMax < 8) _inventoryItemMax = 8;
            _npcReceivers = new M2922.Component.Health.M2922_DamageReceiver[_npcMax];
            _inventoryItems = new M2922.Component.Inventory.M2922_InventoryItem[_inventoryItemMax];

            // Vérifier si on est le host
            _isHost = Networking.LocalPlayer != null && Networking.LocalPlayer.isMaster;
            _hostPlayerName = Networking.Master.displayName;

            this.Log($"M2922 Core Manager initialized.");
            this.Log($"Is Host: {_isHost}");
            this.Log($"Host Player Name: {_hostPlayerName}");
            this.Log($"Player Count: {PlayerCount}");
        }
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            this.Log($"Player joined: {player.displayName}");
            if (_isHost)
            {
                UpdatePlayerCount();
                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                }
            }
        }
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            this.Log($"Player left: {player.displayName}");
            if (_isHost)
            {
                UpdatePlayerCount();
                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                }
            }
        }
        public void UpdatePlayerCount()
        {
            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);
            PlayerCount = players.Length;
        }
        public bool IsHost()
        {
            return _isHost;
        }

        // ===================================================
        // DAMAGE RECEIVER REGISTRY
        // ===================================================

        public void RegisterReceiver(int playerID, M2922.Component.Health.M2922_DamageReceiver receiver)
        {
            if (_registryCount >= 80) return;
            _registeredPlayerIDs[_registryCount] = playerID;
            _registeredReceivers[_registryCount] = receiver;
            _registryCount++;
        }

        public void UnregisterReceiver(int playerID)
        {
            for (int i = 0; i < _registryCount; i++)
            {
                if (_registeredPlayerIDs[i] == playerID)
                {
                    _registryCount--;
                    _registeredPlayerIDs[i] = _registeredPlayerIDs[_registryCount];
                    _registeredReceivers[i] = _registeredReceivers[_registryCount];
                    return;
                }
            }
        }

        public M2922.Component.Health.M2922_DamageReceiver GetReceiverByPlayerID(int playerID)
        {
            for (int i = 0; i < _registryCount; i++)
            {
                if (_registeredPlayerIDs[i] == playerID)
                    return _registeredReceivers[i];
            }
            return null;
        }

        // ===================================================
        // NPC REGISTRY (pour le relai de dégâts réseau)
        // ===================================================

        /// <summary>Enregistre un NPC/destructible et retourne son entityId.</summary>
        public int RegisterNpc(M2922.Component.Health.M2922_DamageReceiver receiver)
        {
            if (_npcReceivers == null)
                _npcReceivers = new M2922.Component.Health.M2922_DamageReceiver[_npcMax];
            if (_npcCount >= _npcMax) return -1;
            int id = _npcCount;
            _npcReceivers[_npcCount] = receiver;
            _npcCount++;
            return id;
        }

        public M2922.Component.Health.M2922_DamageReceiver GetNpcReceiverByEntityId(int entityId)
        {
            if (_npcReceivers == null || entityId < 0 || entityId >= _npcCount) return null;
            return _npcReceivers[entityId];
        }

        // ===================================================
        // INVENTORY ITEM REGISTRY (résolution clé → item)
        // Chaque M2922_InventoryItem s'enregistre LUI-MÊME au Start :
        // les inventaires MONDE n'ont plus besoin de "tracker" les items.
        // ===================================================


        /// <summary>Enregistre un item d'inventaire (appelé par l'item lui-même dès que le Manager est prêt).</summary>
        public void RegisterInventoryItem(M2922.Component.Inventory.M2922_InventoryItem item)
        {
            if (item == null) return;

            if (_inventoryItems == null)
                _inventoryItems = new M2922.Component.Inventory.M2922_InventoryItem[_inventoryItemMax];

            // Anti-doublon (OnManagerReady peut être rappelé, ou self-heal du coffre).
            for (int i = 0; i < _inventoryItemCount; i++)
            {
                if (_inventoryItems[i] == item) return;
            }

            // Clé vide ou DOUBLON (préfabs identiques qui partagent la clé du
            // préfab asset) : régénération d'une clé d'instance DÉTERMINISTE —
            // la même sur tous les clients, donc compatible avec StoredKeys synced.
            if (string.IsNullOrEmpty(item.Key) || GetInventoryItemByKey(item.Key) != null)
            {
                this.Warning($"[Manager] Clé dupliquée/vide pour '{item.ItemName}' (clé '{item.Key}') → clé d'instance régénérée.");
                item._RegenerateKey();
            }

            // Toujours en conflit (ne devrait pas arriver) : on ignore l'item.
            if (string.IsNullOrEmpty(item.Key) || GetInventoryItemByKey(item.Key) != null)
            {
                this.Error($"[Manager] Item '{item.ItemName}' ignoré : clé toujours dupliquée.");
                return;
            }

            if (_inventoryItemCount >= _inventoryItemMax) return;

            _inventoryItems[_inventoryItemCount] = item;
            _inventoryItemCount++;

            this.VerboseLog($"[Manager] Item d'inventaire enregistré : {item.ItemName} (clé {item.Key}, total {_inventoryItemCount})");
        }

        /// <summary>Nombre d'items d'inventaire enregistrés.</summary>
        public int GetInventoryItemCount()
        {
            return _inventoryItemCount;
        }

        /// <summary>Retourne l'item d'inventaire à l'index donné (null si hors bornes).</summary>
        public M2922.Component.Inventory.M2922_InventoryItem GetInventoryItemAt(int index)
        {
            if (_inventoryItems == null || index < 0 || index >= _inventoryItemCount) return null;
            return _inventoryItems[index];
        }

        /// <summary>Retrouve un item d'inventaire par sa clé (Key), null si introuvable.</summary>
        public M2922.Component.Inventory.M2922_InventoryItem GetInventoryItemByKey(string key)
        {
            if (string.IsNullOrEmpty(key) || _inventoryItems == null) return null;

            for (int i = 0; i < _inventoryItemCount; i++)
            {
                M2922.Component.Inventory.M2922_InventoryItem item = _inventoryItems[i];
                if (item != null && Utilities.IsValid(item.gameObject) && item.Key == key)
                    return item;
            }
            return null;
        }
    }
}
