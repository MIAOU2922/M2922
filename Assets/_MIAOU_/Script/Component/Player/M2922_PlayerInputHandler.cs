using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Weapon;
using M2922.Component.Physics;

namespace M2922.Component.Player
{
    /// <summary>
    /// Gestion des inputs joueur : tir, interaction, rechargement.
    /// </summary>
    [AddComponentMenu("M2922/Player/Player Input Handler")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PlayerInputHandler : M2922_Base
    {
        [Header("=== INPUT KEYS ===")]
        [SerializeField] private KeyCode _fireKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode _reloadKey = KeyCode.R;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_FireController _fireController;
        [SerializeField] private M2922_ReloadComponent _reloadComponent;
        [SerializeField] private M2922_MovementComponent _movement;

        public bool FirePressed { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool JumpPressed { get; private set; }

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_fireController == null) _fireController = GetComponent<M2922_FireController>();
            if (_reloadComponent == null) _reloadComponent = GetComponent<M2922_ReloadComponent>();
            if (_movement == null) _movement = GetComponent<M2922_MovementComponent>();
        }

        protected override void Update()
        {
            base.Update();

            FirePressed = Input.GetKey(_fireKey);
            ReloadPressed = Input.GetKeyDown(_reloadKey);
            InteractPressed = Input.GetKeyDown(_interactKey);
            JumpPressed = Input.GetKeyDown(_jumpKey);

            // Transmet aux composants
            if (_fireController != null)
                _fireController.TriggerHeld = FirePressed;

            if (_reloadComponent != null && ReloadPressed)
                _reloadComponent.StartReload();

            if (_movement != null)
            {
                _movement.WantsJump = JumpPressed;
                _movement.WantsSprint = Input.GetKey(KeyCode.LeftShift);
                _movement.MoveDirection = new Vector3(
                    Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Fire", _fireKey.ToString()),
                new M2922_GizmoDisplayInfo("Reload", _reloadKey.ToString()),
                new M2922_GizmoDisplayInfo("Interact", _interactKey.ToString()),
            };
        }
#endif
    }
}
