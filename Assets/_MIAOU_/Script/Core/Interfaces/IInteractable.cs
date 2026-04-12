using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour tout ce qui peut être interagi
    /// (armes à ramasser, véhicules, portes, boutons)
    /// </summary>
    public interface IInteractable
    {
        /// <summary>L'objet peut-il être interagi ?</summary>
        bool CanInteract { get; }
        
        /// <summary>Texte à afficher pour l'interaction (ex: "Appuyer E pour ramasser")</summary>
        string InteractionText { get; }
        
        /// <summary>Distance maximale d'interaction</summary>
        float InteractionRange { get; }
        
        /// <summary>
        /// Appelé quand un joueur interagit
        /// </summary>
        /// <param name="player">Le joueur VRC qui interagit</param>
        void OnInteract(VRCPlayerApi player);
        
        /// <summary>
        /// Appelé quand le joueur regarde l'objet (pour afficher le texte)
        /// </summary>
        void OnLookAt(VRCPlayerApi player);
        
        /// <summary>
        /// Appelé quand le joueur ne regarde plus l'objet
        /// </summary>
        void OnLookAway(VRCPlayerApi player);
    }
}
