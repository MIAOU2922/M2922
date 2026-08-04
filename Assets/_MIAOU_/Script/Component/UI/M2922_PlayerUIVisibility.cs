using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Component.UI
{
    /// <summary>
    /// À placer sur chaque UI de joueur instanciée.
    /// Interroge le M2922_VisibilityByRole central pour savoir
    /// si l'UI doit être visible par le joueur local.
    /// 
    /// RÈGLES :
    /// - Un joueur "autorisé" (Master ou dans la liste) voit les UI
    ///   des joueurs NON autorisés uniquement.
    /// - Un joueur NON autorisé ne voit aucune de ces UI.
    /// - Les UI des joueurs autorisés sont toujours masquées
    ///   (le staff ne voit pas les UI des autres staff).
    /// </summary>
    [AddComponentMenu("M2922/UI/Player UI Visibility")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_PlayerUIVisibility : UdonSharpBehaviour
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("Référence au M2922_VisibilityByRole central (liste synced).")]
        [SerializeField] private M2922_VisibilityByRole _visibilityManager;

        [Tooltip("Le VRCPlayerApi propriétaire de cette UI. Laisser vide = auto-détection via le network owner du GameObject.")]
        [SerializeField] private VRCPlayerApi _uiOwner;

        [Tooltip("Si true et que _uiOwner n'est pas assigné, suit le joueur local. False = suit le network owner du GO.")]
        [SerializeField] private bool _followLocalPlayer = false;

        [Header("=== DEBUG ===")]
        [Tooltip("Log les changements de visibilité dans la console.")]
        [SerializeField] private bool _debugVisibility = false;

        [Tooltip("GameObjects à show/hide selon la règle. Si vide, utilise ce gameObject.")]
        [SerializeField] private GameObject[] _targetObjects;

        private VRCPlayerApi _localPlayer;
        private bool _initialized;
        private int _lastVersion = -1;

        private void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            if (_localPlayer == null) return;

            if (_uiOwner == null)
            {
                if (_followLocalPlayer)
                {
                    _uiOwner = _localPlayer;
                }
                else
                {
                    // Priorité 1 : Network owner du GameObject (même pattern que BoneFollower)
                    VRCPlayerApi networkOwner = Networking.GetOwner(gameObject);
                    if (networkOwner != null && networkOwner.IsValid())
                        _uiOwner = networkOwner;
                    // Priorité 2 : Joueur local (fallback)
                    else
                        _uiOwner = _localPlayer;
                }
            }

            if (_debugVisibility)
                Debug.Log($"[M2922_PlayerUIVisibility] Start - UI Owner: {_uiOwner.displayName}, Local: {_localPlayer.displayName}");

            _initialized = true;
            RefreshVisibility();
        }

        private void Update()
        {
            // Polling léger : vérifie si la version du manager a changé
            if (!_initialized || _visibilityManager == null) return;

            int currentVersion = _visibilityManager.GetVersion();
            if (currentVersion != _lastVersion)
            {
                _lastVersion = currentVersion;
                RefreshVisibility();
            }
        }

        public void SetOwner(VRCPlayerApi owner)
        {
            _uiOwner = owner;
            _localPlayer = Networking.LocalPlayer;
            _initialized = true;
            RefreshVisibility();
        }

        public void RefreshVisibility()
        {
            if (!_initialized || _visibilityManager == null) return;
            if (_localPlayer == null) _localPlayer = Networking.LocalPlayer;
            if (_localPlayer == null) return;
            if (_uiOwner == null || !_uiOwner.IsValid())
            {
                _uiOwner = Networking.GetOwner(gameObject);
                if (_uiOwner == null || !_uiOwner.IsValid())
                    _uiOwner = _localPlayer;
            }

            bool localIsAllowed = _visibilityManager.IsLocalPlayerAllowed();
            bool ownerIsAllowed = _visibilityManager.IsPlayerAllowed(_uiOwner);

            bool shouldShow = localIsAllowed && !ownerIsAllowed;

            if (_debugVisibility)
                Debug.Log($"[M2922_PlayerUIVisibility] Local={_localPlayer.displayName} (allowed={localIsAllowed}), Owner={_uiOwner.displayName} (allowed={ownerIsAllowed}) => Show={shouldShow}");

            ApplyVisibility(shouldShow);
        }

        private void ApplyVisibility(bool show)
        {
            if (_targetObjects == null || _targetObjects.Length == 0)
            {
                gameObject.SetActive(show);
                return;
            }

            for (int i = 0; i < _targetObjects.Length; i++)
            {
                if (_targetObjects[i] != null)
                    _targetObjects[i].SetActive(show);
            }
        }
    }
}
