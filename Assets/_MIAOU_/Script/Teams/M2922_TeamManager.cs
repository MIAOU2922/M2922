using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using M2922.Core;
using CoreEventType = M2922.Core.EventType;

namespace M2922.Teams
{
    /// <summary>
    /// Gestionnaire central des équipes pour le système PvP - OPTIONNEL
    /// 
    /// RESPONSABILITÉS:
    /// - Attribution des équipes aux joueurs (auto-balance)
    /// - Gestion des scores par équipe
    /// - Gestion des couleurs et spawns par équipe
    /// - Vérification du friendly fire
    /// 
    /// ARCHITECTURE:
    /// Ce module est OPTIONNEL. Utilisez-le uniquement pour les modes team-based:
    /// - Team Deathmatch
    /// - Capture the Flag
    /// - Domination
    /// - Etc.
    /// 
    /// Les modes FFA (Free-for-all/Chacun pour soi) n'ont PAS besoin de ce module.
    /// Le système PvP core (Manager, EventBus, HealthController) fonctionne
    /// indépendamment du TeamManager.
    /// 
    /// SUPPORT N-TEAM:
    /// Supporte 2 à 16 équipes dynamiques configurées via TeamConfig[].
    /// Les équipes sont identifiées par leur index int (0-15), NO_TEAM = -1.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_TeamManager : M2922_Base
    {
        [Header("=== TEAM CONFIGURATION ===")]
        [Tooltip("Nombre d'équipes actives (2-16)")]
        [Range(2, 16)]
        [SerializeField] private int _teamCount = 2;
        
        [Tooltip("Noms des équipes (taille = _teamCount)")]
        [SerializeField] private string[] _teamNames;
        
        [Tooltip("Couleurs des équipes (taille = _teamCount)")]
        [SerializeField] private Color[] _teamColors;
        
        [Tooltip("Équipes actives (taille = _teamCount)")]
        [SerializeField] private bool[] _teamIsActive;
        
        [Header("=== GAME SETTINGS ===")]
        [Tooltip("Activer l'auto-balance des équipes")]
        [SerializeField] private bool _autoBalance = true;
        
        [Tooltip("Autoriser le friendly fire")]
        [SerializeField] private bool _allowFriendlyFire = false;
        
        [Tooltip("Multiplicateur de dégâts pour le friendly fire (0 = aucun dégât, 1 = dégâts complets)")]
        [Range(0f, 1f)]
        [SerializeField] private float _friendlyFireDamageMultiplier = 0.5f;
        
        [Tooltip("Couleur pour joueurs sans équipe")]
        [SerializeField] private Color _neutralColor = Color.gray;
        
        [Header("=== SYNC DATA ===")]
        [UdonSynced] private int[] _teamScores;
        [UdonSynced] private int[] _teamPlayerCounts;
        
        // Player team mapping (playerId → TeamIndex)
        private int[] _playerTeams;
        private const int MAX_PLAYERS = 80;
        private const int NO_TEAM = -1;
        
        // Note: UdonSharp ne supporte pas les champs statiques
        // Utilisez une référence assignée depuis l'inspecteur ou Manager.TeamManagerRef
        
        protected override void Start()
        {
            base.Start();
            
            // Initialiser les arrays basés sur le nombre d'équipes
            InitializeTeamData();
            
            // Initialiser le mapping des joueurs
            _playerTeams = new int[MAX_PLAYERS];
            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                _playerTeams[i] = NO_TEAM;
            }
            
            this.Log($"TeamManager initialized. Teams: {_teamCount}, Auto-balance: {_autoBalance}");
        }
        
        private void InitializeTeamData()
        {
            // Créer les arrays si pas déjà fait
            if (_teamScores == null || _teamScores.Length != _teamCount)
            {
                _teamScores = new int[_teamCount];
            }
            
            if (_teamPlayerCounts == null || _teamPlayerCounts.Length != _teamCount)
            {
                _teamPlayerCounts = new int[_teamCount];
            }
            
            // Valider la configuration (doit être faite dans l'inspecteur via le Setup Wizard)
            bool namesOk = _teamNames != null && _teamNames.Length == _teamCount;
            bool colorsOk = _teamColors != null && _teamColors.Length == _teamCount;
            bool activeOk = _teamIsActive != null && _teamIsActive.Length == _teamCount;
            
            if (!namesOk || !colorsOk || !activeOk)
            {
                this.Warning($"TeamManager: arrays mal configurés (names:{(namesOk?"OK":"KO")}, colors:{(colorsOk?"OK":"KO")}, active:{(activeOk?"OK":"KO")}). Utilisez le M2922 Setup Wizard.");
            }
        }
        
