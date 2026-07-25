using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Player
{
    [AddComponentMenu("M2922/Player/Bone Follower")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class M2922_BoneFollower : M2922_Base
    {
        [Header("=== BONE TRACKING ===")]
        [Tooltip("Os humanoid à suivre sur le joueur local.")]
        public HumanBodyBones trackedBone = HumanBodyBones.Hips;

        [Tooltip("Si coché, suit aussi la rotation. Décocher pour ne suivre que la position.")]
        public bool trackRotation = true;

        [Tooltip("Si coché, suit aussi la position. Décocher pour ne suivre que la rotation.")]
        public bool trackPosition = true;

        [Header("=== OFFSET ===")]
        [Tooltip("Décalage local par rapport à la position de l'os suivi.")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Décalage local par rapport à la rotation de l'os suivi.")]
        public Vector3 rotationOffset = Vector3.zero;

        // Privé
        private VRCPlayerApi _localPlayer;
        private bool _isInEditor;

        protected override void Start()
        {
            base.Start();
            _localPlayer = Networking.LocalPlayer;
            _isInEditor = _localPlayer == null;
        }

        protected override void Update()
        {
            if (_isInEditor) return;
            if (_localPlayer == null) return;

            Vector3 targetPos = trackPosition
                ? _localPlayer.GetBonePosition(trackedBone)
                : transform.position;

            Quaternion targetRot = trackRotation
                ? _localPlayer.GetBoneRotation(trackedBone)
                : transform.rotation;

            // Appliquer les offsets
            if (trackPosition && positionOffset != Vector3.zero)
                targetPos += targetRot * positionOffset;

            if (trackRotation && rotationOffset != Vector3.zero)
                targetRot *= Quaternion.Euler(rotationOffset);

            if (trackPosition && trackRotation)
                transform.SetPositionAndRotation(targetPos, targetRot);
            else if (trackPosition)
                transform.position = targetPos;
            else if (trackRotation)
                transform.rotation = targetRot;
        }
    }
}
