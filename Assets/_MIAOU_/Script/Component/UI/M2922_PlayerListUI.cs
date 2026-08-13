using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.UI
{
    /// <summary>
    /// Gestionnaire de la liste des joueurs dans l'UI.
    /// Instantie dynamiquement un M2922_PlayerInfoUI depuis un prefab
    /// à chaque nouveau joueur. Les entrées persistent pour toute
    /// la durée de l'instance (même après départ du joueur).
    /// 
    /// SETUP :
    /// 1. Placer ce script sur le GameObject parent de la liste.
    /// 2. Assigner le M2922_VisibilityByRole et le M2922_PlayerCounterStore.
    /// 3. Assigner le prefab PlayerInfoUI et le Transform parent de la liste.
    /// </summary>
    [AddComponentMenu("M2922/UI/Player List UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_PlayerListUI : M2922_Tickable
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("Manager de visibilité pour la liste allowed.")]
        [SerializeField] private M2922_VisibilityByRole _visibilityManager;

        [Tooltip("Store persistant pour les compteurs.")]
        [SerializeField] private M2922_PlayerCounterStore _counterStore;

        [Header("=== PREFAB ===")]
        [Tooltip("Prefab contenant le composant M2922_PlayerInfoUI.")]
        [SerializeField] private GameObject _playerInfoPrefab;

        [Tooltip("Transform parent où les entrées seront instanciées.")]
        [SerializeField] private Transform _listParent;

        [Header("=== VISIBILITY ===")]
        [Tooltip("GameObject à show/hide selon si le joueur local est allowed. Vide = ce gameObject.")]
        [SerializeField] private GameObject _panelRoot;

        private VRCPlayerApi _localPlayer;
        private bool _initialized;
        private bool _wasVisible;
        private int _lastKnownVersion = -1;

        // =============================================
        //  UNITY LIFECYCLE
        // =============================================

        protected override void Start()
        {
            base.Start();

            _localPlayer = Networking.LocalPlayer;

            // === FORCE HIDE immédiat pour éviter le flash d'une frame ===
            // Le panel sera réactivé par RefreshPanelVisibility() si le joueur est allowed.
            {
                GameObject target = _panelRoot != null ? _panelRoot : gameObject;
                if (target != null)
                    target.SetActive(false);
            }

            // Nettoyer les enfants existants (restes d'une session précédente en éditeur)
            if (_listParent != null)
            {
                for (int i = _listParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(_listParent.GetChild(i).gameObject);
                }
            }

            // Forcer _wasVisible à false (le panel vient d'être forcé à false ci-dessus)
            // pour que RefreshPanelVisibility() applique toujours le bon état.
            _wasVisible = false;

            _initialized = true;

            // Appliquer la visibilité initiale (réactive le panel si allowed)
            RefreshPanelVisibility();

            // IMPORTANT : on décale PopulateExistingPlayers d'une frame car
            // VRCPlayerApi.GetPlayerCount() peut retourner 0 pendant Start()
            // (le networking VRChat n'est pas encore initialisé).
            // Sans ce délai, l'entrée du joueur local (instance owner) n'est jamais créée.
            SendCustomEventDelayedFrames("_DelayedPopulate", 1);
        }

        protected override void Update()
        {
            base.Update();

            if (!_initialized) return;
            RefreshPanelVisibility();
        }

        // =============================================
        //  VRCHAT EVENTS
        // =============================================

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (player == null || !_initialized) return;

            // Chercher si le joueur a déjà une entrée (rejoin)
            M2922_PlayerInfoUI existing = FindEntryByName(player.displayName);
            if (existing != null)
            {
                existing.Reassign(player);
                return;
            }

            // Sinon, instancier une nouvelle entrée
            InstantiateEntry(player);

            // Forcer le rafraîchissement pour intégrer la nouvelle entrée
            ForceLayoutRefresh();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            // Ne pas détruire l'entrée : les données persistent pour toute l'instance.
        }

        // =============================================
        //  PUBLIC
        // =============================================

        public void RefreshList()
        {
            // Détruire toutes les entrées existantes
            if (_listParent != null)
            {
                for (int i = _listParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(_listParent.GetChild(i).gameObject);
                }
            }

            PopulateExistingPlayers();

            // Forcer le rafraîchissement après reconstruction complète
            ForceLayoutRefresh();
        }

        // =============================================
        //  PRIVATE
        // =============================================

        /// <summary>Appelé avec 1 frame de délai pour que le networking soit prêt.</summary>
        public void _DelayedPopulate()
        {
            if (!_initialized) return;

            PopulateExistingPlayers();
            ForceLayoutRefresh();
        }

        private void PopulateExistingPlayers()
        {
            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    if (FindEntryByName(players[i].displayName) == null)
                        InstantiateEntry(players[i]);
                }
            }
        }

        private void RefreshPanelVisibility()
        {
            if (_visibilityManager == null) return;

            bool shouldShow = _visibilityManager.IsLocalPlayerAllowed();

            if (shouldShow != _wasVisible)
            {
                _wasVisible = shouldShow;
                GameObject target = _panelRoot != null ? _panelRoot : gameObject;
                if (target != null)
                {
                    target.SetActive(shouldShow);

                    // Si le panel vient d'être réactivé, forcer un refresh du layout
                    // car les enfants ont pu changer pendant qu'il était caché.
                    if (shouldShow)
                        ForceLayoutRefresh();
                }
            }
        }

        private void InstantiateEntry(VRCPlayerApi player)
        {
            if (player == null || _playerInfoPrefab == null || _listParent == null) return;

            GameObject go = Instantiate(_playerInfoPrefab);
            if (go == null) return;

            go.transform.SetParent(_listParent, false);
            go.transform.localScale = Vector3.one;

            M2922_PlayerInfoUI info = go.GetComponent<M2922_PlayerInfoUI>();
            if (info != null)
                info.Setup(player, _visibilityManager, _counterStore);
        }

        private void ForceLayoutRefresh()
        {
            // Force la reconstruction des Canvas pour que le VerticalLayoutGroup
            // recalcule immédiatement les positions de tous ses enfants.
            Canvas.ForceUpdateCanvases();
        }

        private M2922_PlayerInfoUI FindEntryByName(string displayName)
        {
            if (_listParent == null || string.IsNullOrEmpty(displayName)) return null;

            M2922_PlayerInfoUI[] entries = _listParent.GetComponentsInChildren<M2922_PlayerInfoUI>();
            if (entries == null) return null;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].IsAssignedToName(displayName))
                    return entries[i];
            }
            return null;
        }
    }
}
