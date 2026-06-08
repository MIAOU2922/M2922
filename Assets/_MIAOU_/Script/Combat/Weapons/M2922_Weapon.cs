using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Entity;
using M2922.Entity.Player;
using M2922.Entity.Prop;
using EventType = M2922.Core.EventType;

namespace M2922.Combat
{
    /// <summary>
    /// Mode de rechargement de l'arme.
    /// </summary>
    public enum ReloadMode
    {
        /// <summary>Auto-rechargement déclenché par l'arme elle-même quand le chargeur est vide.</summary>
        Auto    = 0,

        /// <summary>Rechargement déclenché manuellement via TriggerReload() (appui bouton, Input…).</summary>
        Manual  = 1,

        /// <summary>
        /// Rechargement physique : un prop chargeur appelle MagSwap().
        /// TriggerReload() est ignoré dans ce mode.
        /// </summary>
        MagSwap = 2,
    }

    /// <summary>
    /// Système Arme — gère les mécaniques d'une arme : tir, munitions, rechargement.
    ///
    /// N'est PAS une entité. Peut équiper un canon de char, une tourelle, ou toute
    /// structure qui tire. Pour une arme ramassable (entité), voir M2922_WeaponEntity.
    ///
    /// ─── DÉGÂTS MULTI-TYPES ────────────────────────────────────────────────────
    ///   _damageTypes[]        : types appliqués en parallèle (ex: [Bullet, Fire])
    ///   _baseDamageAmounts[]  : montants de base par type (même index)
    ///   _critMultiplier       : multiplicateur appliqué si le collider touché a le tag "Crit"
    ///   SetDamageMultiplier() : buff/debuff global des dégâts (recalcul des effectives)
    ///
    /// ─── MUNITIONS ────────────────────────────────────────────────────────────
    ///   _baseMagSize        : capacité chargeur de base
    ///   _baseReserveSize    : réserve totale de base
    ///   _infiniteAmmo       : toggle (SetInfiniteAmmo())
    ///   SetMagSizeMultiplier / SetReserveSizeMultiplier : buffs/debuffs
    ///
    /// ─── RECHARGEMENT ─────────────────────────────────────────────────────────
    ///   ReloadMode.Auto      : auto après _autoReloadDelay sec quand le mag est vide
    ///   ReloadMode.Manual    : via TriggerReload() (bouton, input…)
    ///   ReloadMode.MagSwap   : via MagSwap() uniquement (prop physique)
    ///   _loseBulletsOnReload : si true, les balles restantes dans le chargeur actuel
    ///                         sont perdues au rechargement (mode réaliste)
    ///                         si false, elles restent / retournent en réserve (mode arcade)
    ///
    /// ─── UTILISATION ───────────────────────────────────────────────────────────
    ///   1. SetAttackerId(playerId)  ← appelé par WeaponEntity ou véhicule au montage
    ///   2. Fire(muzzleTransform)    ← appelé par WeaponEntity / tourelle en Update
    ///   3. TriggerReload()          ← manuel
    ///   4. MagSwap(newAmmo)         ← prop chargeur physique
    ///
    /// ─── EVENTS PUBLIÉS ────────────────────────────────────────────────────────
    ///   OnWeaponFired, OnWeaponReloaded, OnDamageDealt
    ///
    /// ─── SPREAD ───────────────────────────────────────────────────────────────
    ///   _baseMaxSpread        : dispersion maximale en degrés (0 = aucun spread)
    ///   _baseSpreadPerShot    : degrés ajoutés par balle tirée
    ///   _spreadRecoveryRate   : degrés/sec récupérés passivement entre les tirs
    ///   _resetSpreadOnReload  : si coché, rechargement remet le spread à 0
    ///   SetSpreadMultiplier() : buff/debuff multiplicateur du spread (max + per-shot)
    ///   Si _baseMaxSpread = 0 → aucun calcul, toujours droit
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class M2922_Weapon : M2922_System
    {
        // =====================================================================
        // WEAPON IDENTITY
        // =====================================================================
        [Header("=== WEAPON IDENTITY ===")]
        [SerializeField] private string _weaponDisplayName = "Unnamed Weapon";
        [SerializeField] private WeaponType _weaponType = WeaponType.Pistol;

        // =====================================================================
        // DAMAGE CONFIG
        // =====================================================================
        [Header("=== DAMAGE CONFIG ===")]
        [Tooltip("Types de dégâts appliqués simultanément (parallèle avec _baseDamageAmounts).\n" +
                 "Ex: [Bullet, Fire] → dégâts balle ET brûlure au même tir.")]
        [SerializeField] private DamageType[] _damageTypes = { DamageType.Bullet };

        [Tooltip("Montant de dégâts de base par type (même index que _damageTypes).\n" +
                 "Multiplié par _damageMultiplier (buffs/debuffs).")]
        [SerializeField] private float[] _baseDamageAmounts = { 25f };

        [Tooltip("Multiplicateur de dégâts si le collider touché a le tag 'Crit'.\n" +
                 "(ex: zone critique = ×2 → configure les colliders enfants de la cible avec ce tag)")]
        [SerializeField] private float _critMultiplier = 2f;

        // Valeurs effectives = base × _damageMultiplier (recalculées par RecalculateDamage)
        private float[] _effectiveDamageAmounts;
        private float   _damageMultiplier = 1f;

        // =====================================================================
        // FIRE SETTINGS
        // =====================================================================
        [Header("=== FIRE SETTINGS ===")]
        [Tooltip("Cadence de tir (coups par seconde)")]
        [SerializeField] private float _fireRate = 2f;

        [Tooltip("Portée maximale du raycast (mètres) — ignoré si mode projectile")]
        [SerializeField] private float _range = 100f;

        [Tooltip("Layers touchables par le raycast")]
        [SerializeField] private LayerMask _hitLayers = -1;

        [Header("=== FIRE MODE ===")]
        [Tooltip("true = tir automatique en maintenant la gâchette | false = semi-auto (un coup par appui)")]
        [SerializeField] private bool _isAutoFire = false;

        [Tooltip("false = raycast (hitscan) | true = projectile physique via pool")]
        [SerializeField] private bool _useProjectile = false;

        [Tooltip("Transform positionné au bout du canon. Le raycast / projectile part de là.\n" +
                 "Si vide : utilise le transform de cet objet (non recommandé).")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("Temps de maintien de la gâchette requis avant le tir (secondes).\n" +
                 "0 = aucune charge (comportement normal).\n" +
                 "Ex : 1 = l'arme tire 1 seconde après l'appui, si la gâchette reste enfoncée.")]
        [SerializeField] private float _chargeTime = 0f;

        // =====================================================================
        // PROJECTILE POOL
        // =====================================================================
        [Header("=== PROJECTILE MODE ===")]
        [Tooltip("Pool de M2922_Projectile pré-instanciés et désactivés en scène")]
        [SerializeField] private M2922_Projectile[] _projectilePool;

        [Tooltip("Vitesse initiale du projectile (m/s)")]
        [SerializeField] private float _projectileSpeed = 40f;

        // =====================================================================
        // AMMO CONFIG
        // =====================================================================
        [Header("=== AMMO CONFIG ===")]
        [Tooltip("Capacité du chargeur de base (modifiable par buff via SetMagSizeMultiplier)")]
        [SerializeField] private int _baseMagSize = 12;

        [Tooltip("Réserve de munitions totale de base (modifiable par buff via SetReserveSizeMultiplier)")]
        [SerializeField] private int _baseReserveSize = 60;

        [Tooltip("Munitions dans le chargeur au spawn / après retour à l'origine.\n" +
                 "-1 = chargeur plein (valeur par défaut).")]
        [SerializeField] private int _spawnMagAmmo = -1;

        [Tooltip("Munitions en réserve au spawn / après retour à l'origine.\n" +
                 "-1 = réserve pleine (valeur par défaut).")]
        [SerializeField] private int _spawnReserveAmmo = -1;

        [Tooltip("Si coché : aucune consommation de munitions, rechargement instantané")]
        [SerializeField] private bool _infiniteAmmo = false;

        // =====================================================================
        // RELOAD CONFIG
        // =====================================================================
        [Header("=== RELOAD CONFIG ===")]
        [SerializeField] private ReloadMode _reloadMode = ReloadMode.Auto;

        [Tooltip("Délai (sec) avant que l'auto-rechargement commence après vidage du chargeur\n" +
                 "(ReloadMode.Auto uniquement)")]
        [SerializeField] private float _autoReloadDelay = 0.3f;

        [Tooltip("Durée du rechargement en secondes (0 = instantané)")]
        [SerializeField] private float _reloadTime = 2f;

        [Tooltip("Si coché : les balles restantes dans le chargeur actuel sont PERDUES au rechargement (mode réaliste).\n" +
                 "Si décoché : les balles restantes sont conservées / retournent en réserve (mode arcade).")]
        [SerializeField] private bool _loseBulletsOnReload = false;

        // =====================================================================
        // SPREAD CONFIG
        // =====================================================================
        [Header("=== SPREAD CONFIG ===")]
        [Tooltip("Dispersion maximale en degrés.\n" +
                 "0 = aucun spread (toutes les balles partent parfaitement droites).\n" +
                 "Chaque tir augmente le spread d'autant, jusqu'à cette valeur max.")]
        [SerializeField] private float _baseMaxSpread = 2f;

        [Tooltip("Degrés de dispersion ajoutés par balle tirée.\n" +
                 "Plus on tire vite, moins le temps de récupération s'exprime entre les tirs.")]
        [SerializeField] private float _baseSpreadPerShot = 0.25f;

        [Tooltip("Vitesse de récupération passive du spread (degrés par seconde).\n" +
                 "Le spread redescend vers 0 automatiquement entre les tirs.")]
        [SerializeField] private float _spreadRecoveryRate = 2f;

        [Tooltip("Si coché : le spread est remis à 0 à la fin du rechargement.")]
        [SerializeField] private bool _resetSpreadOnReload = true;

        // =====================================================================
        // VFX / SFX
        // =====================================================================
        [Header("=== VFX / SFX ===")]
        [Tooltip("Particle system du flash au bout du canon.\nSon transform est orienté automatiquement dans la direction du tir avant Play().")]
        [SerializeField] private ParticleSystem _muzzleFlash;
        [Tooltip("Particle system d'éjection de la douille.\nPositionné sur le côté de l'arme, joué tel quel (rotation libre).")]
        [SerializeField] private ParticleSystem _casingEjection;
        [SerializeField] private AudioSource    _fireAudio;
        [SerializeField] private AudioSource    _reloadAudio;
        [SerializeField] private AudioSource    _emptyAudio;
        [Tooltip("Son joué en boucle pendant la phase de charge (optionnel).")]
        [SerializeField] private AudioSource    _chargeAudio;

        // =====================================================================
        // SYNCED
        // =====================================================================
        [UdonSynced] private int _currentAmmo;
        [UdonSynced] private int _currentReserveAmmo;

        // =====================================================================
        // RUNTIME (non-synced)
        // =====================================================================
        private int   _attackerId = -1;
        private float _nextFireTime;
        private bool  _isReloading;
        private int   _pendingMagAmmo = -1;   // >= 0 = MagSwap en cours avec ce montant

        // Charge
        private bool  _isCharging      = false;
        private float _chargeStartTime = 0f;

        private float _magSizeMultiplier     = 1f;
        private float _reserveSizeMultiplier = 1f;
        private int   _effectiveMagSize;
        private int   _effectiveReserveSize;

        private float _spreadMultiplier       = 1f;
        private float _currentSpread          = 0f;   // valeur courante (0 → _effectiveMaxSpread)
        private float _effectiveMaxSpread;             // = _baseMaxSpread * _spreadMultiplier
        private float _effectiveSpreadPerShot;         // = _baseSpreadPerShot * _spreadMultiplier

        // =====================================================================
        // PROPERTIES
        // =====================================================================
        public string     WeaponName       => _weaponDisplayName;
        public WeaponType WeaponType       => _weaponType;
        public int        CurrentAmmo      => _infiniteAmmo ? int.MaxValue : _currentAmmo;
        public int        MaxAmmo          => _effectiveMagSize;
        public int        ReserveAmmo      => _infiniteAmmo ? int.MaxValue : _currentReserveAmmo;
        public bool       InfiniteAmmo     => _infiniteAmmo;
        public float      FireRate         => _fireRate;
        public float      Range            => _range;
        public bool       IsReloading      => _isReloading;
        public bool       UseProjectile    => _useProjectile;
        public bool       IsAutoFire       => _isAutoFire;
        public ReloadMode CurrentReloadMode => _reloadMode;
        /// <summary>Dispersion courante en degrés (0 = parfaitement droit, montée tir par tir).</summary>
        public float      CurrentSpread    => _currentSpread;
        public float      MaxSpread        => _effectiveMaxSpread;

        /// <summary>Somme de tous les dégâts effectifs (tous types confondus).</summary>
        public float TotalDamage
        {
            get
            {
                if (_effectiveDamageAmounts == null) return 0f;
                float t = 0f;
                for (int i = 0; i < _effectiveDamageAmounts.Length; i++) t += _effectiveDamageAmounts[i];
                return t;
            }
        }

        public float ChargeTime => _chargeTime;
        /// <summary>Vrai si la gâchette est maintenue et le compteur de charge tourne.</summary>
        public bool  IsCharging => _isCharging;
        /// <summary>0..1 — avancement de la charge. Toujours 1 pour les armes sans charge.</summary>
        public float ChargeProgress
        {
            get
            {
                if (_chargeTime <= 0f) return 1f;
                if (!_isCharging)     return 0f;
                return Mathf.Clamp01((Time.time - _chargeStartTime) / _chargeTime);
            }
        }

        /// <summary>L'arme peut tirer : chargeur non vide (ou infini), pas en rechargement, cadence OK, charge complète.</summary>
        public bool CanFire => (_infiniteAmmo || _currentAmmo > 0)
                            && !_isReloading
                            && Time.time >= _nextFireTime
                            && (_chargeTime <= 0f || (_isCharging && Time.time - _chargeStartTime >= _chargeTime));

        /// <summary>TriggerReload() acceptée dans ce mode (Manual uniquement).</summary>
        public bool CanTriggerReload => _reloadMode == ReloadMode.Manual && !_isReloading;

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        // Awake() non utilisé : UdonSharp ne garantit pas son appel.
        // Toute l'initialisation est dans Start().

        protected override void Start()
        {
            base.Start();

            // 1. Multiplicateurs (UdonSharp initialise les privés non-sérialisés à 0)
            _damageMultiplier      = 1f;
            _magSizeMultiplier     = 1f;
            _reserveSizeMultiplier = 1f;
            _spreadMultiplier      = 1f;
            _pendingMagAmmo        = -1;

            // 2. Calcul des valeurs effectives (dépend des multiplicateurs ci-dessus)
            RecalculateEffectiveValues();

            // 3. Application de l'ammo spawn (dépend de _effectiveMagSize/_effectiveReserveSize)
            RestoreSpawnAmmo();

            // 4. Synchro réseau
            if (Networking.IsOwner(gameObject))
                RequestSerialization();
        }

        protected override void Update()
        {
            base.Update();

            // Récupération passive du spread UNIQUEMENT quand l'arme est au repos.
            // Pendant le tir automatique, _nextFireTime est toujours dans le futur :
            // la récupération est suspendue, le spread monte bien tir après tir.
            // Dès que la gâchette est relâchée et que l'intervalle de tir expire, la récup reprend.
            if (_currentSpread > 0f && _effectiveMaxSpread > 0f && _spreadRecoveryRate > 0f
                && Time.time >= _nextFireTime)
                _currentSpread = Mathf.Max(0f, _currentSpread - _spreadRecoveryRate * Time.deltaTime);
        }

        // =====================================================================
        // BUFF / DEBUFF API
        // =====================================================================

        /// <summary>Multiplicateur global de dégâts. Recalcule les effectives immédiatement.</summary>
        public void SetDamageMultiplier(float multiplier)
        {
            _damageMultiplier = Mathf.Max(0f, multiplier);
            RecalculateDamage();
            this.VerboseLog($"[Weapon] DamageMultiplier → {_damageMultiplier}");
        }

        /// <summary>Multiplicateur de taille de chargeur. Clampe l'ammo courante au nouveau max.</summary>
        public void SetMagSizeMultiplier(float multiplier)
        {
            _magSizeMultiplier = Mathf.Max(0f, multiplier);
            int prev          = _effectiveMagSize;
            _effectiveMagSize  = Mathf.Max(1, Mathf.RoundToInt(_baseMagSize * _magSizeMultiplier));
            if (_currentAmmo > _effectiveMagSize) _currentAmmo = _effectiveMagSize;
            this.VerboseLog($"[Weapon] MagSize: {prev} → {_effectiveMagSize}");
        }

        /// <summary>Multiplicateur de réserve. Clampe la réserve courante au nouveau max.</summary>
        public void SetReserveSizeMultiplier(float multiplier)
        {
            _reserveSizeMultiplier = Mathf.Max(0f, multiplier);
            int prev               = _effectiveReserveSize;
            _effectiveReserveSize  = Mathf.Max(0, Mathf.RoundToInt(_baseReserveSize * _reserveSizeMultiplier));
            if (_currentReserveAmmo > _effectiveReserveSize) _currentReserveAmmo = _effectiveReserveSize;
            this.VerboseLog($"[Weapon] ReserveSize: {prev} → {_effectiveReserveSize}");
        }

        /// <summary>
        /// Multiplicateur de spread (max + per-shot). 1 = base, 0 = aucun spread, 2 = double.
        /// </summary>
        public void SetSpreadMultiplier(float multiplier)
        {
            _spreadMultiplier        = Mathf.Max(0f, multiplier);
            _effectiveMaxSpread      = _baseMaxSpread     * _spreadMultiplier;
            _effectiveSpreadPerShot  = _baseSpreadPerShot * _spreadMultiplier;
            // Clampe le spread courant au nouveau max
            _currentSpread = Mathf.Min(_currentSpread, _effectiveMaxSpread);
            this.VerboseLog($"[Weapon] SpreadMultiplier → {_spreadMultiplier} | Max: {_effectiveMaxSpread}°");
        }

        /// <summary>Recalcule damage + magSize + reserveSize + spread en une seule passe.</summary>
        public void RecalculateEffectiveValues()
        {
            RecalculateDamage();
            _effectiveMagSize       = Mathf.Max(1, Mathf.RoundToInt(_baseMagSize     * _magSizeMultiplier));
            _effectiveReserveSize   = Mathf.Max(0, Mathf.RoundToInt(_baseReserveSize * _reserveSizeMultiplier));
            _effectiveMaxSpread     = _baseMaxSpread     * _spreadMultiplier;
            _effectiveSpreadPerShot = _baseSpreadPerShot * _spreadMultiplier;
            if (_currentAmmo        > _effectiveMagSize)    _currentAmmo        = _effectiveMagSize;
            if (_currentReserveAmmo > _effectiveReserveSize) _currentReserveAmmo = _effectiveReserveSize;
            _currentSpread = Mathf.Min(_currentSpread, _effectiveMaxSpread);
        }

        /// <summary>Toggle munitions infinies à chaud.</summary>
        public void SetInfiniteAmmo(bool infinite)
        {
            _infiniteAmmo = infinite;
            this.Log($"[Weapon] InfiniteAmmo → {_infiniteAmmo}");
        }

        /// <summary>Définit l'identifiant du porteur (joueur ou siège de véhicule).</summary>
        public void SetAttackerId(int playerId) { _attackerId = playerId; }

        // =====================================================================
        // CHARGE API
        // =====================================================================

        /// <summary>
        /// Démarre le compteur de charge. Appeler quand la gâchette est enfoncée.
        /// No-op si l'arme n'a pas de charge (<c>_chargeTime &lt;= 0</c>) ou si déjà en charge.
        /// </summary>
        public void BeginCharge()
        {
            if (_chargeTime <= 0f)  return;
            if (_isCharging)        return;
            if (_isReloading)       return;
            if (!_infiniteAmmo && _currentAmmo <= 0) return;

            _isCharging      = true;
            _chargeStartTime = Time.time;
            PlayChargeSound();
            this.VerboseLog($"[Weapon] Charge démarrée ({_chargeTime}s)");
        }

        /// <summary>
        /// Annule la charge sans tirer. Appeler quand la gâchette est relâchée avant que la charge soit complète.
        /// No-op si l'arme n'a pas de charge.
        /// </summary>
        public void CancelCharge()
        {
            if (!_isCharging) return;
            _isCharging = false;
            StopChargeSound();
            this.VerboseLog("[Weapon] Charge annulée");
        }

        // =====================================================================
        // FIRE API
        // =====================================================================

        /// <summary>
        /// Tirer. Utilise <paramref name="muzzleTransform"/> comme point et direction d'émission.
        /// Si null, utilise le transform de ce composant.
        /// </summary>
        public void Fire(Transform muzzleTransform)
        {
            if (!CanFire) return;

            if (!_infiniteAmmo) _currentAmmo--;
            _nextFireTime = Time.time + 1f / Mathf.Max(0.01f, _fireRate);

            // Direction calculée en premier — nécessaire pour aligner le VFX muzzle flash
            Transform muzzle  = muzzleTransform != null ? muzzleTransform
                              : (_muzzlePoint != null ? _muzzlePoint : transform);
            Vector3   shotDir = _ApplySpread(muzzle.forward, _currentSpread);

            PlayMuzzleFlash(shotDir);
            PlayCasingEjection();
            PlayFireSound();

            if (_useProjectile)
                FireProjectile(muzzle, shotDir);
            else
                FireRaycast(muzzle, shotDir);

            // Accumulation du spread après le tir
            if (_effectiveMaxSpread > 0f)
                _currentSpread = Mathf.Min(_effectiveMaxSpread, _currentSpread + _effectiveSpreadPerShot);

            // Réinitialiser la charge (une seule salve par charge)
            if (_chargeTime > 0f)
            {
                _isCharging = false;
                StopChargeSound();
            }

            RequestSerialization();
            PublishWeaponEvent(EventType.OnWeaponFired);
            this.VerboseLog($"[Weapon] Tir | Ammo: {_currentAmmo}/{_effectiveMagSize} | Spread: {_currentSpread:F2}°");

            // Auto-reload quand le chargeur est vide
            if (!_infiniteAmmo && _currentAmmo <= 0 && _reloadMode == ReloadMode.Auto
                && _currentReserveAmmo > 0)
            {
                if (_autoReloadDelay <= 0f)
                    _BeginReload();
                else
                    SendCustomEventDelayedSeconds(nameof(_BeginReload), _autoReloadDelay);
            }
        }

        // =====================================================================
        // RELOAD API
        // =====================================================================

        /// <summary>
        /// Déclenche le rechargement manuel (ReloadMode.Manual uniquement).
        /// Ignoré si le mode est Auto ou MagSwap.
        /// </summary>
        public void TriggerReload()
        {
            if (_reloadMode != ReloadMode.Manual) return;
            if (_isReloading) return;
            if (!_infiniteAmmo && _currentAmmo >= _effectiveMagSize) return;
            if (!_infiniteAmmo && _currentReserveAmmo <= 0) return;
            _BeginReload();
        }

        /// <summary>
        /// Rechargement par échange physique de chargeur (ReloadMode.MagSwap).
        /// Appelé par le prop chargeur quand il est inséré dans l'arme.
        /// Fonctionne dans tous les modes (usage par un prop externe).
        /// </summary>
        /// <param name="newMagAmmo">
        /// Munitions dans le nouveau chargeur. -1 = chargeur plein (_effectiveMagSize).
        /// </param>
        public void MagSwap(int newMagAmmo = -1)
        {
            if (_isReloading) return;

            if (!_infiniteAmmo)
            {
                if (_loseBulletsOnReload)
                {
                    // Balles restantes dans le chargeur actuel : perdues
                    _currentAmmo = 0;
                }
                else
                {
                    // Balles restantes : retournent en réserve
                    _currentReserveAmmo = Mathf.Min(_effectiveReserveSize,
                                                    _currentReserveAmmo + _currentAmmo);
                    _currentAmmo = 0;
                }
            }

            // Stocker le montant du nouveau chargeur (récupéré dans _FinishReload)
            _pendingMagAmmo = newMagAmmo >= 0
                ? Mathf.Min(newMagAmmo, _effectiveMagSize)
                : _effectiveMagSize;

            _BeginReload();
        }

        /// <summary>
        /// Remet les munitions aux valeurs configurées au spawn (_spawnMagAmmo / _spawnReserveAmmo).
        /// -1 = plein. Appelé au Awake et lors du retour à l'origine de la WeaponEntity.
        /// </summary>
        public void RestoreSpawnAmmo()
        {
            _currentAmmo        = _spawnMagAmmo    < 0 ? _effectiveMagSize     : Mathf.Clamp(_spawnMagAmmo,    0, _effectiveMagSize);
            _currentReserveAmmo = _spawnReserveAmmo < 0 ? _effectiveReserveSize : Mathf.Clamp(_spawnReserveAmmo, 0, _effectiveReserveSize);
            _isReloading        = false;
            _currentSpread      = 0f;
            RequestSerialization();
        }

        /// <summary>Ajouter des munitions de réserve (power-up, caisse…). Ignoré si infini.</summary>
        public void AddAmmo(int amount)
        {
            if (_infiniteAmmo) return;
            _currentReserveAmmo = Mathf.Min(_effectiveReserveSize, _currentReserveAmmo + amount);
            RequestSerialization();
            this.VerboseLog($"[Weapon] +{amount} ammo | Réserve: {_currentReserveAmmo}/{_effectiveReserveSize}");
        }

        /// <summary>Joue le son "chargeur vide" (appelez depuis le consommateur au besoin).</summary>
        public void PlayEmptySound()
        {
            if (_emptyAudio != null && _emptyAudio.clip != null)
                _emptyAudio.PlayOneShot(_emptyAudio.clip);
        }

        // =====================================================================
        // RELOAD INTERNALS
        // =====================================================================

        /// <summary>Interne — début effectif du rechargement (délai terminé ou instantané).</summary>
        public void _BeginReload()
        {
            if (_isReloading) return;

            // Annuler la charge si en cours (rechargement interrompt la charge)
            CancelCharge();

            // Guard : si rechargement depuis réserve, vérifier qu'il y a quelque chose à prendre
            if (!_infiniteAmmo && _pendingMagAmmo < 0 && _currentReserveAmmo <= 0) return;

            _isReloading = true;
            PlayReloadSound();

            if (_reloadTime <= 0f)
                _FinishReload();
            else
                SendCustomEventDelayedSeconds(nameof(_FinishReload), _reloadTime);
        }

        /// <summary>Interne — rechargement terminé. Ne pas appeler directement.</summary>
        public void _FinishReload()
        {
            if (_pendingMagAmmo >= 0)
            {
                // MagSwap : le nouveau chargeur a _pendingMagAmmo balles
                _currentAmmo    = _pendingMagAmmo;
                _pendingMagAmmo = -1;
            }
            else if (_infiniteAmmo)
            {
                _currentAmmo = _effectiveMagSize;
            }
            else if (_loseBulletsOnReload)
            {
                // Réaliste : les balles restantes ont déjà été perdues dans TriggerReload / _BeginReload.
                // On prend un nouveau chargeur complet depuis la réserve.
                int take            = Mathf.Min(_effectiveMagSize, _currentReserveAmmo);
                _currentReserveAmmo -= take;
                _currentAmmo         = take;
            }
            else
            {
                // Arcade : compléter le chargeur depuis la réserve (balles restantes conservées)
                int needed          = _effectiveMagSize - _currentAmmo;
                int take            = Mathf.Min(needed, _currentReserveAmmo);
                _currentAmmo       += take;
                _currentReserveAmmo -= take;
            }

            _isReloading = false;
            if (_resetSpreadOnReload) _currentSpread = 0f;
            RequestSerialization();
            PublishWeaponEvent(EventType.OnWeaponReloaded);
            this.Log($"[Weapon] Rechargé: {_currentAmmo}/{_effectiveMagSize} | Réserve: {_currentReserveAmmo}");
        }

        // =====================================================================
        // FIRE INTERNALS
        // =====================================================================

        private void FireRaycast(Transform origin, Vector3 direction)
        {
            RaycastHit hit;
            if (!Physics.Raycast(origin.position, direction, out hit, _range, _hitLayers, QueryTriggerInteraction.Collide)) return;

            // Stocker le résultat avant de comparer — null-check inline sur UdonSharpBehaviour crash en Udon.
            M2922_CritZone _cz = hit.collider.GetComponent<M2922_CritZone>();
            bool isCrit = _cz != null ? true : false;

            // UdonSharp : GetComponentInParent<UdonSharpBehaviour> n'est pas fiable.
            // On utilise GetComponent<M2922_HitboxOwner>() sur le même GO que le collider.
            M2922_HitboxOwner owner = hit.collider.GetComponent<M2922_HitboxOwner>();
            bool hasOwner = owner != null ? true : false;
            M2922_PlayerController pcVictim = hasOwner ? owner.PlayerController : null;
            bool isPCVictim = pcVictim != null ? true : false;
            M2922_Prop propVictim = hasOwner ? owner.Prop : null;
            bool isPropVictim = propVictim != null ? true : false;
            int victimId = isPCVictim ? pcVictim.EntityId : (isPropVictim ? propVictim.EntityId : -1);
            string targetName = isPCVictim ? pcVictim.EntityName : (isPropVictim ? propVictim.EntityName : hit.collider.name);

            PublishAllDamageEvents(victimId, isCrit, 1f, hit.point, hit.normal, hit.distance);
            ApplyDamageTo(owner, isCrit, 1f);
            this.VerboseLog($"[Weapon] Raycast hit {targetName} | Crit: {isCrit}");
        }

        // Applique les dégâts directement sur la cible (PlayerController ou Prop).
        // Les events sont publiés séparément via PublishAllDamageEvents.
        // Reçoit l'entité déjà trouvée via GetComponentInParent<M2922_Entity>().
        // Utilise des ternaires pour les null-checks UdonSharpBehaviour (pattern sûr en Udon).
        private void ApplyDamageTo(M2922_HitboxOwner owner, bool isCrit, float damageScale)
        {
            bool hasOwner = owner != null ? true : false;
            if (!hasOwner) return;
            if (_damageTypes == null || _effectiveDamageAmounts == null) return;
            float critMult = isCrit ? _critMultiplier : 1f;
            int count = Mathf.Min(_damageTypes.Length, _effectiveDamageAmounts.Length);

            M2922_PlayerController pc = owner.PlayerController;
            bool isPC = pc != null ? true : false;
            if (isPC)
            {
                for (int i = 0; i < count; i++)
                {
                    float dmg = _effectiveDamageAmounts[i] * damageScale * critMult;
                    if (dmg > 0f) pc.TakeDamage(dmg, _attackerId, _damageTypes[i]);
                }
                return;
            }

            M2922_Prop prop = owner.Prop;
            bool isProp = prop != null ? true : false;
            if (isProp)
            {
                for (int i = 0; i < count; i++)
                {
                    float dmg = _effectiveDamageAmounts[i] * damageScale * critMult;
                    if (dmg > 0f) prop.TakeDamage(dmg, _attackerId, _damageTypes[i]);
                }
            }
        }

        private void FireProjectile(Transform origin, Vector3 direction)
        {
            M2922_Projectile proj = GetPooledProjectile();
            if (proj == null)
            {
                this.Log("[Weapon] Aucun projectile disponible dans le pool !");
                return;
            }

            proj.Initialize(
                origin.position, direction, _projectileSpeed,
                _attackerId, _damageTypes, _effectiveDamageAmounts, _critMultiplier);

            this.VerboseLog($"[Weapon] Projectile lancé depuis {origin.position} | Dir: {direction}");
        }

        private M2922_Projectile GetPooledProjectile()
        {
            if (_projectilePool == null) return null;
            for (int i = 0; i < _projectilePool.Length; i++)
                if (_projectilePool[i] != null && !_projectilePool[i].IsActive)
                    return _projectilePool[i];
            return null;
        }

        // =====================================================================
        // DAMAGE PUBLISHING
        // =====================================================================

        private void PublishAllDamageEvents(
            int victimId, bool isCrit, float damageScale,
            Vector3 hitPoint, Vector3 hitNormal, float distance)
        {
            if (_damageTypes == null || _effectiveDamageAmounts == null) return;

            int   count    = Mathf.Min(_damageTypes.Length, _effectiveDamageAmounts.Length);
            float critMult = isCrit ? _critMultiplier : 1f;

            for (int i = 0; i < count; i++)
            {
                float finalDmg = _effectiveDamageAmounts[i] * damageScale * critMult;
                if (finalDmg <= 0f) continue;
                PublishDamageEvent(victimId, finalDmg, _damageTypes[i], isCrit,
                                   hitPoint, hitNormal, distance);
            }
        }

        private void PublishDamageEvent(
            int victimId, float damage, DamageType damageType, bool isCrit,
            Vector3 hitPoint, Vector3 hitNormal, float distance)
        {
            if (Manager == null || Manager.EventBus == null) return;

            M2922_EventData slot = Manager.EventBus.RentSlot();
            if (slot == null) return;

            slot.AttackerId  = _attackerId;
            slot.VictimId    = victimId;
            slot.Damage      = damage;
            slot.DamageType  = (int)damageType;
            slot.WeaponType  = (int)_weaponType;
            slot.WeaponName  = _weaponDisplayName;
            slot.IsHeadshot  = isCrit;    // réutilise le champ (crit = zone critique)
            slot.Position    = hitPoint;
            slot.HitNormal   = hitNormal;
            slot.Distance    = distance;

            Manager.EventBus.PublishNetwork(EventType.OnDamageDealt, slot);
        }

        private void PublishWeaponEvent(EventType eventType)
        {
            if (Manager == null || Manager.EventBus == null) return;

            M2922_EventData slot = Manager.EventBus.RentSlot();
            if (slot == null) return;

            slot.WeaponName   = _weaponDisplayName;
            slot.WeaponType   = (int)_weaponType;
            slot.CurrentAmmo  = _currentAmmo;
            slot.ReserveAmmo  = _currentReserveAmmo;
            slot.AttackerId   = _attackerId;

            Manager.EventBus.Publish(eventType, slot);
        }

        // =====================================================================
        // VFX / SFX
        // =====================================================================
        private void RecalculateDamage()
        {
            if (_baseDamageAmounts == null || _baseDamageAmounts.Length == 0)
            { _effectiveDamageAmounts = new float[0]; return; }

            if (_effectiveDamageAmounts == null
                || _effectiveDamageAmounts.Length != _baseDamageAmounts.Length)
                _effectiveDamageAmounts = new float[_baseDamageAmounts.Length];

            for (int i = 0; i < _baseDamageAmounts.Length; i++)
                _effectiveDamageAmounts[i] = Mathf.Max(0f, _baseDamageAmounts[i] * _damageMultiplier);
        }

        private void PlayMuzzleFlash(Vector3 shotDir)
        {
            if (_muzzleFlash == null) return;
            _muzzleFlash.transform.rotation = Quaternion.LookRotation(shotDir);
            _muzzleFlash.Play();
        }
        private void PlayCasingEjection() { if (_casingEjection != null) _casingEjection.Play(); }
        private void PlayFireSound()
        {
            if (_fireAudio != null && _fireAudio.clip != null)
                _fireAudio.PlayOneShot(_fireAudio.clip);
        }
        private void PlayChargeSound()
        {
            if (_chargeAudio != null && _chargeAudio.clip != null)
                _chargeAudio.Play();
        }
        private void StopChargeSound()
        {
            if (_chargeAudio != null && _chargeAudio.isPlaying)
                _chargeAudio.Stop();
        }
        private void PlayReloadSound()
        {
            if (_reloadAudio != null && _reloadAudio.clip != null)
                _reloadAudio.PlayOneShot(_reloadAudio.clip);
        }

        /// <summary>
        /// Retourne une direction avec dispersion aléatoire dans un cône de <paramref name="angleDeg"/> degrés.
        /// Si angleDeg ≤ 0 (ou spread désactivé), retourne la direction originale sans modification.
        /// </summary>
        private Vector3 _ApplySpread(Vector3 baseDir, float angleDeg)
        {
            if (angleDeg <= 0f || _effectiveMaxSpread <= 0f) return baseDir;

            // Génère un offset aléatoire dans un disque, mis à l'échelle par tan(angle)
            float   halfAngleRad = angleDeg * 0.5f * Mathf.Deg2Rad;
            float   radius       = Mathf.Tan(halfAngleRad);
            Vector2 rand         = Random.insideUnitCircle * radius;

            // Construit une base orthonormée à partir de baseDir
            Vector3 right = Vector3.Cross(baseDir, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(baseDir, Vector3.forward);
            right = right.normalized;
            Vector3 up = Vector3.Cross(right, baseDir).normalized;

            return (baseDir + right * rand.x + up * rand.y).normalized;
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _fireRate            = Mathf.Max(0.01f, _fireRate);
            _range               = Mathf.Max(0f,    _range);
            _baseMagSize         = Mathf.Max(1,     _baseMagSize);
            _baseReserveSize     = Mathf.Max(0,     _baseReserveSize);
            _baseMaxSpread       = Mathf.Max(0f,    _baseMaxSpread);
            _baseSpreadPerShot   = Mathf.Max(0f,    _baseSpreadPerShot);
            _spreadRecoveryRate  = Mathf.Max(0f,    _spreadRecoveryRate);
            _chargeTime          = Mathf.Max(0f,    _chargeTime);

            if (_damageTypes != null && _baseDamageAmounts != null
                && _damageTypes.Length != _baseDamageAmounts.Length)
                UnityEngine.Debug.LogWarning(
                    $"[M2922_Weapon] {_weaponDisplayName}: " +
                    "_damageTypes et _baseDamageAmounts doivent avoir la même longueur !");
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;

            string dmgSummary = "";
            if (_damageTypes != null && _baseDamageAmounts != null)
            {
                int c = Mathf.Min(_damageTypes.Length, _baseDamageAmounts.Length);
                for (int i = 0; i < c; i++)
                    dmgSummary += $"{_damageTypes[i]}:{_baseDamageAmounts[i]}  ";
            }
            string reloadInfo = _reloadMode == ReloadMode.Auto
                ? $"Auto (delay:{_autoReloadDelay}s + {_reloadTime}s)"
                : $"{_reloadMode} ({_reloadTime}s)";

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 1f),
                $"[Weapon] {_weaponDisplayName} ({_weaponType})\n" +
                $"Dégâts: {dmgSummary}\nCrit: ×{_critMultiplier}\n" +
                $"Chargeur: {_baseMagSize} | Réserve: {_baseReserveSize} | Infini: {_infiniteAmmo}\n" +
                $"Reload: {reloadInfo} | LoseBullets: {_loseBulletsOnReload}\n" +
                $"Mode: {(_useProjectile ? "Projectile" : "Raycast")} | Rate: {_fireRate}/s\n" +
                $"Spread max: {_baseMaxSpread}° | /tir: {_baseSpreadPerShot}° | récup: {_spreadRecoveryRate}°/s | resetReload: {_resetSpreadOnReload}"
            );
        }
#endif
    }
}
