using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.UI
{
    /// <summary>
    /// Barre de vie UI : met à jour le m_FillAmount des Images
    /// "Life" et "shield" en fonction du HealthComponent / ShieldComponent.
    /// 
    /// SETUP :
    ///   1. Ajouter ce script sur le GameObject parent de la barre de vie.
    ///   2. Les Images "Life" et "shield" doivent être en Image Type = Filled,
    ///      Fill Method = Horizontal, Fill Origin = Left.
    ///   3. Assigner les références dans l'inspecteur OU laisser l'auto-détection
    ///      (cherche les enfants nommés "Life" et "shield").
    ///   4. Référence au DamageReceiver pour trouver le Health/Shield component.
    /// </summary>
    [AddComponentMenu("M2922/UI/Health Bar UI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_HealthBarUI : M2922_Base
    {
        [Header("=== REFERENCES CIBLE ===")]
        [Tooltip("DamageReceiver de l'entité à observer. Laissez vide = auto-détection locale.")]
        [SerializeField] private M2922_DamageReceiver _damageReceiver;
        [Tooltip("Cherche automatiquement le DamageReceiver du joueur local en remontant la hiérarchie.")]
        [SerializeField] private bool _autoDetectLocalPlayer = false;

        [Header("=== IMAGES (Fill) ===")]
        [Tooltip("Image 'Life' (barre de vie, rouge).")]
        [SerializeField] private Image _lifeFill;
        [Tooltip("Image 'shield' (barre de bouclier, bleu).")]
        [SerializeField] private Image _shieldFill;

        [Header("=== SMOOTHING ===")]
        [Tooltip("Vitesse d'animation de la barre (0 = instantané).")]
        [SerializeField] private float _smoothSpeed = 8f;

        // Cache
        private M2922_HealthComponent _health;
        private M2922_ShieldComponent _shield;
        private float _displayedHP = 1f;
        private float _displayedShield = 1f;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        protected override void Start()
        {
            base.Start();
            ResolveReferences();
        }

        protected override void Update()
        {
            base.Update();

            if (_health == null) return;

            float hpMax = _health.MaxHP;
            float hpCur = _health.CurrentHP;
            float hpPct = hpMax > 0f ? Mathf.Clamp01(hpCur / hpMax) : 0f;

            float shieldPct = 0f;
            if (_shield != null)
            {
                float shMax = _shield.MaxShield;
                float shCur = _shield.CurrentShield;
                shieldPct = shMax > 0f ? Mathf.Clamp01(shCur / shMax) : 0f;
            }

            // Smooth
            if (_smoothSpeed > 0f)
            {
                float dt = Time.deltaTime * _smoothSpeed;
                _displayedHP = Mathf.Lerp(_displayedHP, hpPct, dt);
                _displayedShield = Mathf.Lerp(_displayedShield, shieldPct, dt);
            }
            else
            {
                _displayedHP = hpPct;
                _displayedShield = shieldPct;
            }

            // Appliquer aux Images
            if (_lifeFill != null)
                _lifeFill.fillAmount = _displayedHP;

            if (_shieldFill != null)
                _shieldFill.fillAmount = _displayedShield;
        }

        // ============================================================
        // AUTO-DETECTION
        // ============================================================

        private void ResolveReferences()
        {
            // === 1. Trouver le DamageReceiver ===
            if (_damageReceiver == null && _autoDetectLocalPlayer)
            {
                // Méthode 1 : Manager registry (fonctionne même si l'UI est hors hiérarchie)
                if (Manager != null && Networking.LocalPlayer != null)
                {
                    _damageReceiver = Manager.GetReceiverByPlayerID(Networking.LocalPlayer.playerId);
                    if (_damageReceiver != null)
                        this.Log("[HealthBarUI] DamageReceiver trouvé via Manager registry");
                }

                // Méthode 2 : GetComponentInParent (fallback si pas de Manager)
                if (_damageReceiver == null)
                    _damageReceiver = GetComponentInParent<M2922_DamageReceiver>();

                if (_damageReceiver == null)
                {
                    this.Warning("[HealthBarUI] Aucun DamageReceiver trouvé ! Vérifie que le Manager est dans la scène et que le joueur local a un DamageReceiver.");
                }
            }

            // === 2. Récupérer Health & Shield depuis le DamageReceiver ===
            if (_damageReceiver != null)
            {
                _health = _damageReceiver.GetComponent<M2922_HealthComponent>();
                _shield = _damageReceiver.GetComponent<M2922_ShieldComponent>();

                if (_health == null)
                    this.Warning("[HealthBarUI] Pas de M2922_HealthComponent sur le même GameObject que le DamageReceiver !");
            }

            // === 3. Si toujours pas trouvé, chercher sur le parent direct (fallback) ===
            if (_health == null && transform.parent != null)
            {
                _health = transform.parent.GetComponentInChildren<M2922_HealthComponent>();
                _shield = transform.parent.GetComponentInChildren<M2922_ShieldComponent>();
            }

            // Auto-détection des Images par nom si non assignées
            if (_lifeFill == null)
            {
                Transform lifeTr = transform.Find("Life");
                if (lifeTr != null)
                    _lifeFill = lifeTr.GetComponent<Image>();
            }
            if (_shieldFill == null)
            {
                Transform shieldTr = transform.Find("shield");
                if (shieldTr != null)
                    _shieldFill = shieldTr.GetComponent<Image>();
            }

            // Initialiser les valeurs affichées
            if (_health != null)
                _displayedHP = _health.HPPercentage;
            if (_shield != null)
                _displayedShield = _shield.MaxShield > 0f
                    ? _shield.CurrentShield / _shield.MaxShield
                    : 0f;

            this.Log($"[HealthBarUI] HP={_displayedHP:P0} Shield={_displayedShield:P0}");
        }

        // ============================================================
        // API PUBLIQUE
        // ============================================================

        /// <summary>
        /// Force un rafraîchissement immédiat (sans smoothing).
        /// </summary>
        public void RefreshImmediate()
        {
            if (_health != null && _lifeFill != null)
            {
                _displayedHP = _health.HPPercentage;
                _lifeFill.fillAmount = _displayedHP;
            }
            if (_shield != null && _shieldFill != null)
            {
                _displayedShield = _shield.MaxShield > 0f
                    ? _shield.CurrentShield / _shield.MaxShield
                    : 0f;
                _shieldFill.fillAmount = _displayedShield;
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveReferences();
        }

        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("HP", _health != null ? $"{_health.CurrentHP:F0}/{_health.MaxHP:F0}" : "N/A"),
                new M2922_GizmoDisplayInfo("Shield", _shield != null ? $"{_shield.CurrentShield:F0}/{_shield.MaxShield:F0}" : "N/A"),
                new M2922_GizmoDisplayInfo("Life Fill", _lifeFill != null ? $"{_lifeFill.fillAmount:P0}" : "MISSING"),
                new M2922_GizmoDisplayInfo("Shield Fill", _shieldFill != null ? $"{_shieldFill.fillAmount:P0}" : "MISSING"),
            };
        }
#endif
    }
}