        // === PUBLIC METHODS ===
        
        /// <summary>
        /// Assigner un joueur à une équipe (par index 0-based)
        /// </summary>
        public void AssignPlayerToTeam(int playerId, int teamIndex)
        {
            if (playerId < 0 || playerId >= MAX_PLAYERS)
            {
                this.Warning($"Invalid playerId: {playerId}");
                return;
            }
            
            if (teamIndex < -1 || teamIndex >= _teamCount)
            {
                this.Warning($"Invalid teamIndex: {teamIndex}. Valid range: -1 to {_teamCount - 1}");
                return;
            }
            
            int previousTeam = _playerTeams[playerId];
            
            if (previousTeam == teamIndex)
            {
                this.VerboseLog($"Player {playerId} already in team {teamIndex}");
                return;
            }
            
            // Retirer du compte de l'ancienne équipe
            if (previousTeam >= 0 && previousTeam < _teamCount)
            {
                _teamPlayerCounts[previousTeam] = Mathf.Max(0, _teamPlayerCounts[previousTeam] - 1);
            }
            
            // Assigner à la nouvelle équipe
            _playerTeams[playerId] = teamIndex;
            
            if (teamIndex >= 0 && teamIndex < _teamCount)
            {
                _teamPlayerCounts[teamIndex]++;
            }
            
            string prevName = GetTeamName(previousTeam);
            string newName = GetTeamName(teamIndex);
            this.Log($"Player {playerId}: {prevName} -> {newName}");
            
            // Publier événement
            if (Manager != null && Manager.EventBus != null)
            {
                var slot = Manager.EventBus.RentSlot();
                if (slot != null)
                {
                    slot.TeamIndex = teamIndex;
                    slot.PlayerId = playerId;
                    slot.PreviousTeamIndex = previousTeam;
                    Manager.EventBus.PublishNetwork(CoreEventType.OnTeamChanged, slot);
                }
            }
        }
        
        /// <summary>
        /// Assigner automatiquement un joueur à l'équipe la moins remplie
        /// </summary>
        public int AssignPlayerAutoBalance(int playerId)
        {
            if (!_autoBalance)
            {
                this.Warning("Auto-balance is disabled");
                return NO_TEAM;
            }
            
            int smallestTeam = GetSmallestTeam();
            AssignPlayerToTeam(playerId, smallestTeam);
            
            return smallestTeam;
        }
        
        /// <summary>
        /// Obtenir l'index de l'équipe d'un joueur (-1 = pas d'équipe)
        /// </summary>
        public int GetPlayerTeamIndex(int playerId)
        {
            if (playerId < 0 || playerId >= MAX_PLAYERS)
            {
                return NO_TEAM;
            }
            
            return _playerTeams[playerId];
        }
        
        /// <summary>
        /// Vérifier si deux joueurs sont dans la même équipe
        /// </summary>
        public bool ArePlayersOnSameTeam(int playerId1, int playerId2)
        {
            int team1 = GetPlayerTeamIndex(playerId1);
            int team2 = GetPlayerTeamIndex(playerId2);
            
            if (team1 == NO_TEAM || team2 == NO_TEAM)
                return false;
            
            return team1 == team2;
        }
        
        /// <summary>
        /// Vérifier si le friendly fire est actif
        /// </summary>
        public bool IsFriendlyFireAllowed()
        {
            return _allowFriendlyFire;
        }
        
        /// <summary>
        /// Obtenir le multiplicateur de dégâts entre deux joueurs
        /// Retourne 1.0 pour dégâts normaux, ou le multiplicateur FF si alliés
        /// </summary>
        public float GetDamageMultiplier(int attackerId, int victimId)
        {
            // Dégâts normaux sur soi-même
            if (attackerId == victimId) return 1f;
            
            // Si friendly fire désactivé, retourner 0
            if (!_allowFriendlyFire)
            {
                if (ArePlayersOnSameTeam(attackerId, victimId))
                {
                    return 0f; // Aucun dégât
                }
            }
            // Si friendly fire activé avec même équipe, retourner le multiplicateur
            else if (ArePlayersOnSameTeam(attackerId, victimId))
            {
                return _friendlyFireDamageMultiplier;
            }
            
            // Dégâts normaux contre ennemis
            return 1f;
        }
        
