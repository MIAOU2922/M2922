using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Game
{
    /// <summary>
    /// Gestion des équipes et assignation des joueurs.
    /// </summary>
    [AddComponentMenu("M2922/Game/Team Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_TeamManager : M2922_Base
    {
        [Header("=== TEAMS ===")]
        [SerializeField] private int _maxTeams = 4;
        [SerializeField] private int _maxPlayersPerTeam = 20;

        [Header("=== TEAM COLORS ===")]
        [SerializeField] private Color[] _teamColors;

        [Header("=== SCORES ===")]
        [UdonSynced] private int[] _teamScores;

        // Maps playerId → teamIndex (Udon-compatible, no gameObject access)
        private DataDictionary _playerTeams;

        protected override void Start()
        {
            base.Start();
            _teamScores = new int[_maxTeams];
            _playerTeams = new DataDictionary();
        }

        public void AssignTeam(VRCPlayerApi player)
        {
            if (player == null) return;

            // Équilibrage basique : assigne à l'équipe avec le moins de joueurs
            int bestTeam = 0;
            int minPlayers = int.MaxValue;

            for (int i = 0; i < _maxTeams; i++)
            {
                int count = CountPlayersInTeam(i);
                if (count < minPlayers && count < _maxPlayersPerTeam)
                {
                    minPlayers = count;
                    bestTeam = i;
                }
            }

            _playerTeams[player.playerId] = bestTeam;

            this.Log($"Player {player.displayName} assigned to team {bestTeam}");
        }

        public int GetPlayerTeam(VRCPlayerApi player)
        {
            if (player == null) return -1;
            if (_playerTeams.TryGetValue(player.playerId, out DataToken token))
                return token.Int;
            return -1;
        }

        public void RemovePlayer(VRCPlayerApi player)
        {
            if (player != null && _playerTeams.ContainsKey(player.playerId))
                _playerTeams.Remove(player.playerId);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Teams", $"{_maxTeams} max", Color.cyan),
                new M2922_GizmoDisplayInfo("Players/Team", $"{_maxPlayersPerTeam} max"),
            };
        }
#endif

        private int CountPlayersInTeam(int teamIndex)
        {
            int count = 0;
            DataList keys = _playerTeams.GetKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                if (_playerTeams.TryGetValue(keys[i], out DataToken tkn) && tkn.Int == teamIndex)
                    count++;
            }
            return count;
        }

        public void AddTeamScore(int teamIndex, int points)
        {
            if (teamIndex >= 0 && teamIndex < _teamScores.Length)
            {
                _teamScores[teamIndex] += points;
                if (Networking.IsOwner(gameObject)) RequestSerialization();
            }
        }

        public int GetTeamScore(int teamIndex)
        {
            return (teamIndex >= 0 && teamIndex < _teamScores.Length) ? _teamScores[teamIndex] : 0;
        }

        public Color GetTeamColor(int teamIndex)
        {
            if (_teamColors != null && teamIndex >= 0 && teamIndex < _teamColors.Length)
                return _teamColors[teamIndex];
            return Color.white;
        }
    }
}
