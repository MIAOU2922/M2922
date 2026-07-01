using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Game
{
    /// <summary>
    /// Gestion du score : kills, deaths, points, assists.
    /// Stocke les scores par playerId (Udon-compatible, pas de gameObject).
    /// </summary>
    [AddComponentMenu("M2922/Game/Score Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ScoreManager : M2922_Base
    {
        [Header("=== SCORE VALUES ===")]
        [SerializeField] private int _killScore = 100;
        [SerializeField] private int _assistScore = 50;
        [SerializeField] private int _deathPenalty = 0;

        // Maps playerId → score total
        private DataDictionary _playerScores;
        private DataDictionary _playerKills;
        private DataDictionary _playerDeaths;

        protected override void Start()
        {
            base.Start();
            _playerScores = new DataDictionary();
            _playerKills = new DataDictionary();
            _playerDeaths = new DataDictionary();
        }

        public void RegisterKill(VRCPlayerApi killer, VRCPlayerApi victim)
        {
            if (killer != null)
            {
                IncrementStat(_playerKills, killer.playerId, 1);
                IncrementStat(_playerScores, killer.playerId, _killScore);
            }

            if (victim != null)
            {
                IncrementStat(_playerDeaths, victim.playerId, 1);
                IncrementStat(_playerScores, victim.playerId, -_deathPenalty);
            }

            string killerName = killer != null ? killer.displayName : "???";
            string victimName = victim != null ? victim.displayName : "???";
            this.Log($"Kill: {killerName} → {victimName}");
        }

        public void RegisterAssist(VRCPlayerApi assister)
        {
            if (assister == null) return;
            IncrementStat(_playerScores, assister.playerId, _assistScore);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Kill Score", _killScore.ToString(), Color.green),
                new M2922_GizmoDisplayInfo("Assist Score", _assistScore.ToString()),
                new M2922_GizmoDisplayInfo("Death Penalty", _deathPenalty.ToString(), Color.red),
            };
        }
#endif

        public int GetPlayerScore(VRCPlayerApi player)
        {
            if (player == null) return 0;
            if (_playerScores.TryGetValue(player.playerId, out DataToken tkn))
                return tkn.Int;
            return 0;
        }

        public int GetPlayerKills(VRCPlayerApi player)
        {
            if (player == null) return 0;
            if (_playerKills.TryGetValue(player.playerId, out DataToken tkn))
                return tkn.Int;
            return 0;
        }

        public int GetPlayerDeaths(VRCPlayerApi player)
        {
            if (player == null) return 0;
            if (_playerDeaths.TryGetValue(player.playerId, out DataToken tkn))
                return tkn.Int;
            return 0;
        }

        private void IncrementStat(DataDictionary dict, int playerId, int delta)
        {
            int current = 0;
            if (dict.TryGetValue(playerId, out DataToken tkn))
                current = tkn.Int;
            dict[playerId] = current + delta;
        }
    }
}
