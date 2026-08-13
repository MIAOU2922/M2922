using UdonSharp;
using UnityEngine;
using TMPro;
using M2922.Core;
using VRC.SDKBase;

namespace M2922.Component.UI
{
    /// <summary>
    /// Compteur UI avec boutons + et - et affichage TMP dont la couleur
    /// change selon des seuils (thresholds) configurables.
    /// 
    /// PERSISTENCE : si _counterStore est assigné, la valeur est sauvegardée
    /// par joueur et restaurée après une déconnexion/reconnexion.
    /// 
    /// SETUP :
    /// 1. Ajouter ce script sur un GameObject ayant un TextMeshProUGUI.
    /// 2. Assigner les boutons + et - dans l'inspecteur.
    /// 3. Optionnel : assigner un M2922_PlayerCounterStore pour la persistence.
    /// </summary>
    [AddComponentMenu("M2922/UI/Counter UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_CounterUI : M2922_Tickable
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("Texte TMP pour afficher le compteur (auto-détecté si vide).")]
        [SerializeField] protected TextMeshProUGUI _counterText;

        [Tooltip("Store persistant pour sauvegarder/restaurer la valeur par joueur.")]
        [SerializeField] protected M2922_PlayerCounterStore _counterStore;

        [Tooltip("Si non vide, le compteur affiche la valeur de CE joueur au lieu du joueur local.")]
        [SerializeField] private string _targetDisplayName = "";

        [Header("=== COUNTER SETTINGS ===")]
        [Tooltip("Valeur initiale du compteur.")]
        [SerializeField] private int _startValue = 0;
        [Tooltip("Valeur minimum.")]
        [SerializeField] private int _minValue = 0;
        [Tooltip("Valeur maximum (0 = pas de limite).")]
        [SerializeField] private int _maxValue = 999;
        [Tooltip("Incrément par clic.")]
        [SerializeField] private int _step = 1;

        [Header("=== COLOR THRESHOLDS ===")]
        [Tooltip("Seuils (valeurs). Doit avoir la même taille que Threshold Colors.")]
        [SerializeField] private int[] _thresholdValues = new int[] { 0, 1, 5 };
        [Tooltip("Couleurs associées à chaque seuil (même index).")]
        [SerializeField] private Color[] _thresholdColors = new Color[] { Color.red, Color.blue, Color.green };

        [Tooltip("Couleur par défaut si aucun seuil ne correspond.")]
        [SerializeField] private Color _defaultColor = Color.white;

        // --- état interne ---
        protected int _currentValue;
        protected VRCPlayerApi _localPlayer;
        protected bool _restoredFromStore;

        // =============================================
        //  UNITY LIFECYCLE
        // =============================================

        protected override void Start()
        {
            base.Start();

            if (_counterText == null)
                _counterText = GetComponent<TextMeshProUGUI>();

            _localPlayer = Networking.LocalPlayer;

            // Auto-find du store si non assigné dans l'inspector (ex. Counter Ui sur la tête)
            if (_counterStore == null)
            {
                GameObject storeObj = GameObject.Find("PlayerCounterStore");
                if (storeObj != null)
                    _counterStore = storeObj.GetComponent<M2922_PlayerCounterStore>();
            }

            // Auto-détection du joueur cible si _targetDisplayName est vide :
            // on utilise le network owner du GameObject (même logique que PlayerUIVisibility).
            if (string.IsNullOrEmpty(_targetDisplayName))
            {
                VRCPlayerApi owner = Networking.GetOwner(gameObject);
                if (owner != null && owner.IsValid())
                    _targetDisplayName = owner.displayName;
            }

            InitCounterValues();
        }

        /// <summary>
        /// Polling du store pour détecter les changements faits par d'autres
        /// joueurs (ex. via la Player List). Les classes filles qui override
        /// Update() doivent appeler base.Update() pour hériter de ce polling.
        /// </summary>
        protected override void Update()
        {
            base.Update();

            // ── FRAME-SKIP (M2922_Base.ShouldUpdate) : polling ~1×/sec ──
            if (!ShouldUpdate()) return;

            if (_counterStore == null) return;

            string key = GetSaveKey();
            if (string.IsNullOrEmpty(key)) return;

            int stored = _counterStore.GetCounter(key);
            if (stored >= 0 && stored != _currentValue)
            {
                _currentValue = stored;
                UpdateDisplay();
            }
        }

        /// <summary>Initialise ou restaure la valeur du compteur. Surchargeable.</summary>
        protected virtual void InitCounterValues()
        {
            string playerName = _localPlayer != null ? _localPlayer.displayName : "null";
            this.Log($"[CounterUI] InitCounter - counterText assigned={_counterText != null}, localPlayer={playerName}, startValue={_startValue}, min={_minValue}, max={_maxValue}, step={_step}");

            _restoredFromStore = false;

            if (_counterStore != null)
            {
                string key = GetSaveKey();
                if (!string.IsNullOrEmpty(key) && _counterStore.HasCounter(key))
                {
                    _currentValue = _counterStore.GetCounter(key);
                    _restoredFromStore = true;
                }
            }

            if (!_restoredFromStore)
                _currentValue = Mathf.Clamp(_startValue, _minValue, _maxValue > 0 ? _maxValue : int.MaxValue);

            this.Log($"[CounterUI] InitCounter - initialized with value={_currentValue}");
            UpdateDisplay();
        }

        /// <summary>Clé utilisée pour sauvegarder/restaurer le compteur. Surchargeable.</summary>
        protected virtual string GetSaveKey()
        {
            // Priorité 1 : cible explicite (ex. Counter Ui au-dessus d'un joueur spécifique)
            if (!string.IsNullOrEmpty(_targetDisplayName))
                return _targetDisplayName;
            // Priorité 2 : joueur local (ex. compteur personnel)
            return _localPlayer != null ? _localPlayer.displayName : "";
        }

        // =============================================
        //  PUBLIC (à brancher sur les onClick des boutons)
        // =============================================

        public void Increment()
        {
            this.Log($"[CounterUI] Increment called. Current={_currentValue}, Step={_step}");

            int max = _maxValue > 0 ? _maxValue : int.MaxValue;
            _currentValue = Mathf.Min(_currentValue + _step, max);

            this.Log($"[CounterUI] Increment done. New={_currentValue}");
            UpdateDisplay();
            SaveToStore();
        }

        public void Decrement()
        {
            this.Log($"[CounterUI] Decrement called. Current={_currentValue}, Step={_step}");

            _currentValue = Mathf.Max(_currentValue - _step, _minValue);

            this.Log($"[CounterUI] Decrement done. New={_currentValue}");
            UpdateDisplay();
            SaveToStore();
        }

        public void ResetCounter()
        {
            _currentValue = Mathf.Clamp(_startValue, _minValue, _maxValue > 0 ? _maxValue : int.MaxValue);
            UpdateDisplay();
            SaveToStore();
        }

        public void SetValue(int value)
        {
            int max = _maxValue > 0 ? _maxValue : int.MaxValue;
            _currentValue = Mathf.Clamp(value, _minValue, max);
            UpdateDisplay();
            SaveToStore();
        }

        public int GetValue() => _currentValue;

        // =============================================
        //  PROTECTED — Surchargeable par les classes filles
        // =============================================

        protected virtual void SaveToStore()
        {
            string key = GetSaveKey();
            if (_counterStore == null || string.IsNullOrEmpty(key)) return;
            _counterStore.SaveCounter(key, _currentValue);
        }

        protected void UpdateDisplay()
        {
            if (_counterText == null) return;

            _counterText.text = _currentValue.ToString();
            _counterText.color = GetColorForValue(_currentValue);
        }

        protected Color GetColorForValue(int val)
        {
            if (_thresholdValues == null || _thresholdColors == null)
                return _defaultColor;

            int count = _thresholdValues.Length;
            if (_thresholdColors.Length < count)
                count = _thresholdColors.Length;
            if (count == 0)
                return _defaultColor;

            int best = -1;

            for (int i = 0; i < count; i++)
            {
                if (val >= _thresholdValues[i])
                    best = i;
            }

            if (best < 0)
                return _defaultColor;

            return _thresholdColors[best];
        }
    }
}

