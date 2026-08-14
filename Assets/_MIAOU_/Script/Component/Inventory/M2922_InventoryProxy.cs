using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Relay des events Pickup/Drop de l'objet vers l'inventaire.
    ///
    /// SETUP : placer sur le MÊME GameObject que le collider du VRCPickup de l'objet.
    /// Item = auto-détecté (remonte la hiérarchie jusqu'au M2922_InventoryItem).
    /// Inventory = assigné AUTOMATIQUEMENT par la zone d'insertion à l'entrée de l'objet.
    ///
    /// PERF : 100% événementiel (M2922_Base, aucun Update).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Pickup Proxy")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_InventoryProxy : M2922_Base
    {
        [Header("=== RÉFÉRENCES ===")]
        [Tooltip("L'item d'inventaire associé à cet objet (auto-détecté via GetComponentInParent).")]
        public M2922_InventoryItem Item;
        [Tooltip("Inventaire cible — assigné automatiquement par la zone d'insertion (M2922_InventoryInserter).")]
        public M2922_Inventory Inventory;

        [Header("=== RUNTIME (lecture seule) ===")]
        [Tooltip("True tant que l'objet est dans la zone d'insertion.")]
        public bool InsertingToInventory;

        protected override void AutoDetectReferences()
        {
            // Le proxy est sur le collider (enfant), l'item est sur la racine de l'objet.
            if (Item == null)
                Item = GetComponentInParent<M2922_InventoryItem>();
        }

        public override void OnPickup()
        {
            if (Item == null) return;
            if (!Item.JustSpawned) return;

            Item.JustSpawned = false;
            Item._RunFirstPickupAfterSpawn();
        }

        public override void OnDrop()
        {
            if (!InsertingToInventory) return;
            InsertingToInventory = false;

            if (Item != null && Inventory != null)
                Inventory._AddItem(Item);
        }
    }
}
