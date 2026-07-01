using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Player
{
    /// <summary>
    /// Bridge entre VRCPlayerApi et le système de gameplay.
    /// </summary>
    [AddComponentMenu("M2922/Player/Player Controller")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PlayerController : M2922_Base
    {
        [Header("=== PLAYER ===")]
        [SerializeField] private VRCPlayerApi _localPlayer;

        [Header("=== COMPONENTS ===")]
        [SerializeField] private M2922_HealthComponent _health;
        [SerializeField] private M2922_PlayerStats _stats;
        [SerializeField] private M2922_PlayerInventory _playerInventory;
        [SerializeField] private M2922_PlayerInputHandler _inputHandler;

        public VRCPlayerApi LocalPlayer => _localPlayer;
        public bool IsLocal => _localPlayer != null && _localPlayer.isLocal;

        protected override void Start()
        {
            base.Start();
            _localPlayer = Networking.LocalPlayer;

            if (IsLocal)
                this.Log($"Local player initialized: {_localPlayer.displayName}");
        }

        protected override void AutoDetectReferences()
        {
            if (_health == null) _health = GetComponent<M2922_HealthComponent>();
            if (_stats == null) _stats = GetComponent<M2922_PlayerStats>();
            if (_playerInventory == null) _playerInventory = GetComponent<M2922_PlayerInventory>();
            if (_inputHandler == null) _inputHandler = GetComponent<M2922_PlayerInputHandler>();
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (player.isLocal && _health != null)
                _health.Revive();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("IsLocal", IsLocal ? "YES" : "NO", IsLocal ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Health", _health != null ? "Linked" : "None"),
                new M2922_GizmoDisplayInfo("Stats", _stats != null ? "Linked" : "None"),
            };
        }
#endif
    }
}
