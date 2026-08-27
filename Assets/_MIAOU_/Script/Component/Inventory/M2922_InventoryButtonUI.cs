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
        [Tooltip("OPTIONNEL : TMP affichant le nombre d'items dans le stack (masqué si 1).")]
        public TextMeshProUGUI ItemCount;

        [Header("=== STATUT (optionnel) ===")]
        [Tooltip("OPTIONNEL : TMP affichant l'état de l'item — utilisé par le menu admin de la map.")]
        public TextMeshProUGUI ItemStatus;
        [Tooltip("Couleur du statut quand l'item est visible dans le monde.")]
        public Color StatusActiveColor = new Color(0.30f, 0.85f, 0.35f, 1f);
        [Tooltip("Couleur du statut quand l'item est rangé / absent.")]
        public Color StatusInactiveColor = new Color(0.95f, 0.70f, 0.20f, 1f);

        [Header("=== RUNTIME (lecture seule) ===")]
        public M2922_Inventory Inventory;
        [HideInInspector] public DataDictionary DataItem;

        /// <summary>Initialisé par M2922_Inventory._AddItem au moment de l'instanciation.</summary>
        public void _Init(M2922_Inventory inventory, DataDictionary dataItem)
        {
            Inventory = inventory;
            DataItem = dataItem;
            if (dataItem == null) return;

            DataToken itemToken;
            if (!dataItem.TryGetValue(M2922_Inventory.ID_ITEM, out itemToken) || itemToken.TokenType != TokenType.Reference)
            {
                this.Warning("[ButtonUI] _Init ignoré : entrée sans item valide.");
                return;
            }

            M2922_InventoryItem item = (M2922_InventoryItem)itemToken.Reference;

            if (ItemName != null) ItemName.text = item.ItemName;
            if (ItemIcon != null) ItemIcon.sprite = item.Icon;

            DataToken stackToken;
            DataList stack = null;
            if (dataItem.TryGetValue(M2922_Inventory.ID_STACK, out stackToken) && stackToken.TokenType == TokenType.DataList)
                stack = stackToken.DataList;

            _RefreshCount(stack != null ? stack.Count : 1);
        }

        /// <summary>Met à jour l'affichage du compteur du stack.</summary>
        public void _RefreshCount(int count)
        {
            if (ItemCount == null) return;

            if (count <= 1)
            {
                ItemCount.text = string.Empty;
                ItemCount.gameObject.SetActive(false);
            }
            else
            {
                ItemCount.text = $"x{count}";
                ItemCount.gameObject.SetActive(true);
            }
        }

        /// <summary>Met à jour l'affichage optionnel de l'état (✔ dans le monde / ✘ rangé).</summary>
        public void _SetStatus(bool active)
        {
            if (ItemStatus == null) return;

            ItemStatus.text = active ? "✔ Dans le monde" : "✘ Rangé / absent";
            ItemStatus.color = active ? StatusActiveColor : StatusInactiveColor;
        }

        /// <summary>À lier au onClick du Button.</summary>
        public void _OnClick()
        {
            if (Inventory != null)
                Inventory._SelectItem(DataItem);
        }
    }
}
