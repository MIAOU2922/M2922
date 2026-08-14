using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Interactable qui CONSOMME des items de l'inventaire du joueur local.
    /// Utile pour les quêtes / portes / échanges ("donne-moi 3 Pommes").
    ///
    /// SETUP : sur un GameObject avec un VRC Interactable.
    /// ItemsToRemove = noms des items requis (doublons = quantités).
    /// Si TOUS les items ne sont pas présents, rien n'est consommé.
    ///
    /// PERF : 100% événementiel. Local uniquement (inventaire du joueur).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item Requester")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_InventoryItemRequester : M2922_Base
    {
        [Header("=== RÉFÉRENCES ===")]
        [Tooltip("Inventaire de la scène.")]
        public M2922_Inventory Inventory;

        [Header("=== ITEMS REQUIS ===")]
        [Tooltip("Noms des items à consommer (doublons possibles pour les quantités).")]
        public string[] ItemsToRemove = { "Apple", "Apple", "Apple" };

        public override void Interact()
        {
            if (Inventory == null)
            {
                this.Error("[Requester] Aucun M2922_Inventory assigné !");
                return;
            }

            DataList foundItemList = new DataList();
            DataList cachedItemList = Inventory.ItemList.DeepClone();

            for (int x = 0; x < ItemsToRemove.Length; x++)
            {
                string itemName = ItemsToRemove[x];
                for (int y = 0; y < cachedItemList.Count; y++)
                {
                    M2922_InventoryItem item = (M2922_InventoryItem)cachedItemList[y].DataDictionary[M2922_Inventory.ID_ITEM].Reference;
                    if (item.ItemName == itemName)
                    {
                        foundItemList.Add(cachedItemList[y].DataDictionary);
                        cachedItemList.Remove(cachedItemList[y].DataDictionary);
                        break;
                    }
                }
            }

            if (foundItemList.Count != ItemsToRemove.Length)
            {
                this.Warning($"[Requester] Items manquants : {foundItemList.Count}/{ItemsToRemove.Length}.");
                return;
            }

            for (int i = 0; i < foundItemList.Count; i++)
            {
                M2922_InventoryItem item = (M2922_InventoryItem)foundItemList[i].DataDictionary[M2922_Inventory.ID_ITEM].Reference;
                Inventory._RemoveItem(item);
            }

            this.Log($"[Requester] {ItemsToRemove.Length} item(s) consommé(s).");
        }
    }
}
