using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace M2922.Core
{
    /// <summary>
    /// Gestionnaire principal du système PvP - CORE SYSTEM
    /// 
    /// RESPONSABILITÉS CORE:
    /// - Gestion de l'état du jeu (Waiting, Starting, InProgress, Ending)
    /// - Suivi des joueurs actifs
    /// - Événements globaux via EventBus
    /// - Référence centrale pour tous les modules
    /// 
    /// ARCHITECTURE MODULAIRE:
    /// Le système M2922 peut fonctionner à PLUSIEURS NIVEAUX:
    /// 
    /// NIVEAU 1 - Core minimal (Manager + EventBus + Combat):
    ///   → Utilisation: PvP libre, sandbox, tests
    ///   → Pas de règles, pas de rounds, pas de score
    ///   → Juste combat et événements
    /// 
    /// NIVEAU 2 - Avec Teams (+ TeamManager):
    ///   → Ajout de la gestion d'équipes
    ///   → Toujours pas de règles de jeu
    /// 
    /// ARCHITECTURE MODULAIRE:
    /// Le système M2922 peut fonctionner À PLUSIEURS NIVEAUX:
    /// 
    /// NIVEAU 1 - Core minimal (Manager + EventBus + Combat):
    ///   → Utilisation: PvP libre, sandbox, tests
    ///   → Pas de règles, pas de rounds, pas de score
    ///   → Juste combat et événements
    /// 
    /// NIVEAU 2 - Avec Teams (+ TeamManager):
    ///   → Ajout de la gestion d'équipes
    ///   → Toujours pas de règles de jeu
    /// 
    /// NIVEAU 3 - Avec GameMode:
    ///   → Implémentation des règles (rounds, scores, victoire)
    ///   → Exemples: TDM, CTF, Domination, FFA, etc.
    /// 
    /// CE MANAGER EST LE CORE MINIMAL.
    /// Il ne contient AUCUNE règle de jeu, AUCUNE logique de gameplay.
    /// Les GameModes utilisent les variables d'état (CurrentState, CurrentRound, etc.)
    /// et implémentent leurs propres règles.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_Manager : M2922_Base
    {
        [Header("=== GAME STATE (Pour GameModes) ===")]
        [Tooltip("L'état actuel du jeu. Les GameModes gèrent les changements d'état.")]
        [UdonSynced] public GameState CurrentState = GameState.Waiting;
        
        [Tooltip("Numéro du round actuel. Géré par les GameModes.")]
        [UdonSynced] public int CurrentRound = 0;
        
        [Tooltip("Temps restant dans le round. Géré par les GameModes.")]
        [UdonSynced] public float RoundTimeRemaining = 0f;
        
        [Header("=== PLAYER TRACKING ===")]
        [Tooltip("Nombre de joueurs connectés actuellement.")]
        [UdonSynced] public int PlayerCount = 0;
        private VRCPlayerApi[] _activePlayers;
        private int _maxPlayers = 80; // VRChat max
        
        [Header("=== REFERENCES ===")]
        public M2922_EventBus EventBus;
        
        // Private
        private bool _isHost = false;
        
        protected override void Start()
        {
            base.Start();
            _activePlayers = new VRCPlayerApi[_maxPlayers];
            
            // Trouver l'EventBus si pas assigné
            if (EventBus == null)
            {
                TryFindEventBus();
            }
            
            // Vérifier si on est le host
            _isHost = Networking.LocalPlayer != null && Networking.LocalPlayer.isMaster;
            
            this.Log($"M2922 Core Manager initialized. IsHost: {_isHost}");
            this.Log("No game rules active - waiting for GameMode script or manual control.");
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Le Manager core n'a AUCUNE logique de gameplay
            // Les GameModes implémentent leur propre Update() et gèrent les règles
        }
        
        // === PUBLIC API FOR GAMEMODES ===
        
        /// <summary>
        /// Changer l'état du jeu. Utilisé par les GameModes.
        /// </summary>
        public void SetGameState(GameState newState)
        {
            if (CurrentState == newState) return;
            
            GameState previousState = CurrentState;
            CurrentState = newState;
            
            this.Log($"Game State: {previousState} -> {CurrentState}");
            
            // Sync network
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
            
            // Publier l'événement
            if (EventBus != null)
            {
                var slot = EventBus.RentSlot();
                if (slot != null)
                {
                    slot.NewGameState = (int)CurrentState;
                    slot.PreviousGameState = (int)previousState;
                    EventBus.PublishNetwork(EventType.OnGameStarted, slot);
                }
            }
        }
        
        // === PLAYER MANAGEMENT ===
        
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
            
            // Publier événement
            if (EventBus != null && player.isLocal)
            {
                var slot = EventBus.RentSlot();
                if (slot != null) slot.PlayerId = player.playerId;
                EventBus.Publish(EventType.OnPlayerJoined, slot);
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
            
            // Publier événement
            if (EventBus != null)
            {
                var slot = EventBus.RentSlot();
                if (slot != null) slot.PlayerId = player.playerId;
                EventBus.Publish(EventType.OnPlayerLeft, slot);
            }
        }
        
        /// <summary>
        /// Met à jour le nombre de joueurs. Appelé automatiquement ou manuellement par les GameModes.
        /// </summary>
        public void UpdatePlayerCount()
        {
            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);
            PlayerCount = players.Length;
        }
        
        // === PUBLIC GETTERS ===
        
        public bool IsGameInProgress()
        {
            return CurrentState == GameState.InProgress;
        }
        
        public bool CanSpawn()
        {
            return CurrentState == GameState.InProgress || CurrentState == GameState.Preparing;
        }
        
        public bool IsHost()
        {
            return _isHost;
        }
        
        // === PRIVATE HELPERS ===
        
        private void TryFindEventBus()
        {
            GameObject eventBusObj = GameObject.Find("EventBus");
            if (eventBusObj == null) return;
            EventBus = eventBusObj.GetComponent<M2922_EventBus>();
            if (EventBus == null) return;
        }
    }
}
