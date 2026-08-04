using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.UI
{
    /// <summary>
    /// Manager central de la liste des joueurs "staff" (autorisés).
    /// 
    /// RÈGLES :
    /// - Seul le Master peut modifier la liste (Add/Remove/Clear).
    /// - La liste est synced via [UdonSynced].
    /// - Chaque UI joueur (M2922_PlayerUIVisibility) interroge ce manager.
    /// 
    /// SETUP :
    /// 1. Mettre ce script sur un GameObject persistant dans la scène.
    /// 2. Référencer ce manager dans chaque M2922_PlayerUIVisibility.
    /// </summary>
    [AddComponentMenu("M2922/UI/Visibility By Role")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_VisibilityByRole : M2922_Base
    {
        [Header("=== ALLOWED PLAYERS (synced) ===")]
        [Tooltip("Liste des displayNames autorisés, séparés par | (synced).")]
        [UdonSynced] private string _allowedPlayersSerialized = "";

        [Tooltip("Version incrémentée à chaque modif. Les PlayerUIVisibility pollent ce champ.")]
        [UdonSynced] private int _version = 0;

        // Cache local (non-synced)
        [SerializeField]private string[] _allowedPlayers = new string[0];
        private VRCPlayerApi _localPlayer;

        // =============================================
        //  UNITY LIFECYCLE
        // =============================================

        protected override void Start()
        {
            base.Start();

            _localPlayer = Networking.LocalPlayer;
            DeserializeAllowedPlayers();

            if (_localPlayer == null) return;

            if (_localPlayer.isMaster && !Networking.IsOwner(_localPlayer, gameObject))
                Networking.SetOwner(_localPlayer, gameObject);
        }

        public override void OnDeserialization()
        {
            DeserializeAllowedPlayers();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            // Ne bumper la version que si on est owner, pour forcer les
            // PlayerUIVisibility et PlayerListUI à rafraîchir leur état.
            if (_localPlayer == null) _localPlayer = Networking.LocalPlayer;
            if (_localPlayer != null && Networking.IsOwner(_localPlayer, gameObject))
                BumpVersion();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            // Un joueur est parti : le Master a peut-être changé.
            // On bump la version pour que tous les PlayerUIVisibility
            // réévaluent leur visibilité (le nouveau Master doit cacher son UI).
            if (_localPlayer == null) _localPlayer = Networking.LocalPlayer;
            if (_localPlayer != null && Networking.IsOwner(_localPlayer, gameObject))
                BumpVersion();
        }

        // =============================================
        //  PRIVATE — Serialization
        // =============================================

        private void DeserializeAllowedPlayers()
        {
            if (string.IsNullOrEmpty(_allowedPlayersSerialized))
            {
                _allowedPlayers = new string[0];
                return;
            }
            _allowedPlayers = _allowedPlayersSerialized.Split('|');
        }

        private void SerializeAllowedPlayers()
        {
            if (_allowedPlayers == null || _allowedPlayers.Length == 0)
            {
                _allowedPlayersSerialized = "";
                return;
            }
            string result = _allowedPlayers[0];
            for (int i = 1; i < _allowedPlayers.Length; i++)
                result += "|" + _allowedPlayers[i];
            _allowedPlayersSerialized = result;
        }

        // =============================================
        //  PUBLIC — Consultation (appelé par M2922_PlayerUIVisibility)
        // =============================================

        public int GetVersion() => _version;

        public bool IsLocalPlayerAllowed()
        {
            if (_localPlayer == null)
                _localPlayer = Networking.LocalPlayer;
            if (_localPlayer == null) return false;
            if (_localPlayer.isMaster) return true;
            return IsPlayerAllowedByName(_localPlayer.displayName);
        }

        public bool IsPlayerAllowed(VRCPlayerApi player)
        {
            if (player == null) return false;
            if (player.isMaster) return true;
            return IsPlayerAllowedByName(player.displayName);
        }

        public bool IsPlayerAllowedByName(string displayName)
        {
            if (_allowedPlayers == null || _allowedPlayers.Length == 0) return false;
            if (string.IsNullOrEmpty(displayName)) return false;

            for (int i = 0; i < _allowedPlayers.Length; i++)
            {
                if (_allowedPlayers[i].Trim() == displayName.Trim())
                    return true;
            }
            return false;
        }

        // =============================================
        //  PUBLIC — Modification (Master only)
        // =============================================

        public void AddPlayer(string displayName)
        {
            if (!EnsureMasterOwnership()) return;
            if (IsPlayerAllowedByName(displayName)) return;

            string[] newArr = new string[_allowedPlayers.Length + 1];
            for (int i = 0; i < _allowedPlayers.Length; i++)
                newArr[i] = _allowedPlayers[i];
            newArr[_allowedPlayers.Length] = displayName;
            _allowedPlayers = newArr;

            SerializeAllowedPlayers();
            BumpVersion();
        }

        public void RemovePlayer(string displayName)
        {
            if (!EnsureMasterOwnership()) return;

            int count = 0;
            for (int i = 0; i < _allowedPlayers.Length; i++)
            {
                if (_allowedPlayers[i].Trim() != displayName.Trim())
                    count++;
            }

            string[] newArr = new string[count];
            int idx = 0;
            for (int i = 0; i < _allowedPlayers.Length; i++)
            {
                if (_allowedPlayers[i].Trim() != displayName.Trim())
                {
                    newArr[idx] = _allowedPlayers[i];
                    idx++;
                }
            }
            _allowedPlayers = newArr;

            SerializeAllowedPlayers();
            BumpVersion();
        }

        public void ClearList()
        {
            if (!EnsureMasterOwnership()) return;
            _allowedPlayers = new string[0];
            SerializeAllowedPlayers();
            BumpVersion();
        }

        public void AddLocalPlayer()
        {
            if (_localPlayer == null) return;
            AddPlayer(_localPlayer.displayName);
        }

        public void RemoveLocalPlayer()
        {
            if (_localPlayer == null) return;
            RemovePlayer(_localPlayer.displayName);
        }

        // =============================================
        //  PRIVATE
        // =============================================

        private void BumpVersion()
        {
            _version++;
            RequestSerialization();
        }

        private bool EnsureMasterOwnership()
        {
            if (_localPlayer == null)
                _localPlayer = Networking.LocalPlayer;
            if (_localPlayer == null || !_localPlayer.isMaster)
                return false;
            if (!Networking.IsOwner(_localPlayer, gameObject))
                Networking.SetOwner(_localPlayer, gameObject);
            return true;
        }
    }
}
