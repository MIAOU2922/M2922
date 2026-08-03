using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using M2922.Component.Weapon;

namespace M2922.Component.UI
{
    /// <summary>
    /// Affiche les munitions (current / max) dans un TMP.
    /// 
    /// SETUP : Mettre ce script sur un GameObject avec un TextMeshProUGUI.
    /// La Weapon/FireHandler est auto-détectée dans les parents.
    /// </summary>
    [AddComponentMenu("M2922/UI/Ammo UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_AmmoUI : UdonSharpBehaviour
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("Arme à observer (auto-détectée si vide).")]
        [SerializeField] private M2922_Weapon _weapon;
        [Tooltip("Texte TMP (auto-détecté si vide).")]
        [SerializeField] private TextMeshProUGUI _ammoText;

        [Header("=== DISPLAY ===")]
        [Tooltip("Texte pour munitions infinies.")]
        [SerializeField] private string _infiniteText = "\u221E";
        [Tooltip("Seuil warning orange (0-1, 0 = désactivé).")]
        [SerializeField] private float _warningOrangePct = 0.50f;
        [Tooltip("Seuil warning rouge (0-1).")]
        [SerializeField] private float _warningRedPct = 0.25f;
        [Tooltip("Couleur normal (≥ orange).")]
        [SerializeField] private Color _greenColor = Color.green;
        [Tooltip("Couleur warning orange.")]
        [SerializeField] private Color _orangeColor = new Color(1f, 0.55f, 0f);
        [Tooltip("Couleur warning rouge.")]
        [SerializeField] private Color _redColor = Color.red;

        private Color _defaultColor = Color.white;

        private void Start()
        {
            if (_ammoText == null)
                _ammoText = GetComponent<TextMeshProUGUI>();

            if (_ammoText != null)
                _defaultColor = _ammoText.color;

            if (_weapon == null)
                _weapon = GetComponentInParent<M2922_Weapon>();
        }

        private void Update()
        {
            if (_ammoText == null || _weapon == null) return;

            if (_weapon.InfiniteAmmo)
            {
                _ammoText.text = _infiniteText;
                _ammoText.color = _defaultColor;
            }
            else
            {
                int cur = _weapon.CurrentAmmo;
                int max = _weapon.Magazine;
                _ammoText.text = cur.ToString() + " / " + max.ToString();

                float pct = max > 0 ? ((float)cur / (float)max) : 1f;
                if (pct <= _warningRedPct)
                    _ammoText.color = _redColor;
                else if (pct <= _warningOrangePct)
                    _ammoText.color = _orangeColor;
                else
                    _ammoText.color = _greenColor;
            }
        }
    }
}
