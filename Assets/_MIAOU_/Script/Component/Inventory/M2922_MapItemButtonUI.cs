using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// ⚠ LEGACY : bouton de l'ancienne version du menu admin de la map.
    /// Le menu utilise désormais M2922_InventoryButtonUI (comme tous les
    /// inventaires) — préférez-le pour tout nouveau setup. Conservé pour
    /// compatibilité avec les prefabs existants.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Map Item Button UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_MapItemButtonUI : M2922_Base
    {
        [Header("=== UI ===")]
        [Tooltip("Image de l'icône de l'item.")]
        public Image ItemIcon;
        [Tooltip("Texte du nom de l'item.")]
        public TextMeshProUGUI ItemName;
        [Tooltip("OPTIONNEL : TMP affichant l'état de l'item (dans le monde / rangé).")]
        public TextMeshProUGUI ItemStatus;

        [Header("=== COULEURS D'ÉTAT ===")]
        [Tooltip("Couleur du statut quand l'item est visible dans le monde.")]
        public Color StatusActiveColor = new Color(0.30f, 0.85f, 0.35f, 1f);
        [Tooltip("Couleur du statut quand l'item est rangé / absent.")]
        public Color StatusInactiveColor = new Color(0.95f, 0.70f, 0.20f, 1f);

        [Header("=== RUNTIME (lecture seule) ===")]
        public M2922_MapItemMenu Menu;
        [HideInInspector] public DataDictionary DataItem;

        /// <summary>Initialisé par M2922_MapItemMenu._RebuildMenu au moment de l'instanciation.</summary>
        public void _Init(M2922_MapItemMenu menu, DataDictionary dataItem)
        {
            Menu = menu;
            DataItem = dataItem;

            if (ItemName != null)
                ItemName.text = dataItem[M2922_MapItemMenu.ID_NAME].String;

            if (ItemIcon != null && dataItem.ContainsKey(M2922_MapItemMenu.ID_ITEM))
            {
                M2922_InventoryItem item = (M2922_InventoryItem)dataItem[M2922_MapItemMenu.ID_ITEM].Reference;
                if (item != null)
                    ItemIcon.sprite = item.Icon;
            }

            _SetStatus(false);
        }

        /// <summary>Met à jour l'affichage de l'état (dans le monde / rangé).</summary>
        public void _SetStatus(bool active)
        {
            if (ItemStatus == null) return;

            ItemStatus.text = active ? "✔ Dans le monde" : "✘ Rangé / absent";
            ItemStatus.color = active ? StatusActiveColor : StatusInactiveColor;
        }

        /// <summary>À lier au onClick du Button.</summary>
        public void _OnClick()
        {
            if (Menu != null)
                Menu._OnButtonClicked(DataItem);
        }
    }
}
