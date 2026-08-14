using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Bouton d'un item dans la liste : affiche nom + icône et sélectionne
    /// l'item au clic.
    ///
    /// SETUP : sur la racine du prefab de bouton (avec un Button Unity).
    /// Lier _OnClick au onClick du Button.
    ///
    /// PERF : 100% événementiel (M2922_Base, aucun Update).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Button UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_InventoryButtonUI : M2922_Base
    {
        [Header("=== UI ===")]
        [Tooltip("Image de l'icône de l'item.")]
        public Image ItemIcon;
        [Tooltip("Texte du nom de l'item.")]
        public TextMeshProUGUI ItemName;

        [Header("=== RUNTIME (lecture seule) ===")]
        public M2922_Inventory Inventory;
        public DataDictionary DataItem;

        /// <summary>Initialisé par M2922_Inventory._AddItem au moment de l'instanciation.</summary>
        public void _Init(M2922_Inventory inventory, DataDictionary dataItem)
        {
            Inventory = inventory;
            DataItem = dataItem;

            M2922_InventoryItem item = (M2922_InventoryItem)dataItem[M2922_Inventory.ID_ITEM].Reference;

            if (ItemName != null) ItemName.text = item.ItemName;
            if (ItemIcon != null) ItemIcon.sprite = item.Icon;
        }

        /// <summary>À lier au onClick du Button.</summary>
        public void _OnClick()
        {
            if (Inventory != null)
                Inventory._SelectItem(DataItem);
        }
    }
}
