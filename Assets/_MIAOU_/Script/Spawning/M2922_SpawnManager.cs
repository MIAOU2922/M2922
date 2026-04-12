using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using System;
using M2922.Core;
using M2922.Teams;
using CoreEventType = M2922.Core.EventType;

namespace M2922.Spawning
{
    /// <summary>
    /// Gestionnaire central du système de spawn
    /// 
    /// MODES DE FONCTIONNEMENT:
    /// 1. Mode FFA (sans TeamManager):
    ///    - Utilise tous les spawn points disponibles
    ///    - Spawn aléatoire parmi les points actifs
    /// 
    /// 2. Mode Team (avec TeamManager):
    ///    - Utilise les spawn points filtrés par équipe
    ///    - Spawn uniquement aux points de l'équipe du joueur
    /// 
    /// STRATÉGIES DE SPAWN:
    /// - Random: Spawn aléatoire parmi les points disponibles
    /// - Sequential: Spawn en séquence (rotation)
    /// - LeastRecent: Spawn au point le plus ancien
    /// - Farthest: Spawn au point le plus éloigné des ennemis
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_SpawnManager : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("TeamManager (optionnel, pour mode team-based)")]
        [SerializeField] private M2922_TeamManager _teamManager;
        
        [Header("=== SPAWN SETTINGS ===")]
        [Tooltip("Stratégie de sélection de spawn")]
        [SerializeField] private SpawnStrategy _spawnStrategy = SpawnStrategy.Random;
        
        [Tooltip("Durée d'invincibilité après spawn (secondes)")]
        [SerializeField] private float _spawnProtectionDuration = 3f;
        
        [Tooltip("Délai minimum entre deux spawns au même point (secondes)")]
        [SerializeField] private float _spawnCooldown = 2f;
        
        [Tooltip("Distance minimum entre spawn et ennemis (0 = désactivé)")]
        [SerializeField] private float _minEnemyDistance = 5f;
        
        [Header("=== AUTO DISCOVERY ===")]
        [Tooltip("Trouver automatiquement tous les SpawnPoints dans la scène")]
        [SerializeField] private bool _autoDiscoverSpawnPoints = true;
        
        [Tooltip("Liste des spawn points (remplie automatiquement si Auto Discover activé)")]
        [SerializeField] private M2922_SpawnPoint[] _spawnPoints;
        
        [Header("=== RESPAWN SETTINGS ===")]
        [Tooltip("Activer le respawn automatique")]
        [SerializeField] private bool _autoRespawn = true;
        
        [Tooltip("Délai avant respawn automatique (secondes)")]
        [SerializeField] private float _respawnDelay = 5f;
        
        // === RUNTIME DATA ===
        private M2922_SpawnPoint[] _allSpawnPoints;
        private int _nextSequentialIndex = 0;
        private bool _isInitialized = false;
        
        // Player respawn tracking
        private float[] _playerRespawnTimes;
        private const int MAX_PLAYERS = 80;
        

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        // validation dans l'editeur
        protected virtual void OnValidate()
        {
        if (_autoDiscoverSpawnPoints)
            {
                // Trouver tous les SpawnPoints dans la scène
                _allSpawnPoints = GameObject.FindObjectsOfType<M2922_SpawnPoint>();
                this.Log($"Auto-discovered {_allSpawnPoints.Length} spawn points");
            }
        }
