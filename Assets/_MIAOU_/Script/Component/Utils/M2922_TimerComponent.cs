using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Cooldowns génériques : utilisable pour tirs, sorts, dash, etc.
    /// </summary>
    [AddComponentMenu("M2922/Utils/Timer Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_TimerComponent : M2922_Base
    {
        [Header("=== TIMERS ===")]
        [SerializeField] private int _maxTimers = 8;

        private float[] _timers;
        private float[] _durations;

        protected override void Start()
        {
            base.Start();
            _timers = new float[_maxTimers];
            _durations = new float[_maxTimers];
        }

        /// <summary>Démarre un timer.</summary>
        public void StartTimer(int index, float duration)
        {
            if (index < 0 || index >= _maxTimers) return;
            _timers[index] = Time.time;
            _durations[index] = duration;
        }

        /// <summary>Vérifie si le timer est terminé.</summary>
        public bool IsReady(int index)
        {
            if (index < 0 || index >= _maxTimers) return false;
            return Time.time - _timers[index] >= _durations[index];
        }

        /// <summary>Retourne la progression (0 à 1).</summary>
        public float GetProgress(int index)
        {
            if (index < 0 || index >= _maxTimers || _durations[index] <= 0f) return 1f;
            return Mathf.Clamp01((Time.time - _timers[index]) / _durations[index]);
        }

        /// <summary>Temps restant.</summary>
        public float GetRemaining(int index)
        {
            if (index < 0 || index >= _maxTimers) return 0f;
            return Mathf.Max(0f, _durations[index] - (Time.time - _timers[index]));
        }

        /// <summary>Reset un timer.</summary>
        public void Reset(int index)
        {
            if (index < 0 || index >= _maxTimers) return;
            _timers[index] = 0f;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Timers", $"{_maxTimers} slots"),
            };
        }
#endif
    }
}
