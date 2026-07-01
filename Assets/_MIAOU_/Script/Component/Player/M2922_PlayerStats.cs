using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;
using M2922.Component.Modifier;

namespace M2922.Component.Player
{
    /// <summary>
    /// Stats joueur : HP, armor, speed, team.
    /// </summary>
    [AddComponentMenu("M2922/Player/Player Stats")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PlayerStats : M2922_Base
    {
        [Header("=== STATS ===")]
        [SerializeField] private int _teamIndex = -1; // -1 = pas d'équipe
        [SerializeField] private int _kills = 0;
        [SerializeField] private int _deaths = 0;
        [SerializeField] private int _score = 0;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_HealthComponent _health;
        [SerializeField] private M2922_ModifierContainer _modifierContainer;

        public int TeamIndex { get => _teamIndex; set => _teamIndex = value; }
        public int Kills => _kills;
        public int Deaths => _deaths;
        public int Score => _score;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_health == null) _health = GetComponent<M2922_HealthComponent>();
            if (_modifierContainer == null) _modifierContainer = GetComponent<M2922_ModifierContainer>();
        }

        public void AddKill() { _kills++; _score += 100; }
        public void AddDeath() { _deaths++; }
        public void AddScore(int points) { _score += points; }

        public void ResetStats()
        {
            _kills = 0;
            _deaths = 0;
            _score = 0;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("K/D", $"{_kills} / {_deaths}"),
                new M2922_GizmoDisplayInfo("Score", _score.ToString(), Color.yellow),
                new M2922_GizmoDisplayInfo("Team", _teamIndex.ToString()),
            };
        }
#endif
    }
}