#endif
        protected override void Start()
        {
            base.Start();
            
            // TeamManager doit être assigné via l'inspector (FFA si absent)
            if (_teamManager == null)
                this.Log("Running in FFA mode (no TeamManager assigned in inspector)");
            
            // Initialiser le tracking des respawns
            _playerRespawnTimes = new float[MAX_PLAYERS];
            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                _playerRespawnTimes[i] = -999f;
            }
            
            // Découvrir les spawn points
            InitializeSpawnPoints();
            
            // S'abonner aux événements
            SubscribeToEvents();
            
            _isInitialized = true;
            this.Log($"SpawnManager initialized. Mode: {(_teamManager != null ? "Team-based" : "FFA")}, Strategy: {_spawnStrategy}");
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Gérer les respawns automatiques
            if (_autoRespawn)
            {
                ProcessAutoRespawns();
            }
        }
        
        // === INITIALIZATION ===
        
        private void InitializeSpawnPoints()
        {
            // Utiliser les spawns manuels
            _allSpawnPoints = _spawnPoints;
            this.Log($"Using {_allSpawnPoints.Length} manually assigned spawn points");
            
            
            if (_allSpawnPoints == null || _allSpawnPoints.Length == 0)
            {
                this.Warning("No spawn points found! Players won't be able to spawn.");
            }
        }
        
        private void SubscribeToEvents()
        {
            if (Manager != null && Manager.EventBus != null)
            {
                // S'abonner à l'événement de mort pour gérer le respawn
                Manager.EventBus.Subscribe(CoreEventType.OnPlayerDied, this);
                
                this.VerboseLog("Subscribed to player death events");
            }
        }
        
        // === EVENT HANDLERS ===
        
        public void OnPlayerDied(object eventData)
        {
            // TODO: Extraire playerData du eventData
            // Pour l'instant, on laisse l'auto-respawn gérer
            this.VerboseLog("Player died event received");
        }
        
        // === SPAWNING ===
        
        /// <summary>
        /// Spawner un joueur à une position aléatoire selon son équipe
        /// </summary>
        public void SpawnPlayer(VRCPlayerApi player)
        {
            if (!_isInitialized || player == null) return;
            
            int teamIndex = -1;
            
            // Obtenir l'équipe du joueur si TeamManager existe
            if (_teamManager != null)
            {
                teamIndex = _teamManager.GetPlayerTeamIndex(player.playerId);
            }
            
            // Sélectionner un spawn point
            M2922_SpawnPoint spawnPoint = SelectSpawnPoint(teamIndex, player);
            
            if (spawnPoint == null)
            {
                this.Warning($"No valid spawn point found for player {player.displayName} (team {teamIndex})");
                return;
            }
            
            // Téléporter le joueur
            player.TeleportTo(spawnPoint.Position, spawnPoint.Rotation);
            
            // Enregistrer le spawn
            spawnPoint.RecordSpawn();
            
            this.Log($"Spawned player {player.displayName} at {spawnPoint.name}");
            
            // TODO: Appliquer protection de spawn
            // TODO: Publier événement de spawn
        }
        
        /// <summary>
        /// Spawner le joueur local
        /// </summary>
        public void SpawnLocalPlayer()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer != null)
            {
                SpawnPlayer(localPlayer);
            }
        }
        
        /// <summary>
        /// Planifier un respawn dans X secondes
        /// </summary>
        public void ScheduleRespawn(VRCPlayerApi player, float delay)
        {
            if (player == null) return;
            
            _playerRespawnTimes[player.playerId] = Time.time + delay;
            
            this.VerboseLog($"Respawn scheduled for {player.displayName} in {delay}s");
        }
        
        private void ProcessAutoRespawns()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null) return;
            
            int playerId = localPlayer.playerId;
            
            // Vérifier si c'est l'heure de respawn
            if (_playerRespawnTimes[playerId] > 0f && Time.time >= _playerRespawnTimes[playerId])
            {
                _playerRespawnTimes[playerId] = -999f;
                SpawnLocalPlayer();
            }
        }
        
        // === SPAWN POINT SELECTION ===
        
        private M2922_SpawnPoint SelectSpawnPoint(int teamIndex, VRCPlayerApi player)
        {
            if (_allSpawnPoints == null || _allSpawnPoints.Length == 0)
                return null;
            
            // Filtrer les spawn points valides
            M2922_SpawnPoint[] validSpawns = GetValidSpawnPoints(teamIndex);
            
            if (validSpawns == null || validSpawns.Length == 0)
                return null;
            
            // Sélectionner selon la stratégie
            switch (_spawnStrategy)
            {
                case SpawnStrategy.Random:
                    return SelectRandomSpawn(validSpawns);
                    
                case SpawnStrategy.Sequential:
                    return SelectSequentialSpawn(validSpawns);
                    
                case SpawnStrategy.LeastRecent:
                    return SelectLeastRecentSpawn(validSpawns);
                    
                case SpawnStrategy.Farthest:
                    return SelectFarthestSpawn(validSpawns, player);
                    
                default:
                    return SelectRandomSpawn(validSpawns);
            }
        }
        
        private M2922_SpawnPoint[] GetValidSpawnPoints(int teamIndex)
        {
            int count = 0;
            
            // Compter les spawns valides
            for (int i = 0; i < _allSpawnPoints.Length; i++)
            {
                M2922_SpawnPoint spawn = _allSpawnPoints[i];
                if (spawn == null || !spawn.IsActive) continue;
                
                // Vérifier le cooldown
                if (spawn.GetTimeSinceLastSpawn() < _spawnCooldown) continue;
                
                // Vérifier l'équipe (si mode team)
                if (_teamManager != null && teamIndex >= 0)
                {
                    if (spawn.TeamIndex != teamIndex) continue;
                }
                
                count++;
            }
            
            if (count == 0) return null;
            
            // Créer le tableau filtré
            M2922_SpawnPoint[] validSpawns = new M2922_SpawnPoint[count];
            int index = 0;
            
            for (int i = 0; i < _allSpawnPoints.Length; i++)
            {
                M2922_SpawnPoint spawn = _allSpawnPoints[i];
                if (spawn == null || !spawn.IsActive) continue;
                if (spawn.GetTimeSinceLastSpawn() < _spawnCooldown) continue;
                
                if (_teamManager != null && teamIndex >= 0)
                {
                    if (spawn.TeamIndex != teamIndex) continue;
                }
                
                validSpawns[index++] = spawn;
            }
            
            return validSpawns;
        }
        
        private M2922_SpawnPoint SelectRandomSpawn(M2922_SpawnPoint[] spawns)
        {
            int randomIndex = UnityEngine.Random.Range(0, spawns.Length);
            return spawns[randomIndex];
        }
        
        private M2922_SpawnPoint SelectSequentialSpawn(M2922_SpawnPoint[] spawns)
        {
            M2922_SpawnPoint selected = spawns[_nextSequentialIndex % spawns.Length];
            _nextSequentialIndex++;
            return selected;
        }
        
        private M2922_SpawnPoint SelectLeastRecentSpawn(M2922_SpawnPoint[] spawns)
        {
            M2922_SpawnPoint oldest = spawns[0];
            float oldestTime = oldest.GetTimeSinceLastSpawn();
            
            for (int i = 1; i < spawns.Length; i++)
            {
                float time = spawns[i].GetTimeSinceLastSpawn();
                if (time > oldestTime)
                {
                    oldest = spawns[i];
                    oldestTime = time;
                }
            }
            
            return oldest;
        }
        
        private M2922_SpawnPoint SelectFarthestSpawn(M2922_SpawnPoint[] spawns, VRCPlayerApi player)
        {
            // TODO: Calculer la distance aux ennemis
            // Pour l'instant, utiliser random
            return SelectRandomSpawn(spawns);
        }
        
        // === PUBLIC API ===
        
        /// <summary>
        /// Obtenir tous les spawn points pour une équipe
        /// </summary>
        public M2922_SpawnPoint[] GetTeamSpawnPoints(int teamIndex)
        {
            return GetValidSpawnPoints(teamIndex);
        }
        
        /// <summary>
        /// Obtenir un spawn point aléatoire pour une équipe
        /// </summary>
        public Transform GetRandomTeamSpawn(int teamIndex)
        {
            M2922_SpawnPoint spawn = SelectSpawnPoint(teamIndex, null);
            return spawn != null ? spawn.transform : null;
        }
        
        /// <summary>
        /// Activer/désactiver tous les spawns d'une équipe
        /// </summary>
        public void SetTeamSpawnsActive(int teamIndex, bool active)
        {
            for (int i = 0; i < _allSpawnPoints.Length; i++)
            {
                if (_allSpawnPoints[i].TeamIndex == teamIndex)
                {
                    _allSpawnPoints[i].SetActive(active);
                }
            }
            
            this.Log($"Team {teamIndex} spawns {(active ? "activated" : "deactivated")}");
        }
    }
    
    /// <summary>
    /// Stratégies de sélection de spawn
    /// </summary>
    public enum SpawnStrategy
    {
        Random,      // Aléatoire parmi les points disponibles
        Sequential,  // Rotation séquentielle
        LeastRecent, // Point le plus ancien (non utilisé récemment)
        Farthest     // Point le plus éloigné des ennemis
    }
}
