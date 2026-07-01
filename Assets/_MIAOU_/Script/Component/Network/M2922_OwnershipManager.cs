using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Network
{
    /// <summary>
    /// Gestion de l'ownership Udon : critique pour la synchro réseau.
    /// </summary>
    [AddComponentMenu("M2922/Network/Ownership Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_OwnershipManager : M2922_Base
    {
        [Header("=== OWNERSHIP ===")]
        [SerializeField] private bool _takeOwnershipOnInteract = true;

        private VRCPlayerApi _currentOwner;

        public VRCPlayerApi CurrentOwner => _currentOwner;
        public bool IsOwner => _currentOwner != null && _currentOwner.isLocal;

        public void RequestOwnership(VRCPlayerApi player)
        {
            if (!Networking.IsOwner(player, gameObject))
            {
                Networking.SetOwner(player, gameObject);
            }
            _currentOwner = player;
        }

        public void ReleaseOwnership()
        {
            _currentOwner = null;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi newOwner)
        {
            _currentOwner = newOwner;
            string ownerName = newOwner != null ? newOwner.displayName : "null";
            this.Log($"Ownership transferred to: {ownerName}");
        }

        public bool CanInteract(VRCPlayerApi player)
        {
            if (!_takeOwnershipOnInteract) return true;
            if (_currentOwner == null) return true;
            return _currentOwner.playerId == player.playerId;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            string owner = _currentOwner != null ? _currentOwner.displayName : "None";
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Owner", owner, _currentOwner != null ? Color.yellow : Color.gray),
                new M2922_GizmoDisplayInfo("Take On Interact", _takeOwnershipOnInteract.ToString()),
            };
        }
#endif
    }
}
