using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Network
{
    /// <summary>
    /// Compensation de lag simplifiée pour les hits.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_LagCompensation : M2922_Base
    {
        [Header("=== LAG COMPENSATION ===")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private float _maxRewindTime = 0.2f;
        [SerializeField] private int _maxHistoryFrames = 20;

        // Parallel arrays au lieu d'un struct (UdonSharp ne supporte pas les champs sur les structs)
        private Vector3[] _positions;
        private float[] _times;
        private int _historyIndex = 0;

        protected override void Start()
        {
            base.Start();
            _positions = new Vector3[_maxHistoryFrames];
            _times = new float[_maxHistoryFrames];
        }

        protected override void Update()
        {
            base.Update();
            _positions[_historyIndex] = transform.position;
            _times[_historyIndex] = Time.time;
            _historyIndex = (_historyIndex + 1) % _maxHistoryFrames;
        }

        /// <summary>Tente de retrouver la position au temps donné.</summary>
        public bool TryGetPositionAtTime(float targetTime, out Vector3 position)
        {
            position = transform.position;

            if (!_enabled) return false;

            Vector3 bestPos = _positions[0];
            float bestDiff = float.MaxValue;

            for (int i = 0; i < _maxHistoryFrames; i++)
            {
                float diff = Mathf.Abs(_times[i] - targetTime);
                if (diff < bestDiff && _times[i] > 0)
                {
                    bestDiff = diff;
                    bestPos = _positions[i];
                }
            }

            if (bestDiff < _maxRewindTime)
            {
                position = bestPos;
                return true;
            }

            return false;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Enabled", _enabled ? "ON" : "OFF", _enabled ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Rewind", $"{_maxRewindTime}s"),
                new M2922_GizmoDisplayInfo("Frames", $"{_maxHistoryFrames}"),
            };
        }
#endif
    }
}
