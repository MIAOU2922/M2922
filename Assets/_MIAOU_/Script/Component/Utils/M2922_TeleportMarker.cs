using UdonSharp;
using UnityEngine;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Type d'entité pour le filtrage des téléporteurs.
    /// </summary>
    public enum TeleportEntityType
    {
        NPC     = 0,
        Vehicle = 1,
        Physics = 2,
        Object  = 3,
    }

    /// <summary>
    /// Marqueur à placer sur le GameObject racine d'une entité téléportable.
    ///
    /// Définit le TYPE de l'entité (NPC, Vehicle, Physics) pour que le
    /// M2922_Teleporter puisse filtrer sans dépendre des autres composants
    /// (plus de GetComponent<M2922_DamageReceiver>, M2922_Weapon, etc.).
    ///
    /// Sert AUSSI de rider marker : si le joueur local est à portée
    /// (_riderTeleportRadius) d'une entité marquée, il est embarqué avec elle.
    ///
    /// EXEMPLES :
    ///   • NPC         → M2922_TeleportMarker (NPC) sur le GO du NPC
    ///   • Voiture     → M2922_TeleportMarker (Vehicle) sur le GO racine
    ///   • Caisse      → M2922_TeleportMarker (Physics) sur le GO
    ///   • Objet       → M2922_TeleportMarker (Object) sur le GO (pas de physique)
    /// </summary>
    [AddComponentMenu("M2922/Utils/Teleport Marker")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_TeleportMarker : UdonSharpBehaviour
    {
        [Tooltip("Type de l'entité pour le filtrage du téléporteur.")]
        [SerializeField] private TeleportEntityType _entityType = TeleportEntityType.Vehicle;

        public TeleportEntityType EntityType => _entityType;
    }
}
