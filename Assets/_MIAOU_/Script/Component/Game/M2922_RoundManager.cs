using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Game
{
    public enum RoundPhase { Waiting, Countdown, Playing, Overtime, Ended }

    /// <summary>
    /// Gestion des manches : début, fin, timers, phases.
    /// </summary>
    [AddComponentMenu("M2922/Game/Round Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_RoundManager : M2922_Base
    {
        [Header("=== ROUND ===")]
        [SerializeField] private RoundPhase _phase = RoundPhase.Waiting;
        [SerializeField] private float _countdownDuration = 5f;
        [SerializeField] private float _roundDuration = 300f;
        [SerializeField] private int _roundsToWin = 3;

        [Header("=== STATE ===")]
        [UdonSynced] private int _currentRound = 0;
        [UdonSynced] private float _phaseTimer = 0f;

        public RoundPhase Phase => _phase;
        public int CurrentRound => _currentRound;

        protected override void Start()
        {
            base.Start();
            SetPhase(RoundPhase.Waiting);
        }

        protected override void Update()
        {
            base.Update();
            _phaseTimer -= Time.deltaTime;

            switch (_phase)
            {
                case RoundPhase.Countdown:
                    if (_phaseTimer <= 0f) SetPhase(RoundPhase.Playing);
                    break;
                case RoundPhase.Playing:
                    if (_phaseTimer <= 0f) SetPhase(RoundPhase.Overtime);
                    break;
                case RoundPhase.Overtime:
                    if (_phaseTimer <= 0f) EndRound();
                    break;
            }
        }

        public void StartCountdown()
        {
            _currentRound++;
            SetPhase(RoundPhase.Countdown);
        }

        private void SetPhase(RoundPhase newPhase)
        {
            _phase = newPhase;
            switch (newPhase)
            {
                case RoundPhase.Countdown: _phaseTimer = _countdownDuration; break;
                case RoundPhase.Playing: _phaseTimer = _roundDuration; break;
                case RoundPhase.Overtime: _phaseTimer = 60f; break;
                default: _phaseTimer = 0f; break;
            }
            this.Log($"Round phase: {_phase}");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Phase", _phase.ToString(), Color.yellow),
                new M2922_GizmoDisplayInfo("Round", $"{_currentRound} / {_roundsToWin}", Color.cyan),
                new M2922_GizmoDisplayInfo("Timer", $"{_phaseTimer:F1}s"),
            };
        }
#endif

        private void EndRound()
        {
            SetPhase(RoundPhase.Ended);
            this.Log($"Round {_currentRound} ended.");
        }
    }
}
