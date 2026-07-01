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
    public class M2922_Manager : M2922_Base
    {
        [Header("=== PLAYER TRACKING ===")]
        [UdonSynced] public int PlayerCount = 0;
        private VRCPlayerApi[] _activePlayers;
        private int _maxPlayers = 80; // VRChat max
        // Private
        private bool _isHost = false;
        [UdonSynced] private string _hostPlayerName = "";

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
        protected override void Update()
        {
            base.Update();
            // todo
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
    }
}