        /// <summary>
        /// Vérifier si un joueur peut endommager un autre
        /// </summary>
        public bool CanDamage(int attackerId, int victimId)
        {
            // Toujours autoriser les dégâts sur soi-même
            if (attackerId == victimId) return true;
            
            // Si pas de friendly fire, vérifier si ce sont des alliés
            if (!_allowFriendlyFire)
            {
                if (ArePlayersOnSameTeam(attackerId, victimId))
                {
                    this.VerboseLog($"Blocked friendly fire: {attackerId} -> {victimId}");
                    return false;
                }
            }
            
            return true;
        }
        
        // === TEAM SCORES ===
        
        /// <summary>
        /// Ajouter des points à une équipe
        /// </summary>
        public void AddTeamScore(int teamIndex, int points)
        {
            if (teamIndex < 0 || teamIndex >= _teamCount)
            {
                this.Warning($"Invalid teamIndex: {teamIndex}");
                return;
            }
            
            if (points <= 0) return;
            
            int oldScore = _teamScores[teamIndex];
            _teamScores[teamIndex] += points;
            
            this.Log($"Team {GetTeamName(teamIndex)} score: {oldScore} -> {_teamScores[teamIndex]} (+{points})");
            
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
            
            // Publier événement
            if (Manager != null && Manager.EventBus != null)
            {
                var slot = Manager.EventBus.RentSlot();
                if (slot != null)
                {
                    slot.TeamIndex = teamIndex;
                    slot.TeamScore = _teamScores[teamIndex];
                    slot.ScoreChange = points;
                    Manager.EventBus.PublishNetwork(M2922.Core.EventType.OnTeamScoreChanged, slot);
                }
            }
        }
        
        /// <summary>
        /// Obtenir le score d'une équipe
        /// </summary>
        public int GetTeamScore(int teamIndex)
        {
            if (teamIndex < 0 || teamIndex >= _teamCount)
                return 0;
            
            return _teamScores[teamIndex];
        }
        
        /// <summary>
        /// Réinitialiser tous les scores
        /// </summary>
        public void ResetAllScores()
        {
            for (int i = 0; i < _teamCount; i++)
            {
                _teamScores[i] = 0;
            }
            
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
            
            this.Log("All team scores reset");
        }
        
        /// <summary>
        /// Obtenir l'index de l'équipe gagnante (score le plus élevé)
        /// </summary>
        public int GetWinningTeamIndex()
        {
            int maxScore = 0;
            int winningTeam = NO_TEAM;
            
            for (int i = 0; i < _teamCount; i++)
            {
                bool isActive = _teamIsActive != null && i < _teamIsActive.Length && _teamIsActive[i];
                if (isActive && _teamScores[i] > maxScore)
                {
                    maxScore = _teamScores[i];
                    winningTeam = i;
                }
            }
            
            return winningTeam;
        }
        
        // === TEAM COLORS & INFO ===
        
        public Color GetTeamColor(int teamIndex)
        {
            if (teamIndex < 0 || teamIndex >= _teamCount || _teamColors == null || teamIndex >= _teamColors.Length)
                return _neutralColor;
            
            return _teamColors[teamIndex];
        }
        
        public string GetTeamName(int teamIndex)
        {
            if (teamIndex < 0 || teamIndex >= _teamCount)
                return "None";
            
            if (_teamNames != null && teamIndex < _teamNames.Length)
                return _teamNames[teamIndex];
            
            return $"Team {teamIndex}";
        }
        
        public int GetTeamCount()
        {
            return _teamCount;
        }
        
        public int GetTeamPlayerCount(int teamIndex)
        {
            if (teamIndex < 0 || teamIndex >= _teamCount)
                return 0;
            
            return _teamPlayerCounts[teamIndex];
        }
        
        // === PRIVATE HELPERS ===
        
        private int GetSmallestTeam()
        {
            int smallestTeam = 0;
            int smallestCount = int.MaxValue;
            
            for (int i = 0; i < _teamCount; i++)
            {
                bool isActive = _teamIsActive != null && i < _teamIsActive.Length && _teamIsActive[i];
                if (isActive && _teamPlayerCounts[i] < smallestCount)
                {
                    smallestTeam = i;
                    smallestCount = _teamPlayerCounts[i];
                }
            }
            
            return smallestTeam;
        }
        
    }
}
