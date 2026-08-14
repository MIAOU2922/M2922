using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Interactable qui DONNE des items au joueur (une seule utilisation par monde).
    ///
    /// SETUP : sur un GameObject avec un VRC Interactable (Desktop ou VR pickup).
    /// Items = objets à donner. Inventory = l'inventaire de la scène.
    ///
    /// Sync : [UdonSynced] Used — l'interactable est désactivé pour tous après usage.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item Giver")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_InventoryItemGiver : M2922_Base
    {
        [Header("=== RÉFÉRENCES ===")]
        [Tooltip("Items donnés au joueur quand il interagit.")]
        public M2922_InventoryItem[] Items;
        [Tooltip("Inventaire cible.")]
        public M2922_Inventory Inventory;

        [Header("=== ÉTAT SYNCHRONISÉ ===")]
        [UdonSynced] public bool Used;

        protected override void Start()
        {
            base.Start();

            // Cache les items que le joueur local possède déjà (état conservé
            // après rejoin : l'objet reste rangé pour son propriétaire).
            foreach (M2922_InventoryItem item in Items)
            {
                if (item == null) continue;
                if (Networking.LocalPlayer.IsOwner(item.gameObject))
                {
                    item._Hide();
                }
            }
        }

        public override void OnDeserialization()
        {
            DisableInteractive = Used;
        }

        public override void Interact()
        {
            if (Used) return;

            // Pré-vérification : si l'inventaire est trop plein (limite de poids),
            // on n'annule PAS le giver — le joueur peut réessayer plus tard.
            foreach (M2922_InventoryItem item in Items)
            {
                if (item == null) continue;
                if (Inventory != null && !Inventory._CanStoreWeight(item))
                {
                    this.Warning($"[Giver] Inventaire plein ({Inventory._GetTotalWeight()}/{Inventory.MaxWeight}), items non donnés.");
                    return;
                }
            }

            foreach (M2922_InventoryItem item in Items)
            {
                if (item == null) continue;
                if (Inventory != null) Inventory._AddItem(item);
            }

            Used = true;
            RequestSerialization();
            OnDeserialization();
        }
    }
}
