using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour toute entité gérant un inventaire d'armes et d'équipements.
    /// 
    /// RÈGLE : InventorySystem ne tire pas, ne soigne pas.
    ///         Il connaît ce que l'entité PORTE et dans quel slot.
    ///         WeaponProp reste responsable du tir.
    /// 
    /// Implémenté par : M2922_InventorySystem
    /// </summary>
    public interface IInventoryHolder
    {
        /// <summary>Nombre total de slots d'armes disponibles</summary>
        int WeaponSlotCount { get; }

        /// <summary>Index du slot d'arme actuellement actif</summary>
        int ActiveSlotIndex { get; }

        /// <summary>Au moins un slot est-il occupé ?</summary>
        bool HasAnyWeapon { get; }

        /// <summary>
        /// Tente d'ajouter une arme dans le premier slot disponible.
        /// </summary>
        /// <param name="weaponGO">GameObject de l'arme (doit avoir M2922_WeaponProp)</param>
        /// <returns>True si l'arme a été ajoutée, false si inventaire plein</returns>
        bool TryAddWeapon(GameObject weaponGO);

        /// <summary>
        /// Retire l'arme du slot indiqué et la drop au sol.
        /// </summary>
        void DropWeapon(int slotIndex);

        /// <summary>
        /// Retire et drop toutes les armes (appelé à la mort).
        /// </summary>
        void DropAll();

        /// <summary>
        /// Retourne le GameObject de l'arme dans le slot indiqué (null si vide).
        /// </summary>
        GameObject GetWeaponInSlot(int slotIndex);

        /// <summary>
        /// Retourne le GameObject de l'arme active (null si inventaire vide).
        /// </summary>
        GameObject GetActiveWeapon();

        /// <summary>
        /// Change le slot actif (ex: scroll, input 1/2/3).
        /// </summary>
        void SwitchToSlot(int slotIndex);

        /// <summary>
        /// Passe au slot suivant (cycle).
        /// </summary>
        void SwitchNext();

        /// <summary>
        /// Passe au slot précédent (cycle).
        /// </summary>
        void SwitchPrevious();
    }
}
