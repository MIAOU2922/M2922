using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Zone d'insertion de l'inventaire : quand le joueur y lâche un objet
    /// portant un M2922_InventoryProxy, l'objet est rangé dans l'inventaire.
    ///
    /// SETUP : Collider en Trigger sur ce GameObject (pas besoin de Rigidbody
    /// sur la zone : le pickup déposé en a déjà un, un trigger statique suffit).
    /// Le collider de l'objet doit porter le M2922_InventoryProxy.
    ///
    /// PERF : 100% événementiel (OnTriggerEnter/Exit, aucun Update).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Inserter")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_InventoryInserter : M2922_Base
    {
        [Header("=== RÉFÉRENCES ===")]
        [Tooltip("Inventaire lié à cette zone (personnel ou MONDE). Assigné automatiquement au proxy des objets qui entrent — aucun réglage sur les items.")]
        public M2922_Inventory Inventory;

        /// <summary>
        /// Feedback quand un objet entre/sort de la zone.
        /// Surchargé par M2922_InventoryInserterUI (feedback visuel).
        /// </summary>
        public virtual void _Highlight(bool value)
        {
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Utilities.IsValid(other)) return;
            if (!Networking.LocalPlayer.IsOwner(other.gameObject)) return;

            M2922_InventoryProxy proxy = other.GetComponent<M2922_InventoryProxy>();
            if (!proxy) return;

            // L'inventaire de CETTE zone est transmis au proxy au runtime :
            // l'item n'a aucune référence à configurer manuellement.
            proxy.Inventory = Inventory;
            proxy.InsertingToInventory = true;
            _Highlight(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Utilities.IsValid(other)) return;
            if (!Networking.LocalPlayer.IsOwner(other.gameObject)) return;

            M2922_InventoryProxy proxy = other.GetComponent<M2922_InventoryProxy>();
            if (!proxy) return;

            proxy.InsertingToInventory = false;
            _Highlight(false);
        }
    }
}
