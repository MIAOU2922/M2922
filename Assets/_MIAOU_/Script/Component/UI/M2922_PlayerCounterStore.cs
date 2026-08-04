using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.UI
{
    /// <summary>
    /// Stockage persistant des compteurs par joueur (displayName).
    /// Survit aux déconnexions/reconnexions car stocké sur un objet synced.
    /// 
    /// SETUP :
    /// 1. Placer sur un GameObject persistant dans la scène.
    /// 2. Référencer ce store dans chaque M2922_CounterUI synchronisé.
    /// </summary>
    [AddComponentMenu("M2922/UI/Player Counter Store")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PlayerCounterStore : M2922_Base
    {
        [Header("=== STORAGE (synced) ===")]
        [Tooltip("Noms des joueurs, séparés par | (synced).")]
        [UdonSynced] private string _playerNamesSerialized = "";

        [Tooltip("Valeurs des compteurs, séparées par | (synced, même ordre que les noms).")]
        [UdonSynced] private string _playerCountersSerialized = "";

        // Cache local (non-synced)
        [SerializeField] private string[] _playerNames = new string[0];
        [SerializeField] private int[] _playerCounters = new int[0];

        private VRCPlayerApi _localPlayer;

        protected override void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            DeserializeCounters();
        }

        public override void OnDeserialization()
        {
            DeserializeCounters();
        }

        // =============================================
        //  PRIVATE — Serialization
        // =============================================

        private void DeserializeCounters()
        {
            string[] names = SplitByPipe(_playerNamesSerialized);
            string[] countersStr = SplitByPipe(_playerCountersSerialized);

            int count = names.Length;
            if (countersStr.Length < count)
                count = countersStr.Length;

            _playerNames = new string[count];
            _playerCounters = new int[count];

            for (int i = 0; i < count; i++)
            {
                _playerNames[i] = names[i];
                int val = 0;
                if (int.TryParse(countersStr[i], out val))
                    _playerCounters[i] = val;
            }
        }

        private void SerializeCounters()
        {
            if (_playerNames == null || _playerNames.Length == 0)
            {
                _playerNamesSerialized = "";
                _playerCountersSerialized = "";
                return;
            }

            _playerNamesSerialized = JoinByPipe(_playerNames);

            string[] counterStrs = new string[_playerCounters.Length];
            for (int i = 0; i < _playerCounters.Length; i++)
                counterStrs[i] = _playerCounters[i].ToString();
            _playerCountersSerialized = JoinByPipe(counterStrs);
        }

        // =============================================
        //  PUBLIC — Appelé par M2922_CounterUI
        // =============================================

        /// <summary>Sauvegarde le compteur d'un joueur (appelé par le CounterUI).</summary>
        public void SaveCounter(string displayName, int value)
        {
            if (_localPlayer == null) _localPlayer = Networking.LocalPlayer;
            if (_localPlayer == null) return;
            if (!Networking.IsOwner(_localPlayer, gameObject))
                Networking.SetOwner(_localPlayer, gameObject);

            // Chercher si le joueur existe déjà
            int idx = -1;
            for (int i = 0; i < _playerNames.Length; i++)
            {
                if (_playerNames[i] == displayName) { idx = i; break; }
            }

            if (idx >= 0)
            {
                _playerCounters[idx] = value;
            }
            else
            {
                // Ajouter
                string[] newNames = new string[_playerNames.Length + 1];
                int[] newCounters = new int[_playerCounters.Length + 1];
                for (int i = 0; i < _playerNames.Length; i++)
                {
                    newNames[i] = _playerNames[i];
                    newCounters[i] = _playerCounters[i];
                }
                newNames[_playerNames.Length] = displayName;
                newCounters[_playerCounters.Length] = value;
                _playerNames = newNames;
                _playerCounters = newCounters;
            }

            SerializeCounters();
            RequestSerialization();
        }

        /// <summary>Récupère le compteur sauvegardé d'un joueur. Retourne -1 si absent.</summary>
        public int GetCounter(string displayName)
        {
            for (int i = 0; i < _playerNames.Length; i++)
            {
                if (_playerNames[i] == displayName)
                    return _playerCounters[i];
            }
            return -1;
        }

        /// <summary>Le joueur a-t-il un compteur sauvegardé ?</summary>
        public bool HasCounter(string displayName)
        {
            for (int i = 0; i < _playerNames.Length; i++)
            {
                if (_playerNames[i] == displayName)
                    return true;
            }
            return false;
        }

        // =============================================
        //  HELPERS
        // =============================================

        private string[] SplitByPipe(string s)
        {
            if (string.IsNullOrEmpty(s)) return new string[0];
            return s.Split('|');
        }

        private string JoinByPipe(string[] parts)
        {
            if (parts == null || parts.Length == 0) return "";
            string result = parts[0];
            for (int i = 1; i < parts.Length; i++)
                result += "|" + parts[i];
            return result;
        }
    }
}
