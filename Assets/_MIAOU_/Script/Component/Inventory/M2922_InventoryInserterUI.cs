using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Version visuelle de la zone d'insertion : change la couleur d'une Image
    /// quand un objet rangeable entre dans la zone.
    ///
    /// SETUP : identique à M2922_InventoryInserter + assigner SpriteImage.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Inserter UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_InventoryInserterUI : M2922_InventoryInserter
    {
        [Header("=== UI ===")]
        [Tooltip("Image dont la couleur change quand un objet entre dans la zone.")]
        public Image SpriteImage;

        [Header("=== COULEURS ===")]
        [Tooltip("Couleur au repos.")]
        public Color IdleColor = Color.white;
        [Tooltip("Couleur quand un objet est prêt à être rangé.")]
        public Color DroppingColor = Color.green;

        public override void _Highlight(bool value)
        {
            base._Highlight(value);

            if (SpriteImage != null)
                SpriteImage.color = value ? DroppingColor : IdleColor;
        }
    }
}
