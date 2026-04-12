using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour toute entité pouvant entrer et sortir d'un véhicule.
    /// 
    /// RÈGLE : VehicleController appelle ces méthodes sur l'occupant.
    ///         L'occupant met à jour son état (InVehicle) en réponse.
    /// 
    /// Implémenté par : M2922_PlayerController, M2922_NpcController
    /// </summary>
    public interface IVehicleOccupant
    {
        /// <summary>L'entité est-elle actuellement dans un véhicule ?</summary>
        bool IsInVehicle { get; }

        /// <summary>
        /// GameObject du véhicule actuellement occupé (null si aucun).
        /// </summary>
        GameObject CurrentVehicle { get; }

        /// <summary>
        /// Index du siège occupé dans le véhicule (0 = pilote, 1+ = passager).
        /// -1 si pas dans un véhicule.
        /// </summary>
        int SeatIndex { get; }

        /// <summary>
        /// Appelé par VehicleController quand l'entité monte dans le véhicule.
        /// Met à jour l'état IsInVehicle et désactive certains comportements
        /// (ex: PlayerController ne spawn pas pendant qu'il est dans un véhicule).
        /// </summary>
        /// <param name="vehicleGO">GameObject du véhicule</param>
        /// <param name="seatIndex">Index du siège</param>
        void OnEnterVehicle(GameObject vehicleGO, int seatIndex);

        /// <summary>
        /// Appelé par VehicleController quand l'entité quitte le véhicule.
        /// Rétablit les comportements normaux.
        /// Peut aussi être appelé par OnPropDied() du véhicule (éjection forcée).
        /// </summary>
        void OnExitVehicle();
    }
}
