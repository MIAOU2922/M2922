using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// Interface pour tout ce qui peut être interagi
    /// (armes à ramasser, véhicules, portes, boutons)
    public interface IInteractable
    {
        bool CanInteract { get; }
        string InteractionText { get; }
        float InteractionRange { get; }
        void OnInteract(VRCPlayerApi player);
        void OnLookAt(VRCPlayerApi player);
        void OnLookAway(VRCPlayerApi player);
    }
}
