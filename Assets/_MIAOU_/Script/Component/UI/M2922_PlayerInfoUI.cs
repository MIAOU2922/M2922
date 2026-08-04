using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;

namespace M2922.Component.UI
{
    /// <summary>
    /// Entrée individuelle dans la Player List UI.
    /// Hérite de M2922_CounterUI pour le compteur +/- et ajoute :
    /// - Icône Master, toggle allowed, nom du joueur.
    /// 
    /// SETUP :
    /// 1. Placer sur un prefab "Player infos" dans la liste.
    /// 2. Assigner les références dans l'inspecteur.
    /// 3. Les boutons +/− et toggle doivent pointer via SendCustomEvent.
    /// </summary>
    [AddComponentMenu("M2922/UI/Player Info UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_PlayerInfoUI : M2922_CounterUI
    {
        [Header("=== PLAYER REFERENCES ===")]
        [Tooltip("Icône activée seulement si le joueur local est Master.")]
        [SerializeField] private GameObject _masterIcon;

        [Tooltip("Image du bouton toggle dont la couleur change selon l'état allowed.")]
        [SerializeField] private Image _toggleImage;

        [Tooltip("Couleur quand le joueur EST dans la liste allowed.")]
        [SerializeField] private Color _allowedColor = Color.green;

        [Tooltip("Couleur quand le joueur N'EST PAS dans la liste allowed.")]
        [SerializeField] private Color _notAllowedColor = Color.gray;

        [Tooltip("Texte TMP pour le nom du joueur.")]
        [SerializeField] private TextMeshProUGUI _nameText;

        // --- état joueur ---
        private VRCPlayerApi _assignedPlayer;
        private string _assignedDisplayName = "";
        private M2922_VisibilityByRole _visibilityManager;
        private bool _isSetup;

        // =============================================
        //  UNITY LIFECYCLE (override CounterUI)
        // =============================================

        protected override void Start()
        {
            base.Start(); // CounterUI.Start() → InitCounterValues()

            _localPlayer = Networking.LocalPlayer;

            // Ne désactiver que si Setup() n'a pas déjà été appelé
            // (en Udon, Instantiate peut exécuter Setup avant Start)
            if (!_isSetup)
                gameObject.SetActive(false);
        }

        protected override string GetSaveKey()
        {
            return _assignedDisplayName;
        }

        protected override void Update()
        {
            base.Update();

            if (!_isSetup) return;

            // Sync compteur depuis le store (modifié par d'autres joueurs)
            if (_counterStore != null && !string.IsNullOrEmpty(_assignedDisplayName))
            {
                int stored = _counterStore.GetCounter(_assignedDisplayName);
                if (stored >= 0 && stored != _currentValue)
                {
                    _currentValue = stored;
                    UpdateDisplay();
                }
            }

            // Mettre à jour la couleur du toggle
            if (_visibilityManager != null && _toggleImage != null)
            {
                bool isAllowed = _visibilityManager.IsPlayerAllowedByName(_assignedDisplayName);
                _toggleImage.color = isAllowed ? _allowedColor : _notAllowedColor;
            }

            // Icône Master : uniquement sur l'entrée du Master
            if (_masterIcon != null && !string.IsNullOrEmpty(_assignedDisplayName))
            {
                bool isMasterEntry = Networking.Master != null && _assignedDisplayName == Networking.Master.displayName;
                _masterIcon.SetActive(isMasterEntry);
            }
        }

        // =============================================
        //  PUBLIC — Appelé par M2922_PlayerListUI
        // =============================================

        public void Setup(VRCPlayerApi player, M2922_VisibilityByRole visibility, M2922_PlayerCounterStore store)
        {
            if (player == null) return;

            _assignedPlayer = player;
            _assignedDisplayName = player.displayName;
            _visibilityManager = visibility;
            _counterStore = store;

            if (_localPlayer == null)
                _localPlayer = Networking.LocalPlayer;

            // Nom
            if (_nameText != null)
                _nameText.text = _assignedDisplayName;

            // Initialiser le compteur (utilise GetSaveKey() → _assignedDisplayName)
            InitCounterValues();

            _isSetup = true;
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            _isSetup = false;
            _assignedPlayer = null;
            _assignedDisplayName = "";
            _currentValue = 0;

            if (_nameText != null) _nameText.text = "";
            if (_counterText != null) _counterText.text = "";
            if (_masterIcon != null) _masterIcon.SetActive(false);
            if (_toggleImage != null) _toggleImage.color = _notAllowedColor;

            gameObject.SetActive(false);
        }

        /// <summary>Met à jour le VRCPlayerApi quand un joueur reconnecte.</summary>
        public void Reassign(VRCPlayerApi player)
        {
            if (player == null) return;
            _assignedPlayer = player;
            _assignedDisplayName = player.displayName;
            _isSetup = true;
            gameObject.SetActive(true);
        }

        public bool IsAssignedTo(VRCPlayerApi player)
        {
            return player != null && _assignedPlayer != null && _assignedPlayer.playerId == player.playerId;
        }

        public bool IsAssignedToName(string displayName)
        {
            return !string.IsNullOrEmpty(_assignedDisplayName) && _assignedDisplayName == displayName;
        }

        public bool IsFree()
        {
            return string.IsNullOrEmpty(_assignedDisplayName);
        }

        // =============================================
        //  PUBLIC — Boutons (SendCustomEvent)
        // =============================================

        public void ToggleAllowed()
        {
            if (_visibilityManager == null || string.IsNullOrEmpty(_assignedDisplayName)) return;
            if (_localPlayer == null) _localPlayer = Networking.LocalPlayer;
            if (_localPlayer == null || !_localPlayer.isMaster) return;

            if (_visibilityManager.IsPlayerAllowedByName(_assignedDisplayName))
                _visibilityManager.RemovePlayer(_assignedDisplayName);
            else
                _visibilityManager.AddPlayer(_assignedDisplayName);
        }
    }
}

