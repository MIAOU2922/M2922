using UdonSharp;
using UnityEngine;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Marqueur identifiant un objet comme chargeur/munition pour le MagazineWell.
    /// À placer sur tout prefab de chargeur. Aucune logique, juste un tag détectable.
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Magazine Marker")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_MagazineMarker : UdonSharpBehaviour
    {
    }
}
