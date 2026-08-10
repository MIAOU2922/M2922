using UdonSharp;
using UnityEngine;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Marqueur indiquant qu'une entité (véhicule, plateforme mobile…)
    /// doit embarquer le joueur local avec elle lors d'une téléportation.
    ///
    /// À placer sur le GameObject racine du véhicule.
    /// Sans ce marker, une entité qui entre dans un M2922_Teleporter
    /// est téléportée SEULE (le joueur à côté n'est pas affecté).
    ///
    /// Le rayon de détection du rider est configuré dans le Teleporter
    /// (_riderTeleportRadius). Ce marker active seulement la détection.
    ///
    /// EXEMPLE :
    ///   • Voiture avec M2922_TeleportRiderMarker → tp véhicule + conducteur
    ///   • Arme lancée SANS marker → tp l'arme seule (même si joueur proche)
    /// </summary>
    [AddComponentMenu("M2922/Utils/Teleport Rider Marker")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_TeleportRiderMarker : UdonSharpBehaviour
    {
    }
}
