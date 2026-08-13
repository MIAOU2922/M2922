using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
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
        private int _registryCount = 0;

        [Header("=== NPC REGISTRY ===")]
        private M2922.Component.Health.M2922_DamageReceiver[] _npcReceivers = new M2922.Component.Health.M2922_DamageReceiver[200];
        private int _npcCount = 0;

        // === METHODE ===
        protected override void Start()
        {
            base.Start();
            _activePlayers = new VRCPlayerApi[_maxPlayers];

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
            if (_npcCount >= 200) return -1;
            int id = _npcCount;
            _npcReceivers[_npcCount] = receiver;
            _npcCount++;
            return id;
        }

        public M2922.Component.Health.M2922_DamageReceiver GetNpcReceiverByEntityId(int entityId)
        {
            if (entityId < 0 || entityId >= _npcCount) return null;
            return _npcReceivers[entityId];
        }
    }
}
