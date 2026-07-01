using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Game
{
    public enum GameMode { FreeForAll, TeamDeathmatch, CaptureTheFlag, KingOfTheHill }

    /// <summary>
    /// Règles de partie : TDM, FFA, CTF, etc.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_GameModeManager : M2922_Base
    {
        [Header("=== GAME MODE ===")]
        [SerializeField] private GameMode _currentMode = GameMode.FreeForAll;
        [SerializeField] private int _scoreLimit = 50;
        [SerializeField] private float _timeLimit = 600f; // 10 min

        [Header("=== STATE ===")]
        [UdonSynced] private bool _isRoundActive = false;
        [UdonSynced] private float _roundTimeRemaining = 0f;

        public GameMode CurrentMode => _currentMode;
        public bool IsRoundActive => _isRoundActive;
        public float TimeRemaining => _roundTimeRemaining;

        public void StartRound()
        {
            _isRoundActive = true;
            _roundTimeRemaining = _timeLimit;
            this.Log($"Round started: {_currentMode}");
        }

        public void EndRound()
        {
            _isRoundActive = false;
            this.Log("Round ended.");
        }

        protected override void Update()
        {
            base.Update();

            if (_isRoundActive)
            {
                _roundTimeRemaining -= Time.deltaTime;
                if (_roundTimeRemaining <= 0f)
                    EndRound();
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Mode", _currentMode.ToString(), Color.yellow),
                new M2922_GizmoDisplayInfo("Score Limit", _scoreLimit.ToString()),
                new M2922_GizmoDisplayInfo("Active", _isRoundActive.ToString(), _isRoundActive ? Color.green : Color.red),
                new M2922_GizmoDisplayInfo("Time Left", $"{_roundTimeRemaining:F0}s"),
            };
        }
#endif
    }
}
