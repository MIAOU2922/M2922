using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Network
{
    /// <summary>
    /// Synchronisation des variables importantes entre joueurs.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_SyncComponent : M2922_Base
    {
        [Header("=== SYNC INTERVAL ===")]
        [SerializeField] private float _syncInterval = 0.1f; // 10 Hz
        [SerializeField] private float _positionThreshold = 0.01f;

        [Header("=== SYNCED DATA ===")]
        [UdonSynced] private Vector3 _syncedPosition;
        [UdonSynced] private Quaternion _syncedRotation;

        private float _lastSyncTime = 0f;
        private Vector3 _lastSentPosition;
        private Quaternion _lastSentRotation;

        protected override void Update()
        {
            base.Update();

            if (!Networking.IsOwner(gameObject)) return;

            if (Time.time - _lastSyncTime > _syncInterval)
            {
                bool changed = Vector3.Distance(transform.position, _lastSentPosition) > _positionThreshold
                            || Quaternion.Angle(transform.rotation, _lastSentRotation) > 1f;

                if (changed)
                {
                    _syncedPosition = transform.position;
                    _syncedRotation = transform.rotation;
                    _lastSentPosition = transform.position;
                    _lastSentRotation = transform.rotation;
                    RequestSerialization();
                }

                _lastSyncTime = Time.time;
            }
        }

        public override void OnDeserialization()
        {
            if (!Networking.IsOwner(gameObject))
            {
                transform.position = Vector3.Lerp(transform.position, _syncedPosition, 0.3f);
                transform.rotation = Quaternion.Lerp(transform.rotation, _syncedRotation, 0.3f);
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Interval", $"{_syncInterval}s"),
                new M2922_GizmoDisplayInfo("Threshold", $"{_positionThreshold:F2}"),
            };
        }
#endif
    }
}
