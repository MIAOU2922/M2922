using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Oriente le GameObject pour qu'il regarde toujours la caméra
    /// du joueur local (billboard). Utile pour barres de vie, noms, tags.
    /// </summary>
    [AddComponentMenu("M2922/Utils/Look At Camera")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_LookAtCamera : M2922_Base
    {
        [Header("=== AXES ===")]
        [Tooltip("Axe(s) à verrouiller (ex: Y seulement pour rester vertical).")]
        [SerializeField] private bool _lockX = false;
        [SerializeField] private bool _lockY = false;
        [SerializeField] private bool _lockZ = false;

        [Header("=== OPTIONS ===")]
        [Tooltip("Inverse le regard (montre le dos à la caméra).")]
        [SerializeField] private bool _flip = false;

        [Header("=== UPDATE MODE ===")]
        [Tooltip("PostLateUpdate pour suivre parfaitement. Décochez = Update.")]
        [SerializeField] private bool _useLateUpdate = false;

        private VRCPlayerApi _localPlayer;

        protected override void Start()
        {
            base.Start();
            _localPlayer = Networking.LocalPlayer;
        }

        protected override void Update()
        {
            base.Update();
            if (!_useLateUpdate) LookAtCamera();
        }

        public override void PostLateUpdate()
        {
            if (_useLateUpdate) LookAtCamera();
        }

        private void LookAtCamera()
        {
            if (_localPlayer == null) return;

            var tracking = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 dir = tracking.position - transform.position;
            if (_flip) dir = -dir;

            if (_lockX) dir.x = 0f;
            if (_lockY) dir.y = 0f;
            if (_lockZ) dir.z = 0f;

            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
