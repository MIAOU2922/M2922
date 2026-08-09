using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Phases du rechargement séquentiel (shotgun).
    /// </summary>
    public enum ReloadPhase
    {
        None,
        Start,  // Ouverture de l'arme
        Loop,   // Insertion balle par balle
        End     // Fermeture de l'arme
    }

    /// <summary>
    /// Style de rechargement configurable par arme.
    /// </summary>
    public enum ReloadStyle
    {
        Default,    // Déterminé par le type d'arme
        Magazine,   // Chargeur complet
        Sequential  // Balle par balle (Shotgun, Revolver, GL, Scout, etc.)
    }

    /// <summary>
    /// Gère la logique de tir : hitscan (raycast) pour les armes à balle,
    /// projectiles physiques pour les lanceurs (roquette, grenade).
    /// 
    /// HITSCAN : AutoRifle, PulseRifle, ScoutRifle, HandCannon, SMG, Sidearm,
    ///          Sniper, MachineGun, Shotgun (multi-raycast), CombatBow,
    ///          FusionRifle, LinearFusionRifle
    /// PROJECTILE : RocketLauncher, BreechGL, HeavyGL, RocketSidearm
    /// BEAM : TraceRifle (raycast continu)
    /// MELEE : Sword, Glaive (melee)
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Fire Handler")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_WeaponFireHandler : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_Weapon _weapon;
        [SerializeField] private Transform _muzzlePoint;

        [Header("=== HITSCAN SETTINGS ===")]
        [Tooltip("Layer des hitboxes. Résolu auto depuis le nom.")]
        [SerializeField] private string _hitboxLayerName = "Hitbox";
        [SerializeField] private LayerMask _hitscanLayerMask = ~0;
        [Tooltip("Layer ID (auto-résolu, ne pas modifier).")]
        [SerializeField] private int _hitboxLayer = 8;
        [SerializeField] private GameObject _hitEffectPrefab;

        [Header("=== VFX ===")]
        [Tooltip("Prefab de muzzle flash (particules). Spawné au muzzle avec le spread du tir.")]
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [Tooltip("Durée de vie du muzzle flash avant destroy (secondes).")]
        [SerializeField] private float _muzzleFlashLifetime = 0.1f;

        [Header("=== SFX ===")]
        [Tooltip("AudioSource pour les sons. Si null, sera cherché sur ce GameObject.")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _fireSound;
        [SerializeField] private AudioClip _reloadSound;
        [SerializeField] private AudioClip _emptySound;
        [Tooltip("Volume du son de tir (0-1).")]
        [SerializeField] private float _fireVolume = 0.8f;
        [Tooltip("Volume du son de rechargement (0-1).")]
        [SerializeField] private float _reloadVolume = 0.7f;
        [Tooltip("Volume du son tir à vide (0-1).")]
        [SerializeField] private float _emptyVolume = 0.5f;

        [Header("=== PROJECTILE (lanceurs seulement) ===")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private int _poolSize = 10;
        private M2922_Projectile[] _pool;
        private int _poolIndex = 0;

        [Header("=== BEAM (Trace Rifle) ===")]
        [SerializeField] private LineRenderer _beamRenderer;
        [Tooltip("Layers que le beam traverse/détecte (hitbox + environment).")]
        [SerializeField] private LayerMask _beamLayerMask = ~0;
        private float _beamDamageAccum = 0f;
        private Collider _beamHitTarget = null;
        private float _beamTimer = 0f;

        [Header("=== RELOAD ===")]
        [Tooltip("Rechargement automatique quand le chargeur est vide.")]
        [SerializeField] private bool _autoReload = true;

        [Header("=== MELEE ===")]
        [SerializeField] private float _meleeRange = 2f;
        [SerializeField] private float _meleeRadius = 1.5f;

        [Header("=== RECOIL VISUEL (animation du modèle) ===")]
        [Tooltip("Transform du modèle 3D à animer. Si null, prend transform.parent.")]
        [SerializeField] private Transform _weaponModelRoot;
        private float _recoilKickBack = 0f;
        private float _recoilKickUp = 0f;
        private float _recoilKickSide = 0f;
        private float _recoilRecoverySpeed = 0f;
        private Vector3 _recoilOriginalLocalPos;
        private Quaternion _recoilOriginalLocalRot;
        // Valeurs calculées (remplies dans Start si overrides = 0)
        private float _finalRecoilKickBack;
        private float _finalRecoilKickUp;
        private float _finalRecoilKickSide;
        private float _finalRecoilRecoverySpeed;

        // --- INPUT ---
        private bool _triggerHeld = false;
        private bool _triggerJustPressed = false;

        // --- FIRE STATE ---
        private float _fireCooldown = 0f;
        private float _fireInterval = 0.1f;
        private int _burstRemaining = 0;
        private int _burstTotal = 0;       // total de la rafale (pour progresion)
        private float _burstDelay = 0.06f;
        private float _chargeTimer = 0f;
        private bool _isCharging = false;
        private bool _beamActive = false;
        private bool _beamEmptyPlayed = false; // évite spam du son tir à vide sur le beam
        private float _currentSpreadMult = 1f; // multiplicateur de spread (rafale)
        private float _bloomAccum = 0f;        // accumulation bloom en tir continu
        private float _bloomPerShot = 0.3f;    // +0.3 par tir full auto
        private float _bloomDecay = 2f;        // -2/sec à l'arrêt
        private float _bloomMaxMult = 5f;      // cap du multiplicateur (x5 max)

        // --- RELOAD STATE ---
        private bool _isReloading = false;
        private float _reloadTimer = 0f;
        private ReloadPhase _reloadPhase = ReloadPhase.None;
        private bool _isSequentialReload = false;

        // --- OWNERSHIP ---
        private VRCPlayerApi _localPlayer;
        private bool _isHeld = false;
        private WeaponType _weaponType;
        private FireMode _fireMode;
        private bool _useHitscan;
        private bool _useProjectile;

        // --- NETWORK FIRE VFX ---
        [UdonSynced] private int _fireTick = 0;
        [UdonSynced] private float _syncedSpreadX = 0f;
        [UdonSynced] private float _syncedSpreadY = 0f;
        private int _lastFireTick = -1;

        // --- NETWORK PROJECTILE (spawn pour clients distants) ---
        [UdonSynced] private int _projectileFireTick = 0;
        [UdonSynced] private float _projPosX, _projPosY, _projPosZ;
        [UdonSynced] private float _projFwdX, _projFwdY, _projFwdZ;
        private int _lastProjectileFireTick = -1;

        // --- NETWORK BEAM (Trace Rifle) ---
        [UdonSynced] private bool _syncedBeamActive = false;
        [UdonSynced] private float _syncedBeamStartX, _syncedBeamStartY, _syncedBeamStartZ;
        [UdonSynced] private float _syncedBeamEndX, _syncedBeamEndY, _syncedBeamEndZ;

        // --- NETWORK DAMAGE RELAY (PvP + NPC) ---
        /// <summary>Player ID de la CIBLE (-1 = NPC/destructible).</summary>
        [UdonSynced] private int _relayTargetId = -1;
        /// <summary>Dégâts accumulés (shotgun multi-pellet, beam pulses).</summary>
        [UdonSynced] private float _relayDamage = 0f;
        [UdonSynced] private int _relayDamageType = 0;
        /// <summary>Entity ID pour les NPCs (via HitboxSystem._entityId).</summary>
        [UdonSynced] private int _relayEntityId = -1;
        /// <summary>True si cible = NPC (sinon PvP).</summary>
        [UdonSynced] private bool _relayIsNpc = false;
        /// <summary>Player ID du TIREUR (pour éviter double-apply).</summary>
        [UdonSynced] private int _relayShooterId = -1;
        /// <summary>N° de séquence incrémenté à chaque batch.</summary>
        [UdonSynced] private int _relaySequence = 0;
        private int _lastRelaySeq = -1;
        /// <summary>Frame du dernier reset de l'accumulateur _relayDamage.</summary>
        private int _relayFrame = -1;

        // --- RELAY BATCH (projectile splash multi-cibles) ---
        private M2922_DamageReceiver[] _batchReceivers = new M2922_DamageReceiver[16];
        private float[] _batchDamages = new float[16];
        private int _batchCount = 0;
        private int _batchIndex = 0;

        // ===================================================
        // LIFECYCLE
        // ===================================================

        protected override void Start()
        {
            base.Start();

            _localPlayer = Networking.LocalPlayer;

            if (_weapon == null)
                _weapon = GetComponent<M2922_Weapon>();
            // Fallback : chercher dans les enfants (si Weapon sur un child)
            if (_weapon == null)
                _weapon = GetComponentInChildren<M2922_Weapon>();

            if (_weapon == null)
            {
                this.Error("Aucun M2922_Weapon trouvé !");
                return;
            }

            // AudioSource : utiliser celui assigné ou en trouver un sur ce GO
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            _weaponType = (WeaponType)_weapon.WeaponTypeAsInt;
            _fireMode = FireModeMapping.GetFireMode(_weaponType);
            _useHitscan = FireModeMapping.UseHitscan(_weaponType);
            _useProjectile = FireModeMapping.UseProjectile(_weaponType);

            float rpm = _weapon.RPM;
            if (rpm <= 0f) rpm = 300f;
            _fireInterval = 60f / rpm;

            if (_useProjectile && _projectilePrefab != null)
                InitProjectilePool();

            if (_beamRenderer != null)
            {
                _beamRenderer.positionCount = 2;
                _beamRenderer.enabled = false;
            }

            // Recoil : capturer la position/rotation locale du modèle
            if (_weaponModelRoot == null)
                _weaponModelRoot = transform.parent;
            ComputeRecoilStats();
            CaptureRecoilOrigin();

            this.Log("FireHandler pret. Mode=" + _fireMode.ToString()
                + " RPM=" + rpm.ToString()
                + " Hitscan=" + _useHitscan.ToString()
                + " Projectile=" + _useProjectile.ToString());
        }

        private void InitProjectilePool()
        {
            _pool = new M2922_Projectile[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject obj = Instantiate(_projectilePrefab);
                // Détacher du weapon pour que les projectiles inactifs ne suivent pas l'arme
                obj.transform.SetParent(null);
                obj.transform.position = Vector3.zero;
                obj.SetActive(false);
                _pool[i] = obj.GetComponent<M2922_Projectile>();
            }
        }

        // ===================================================
        // VRChat PICKUP CALLBACKS (sur le même GO que VRC Pickup)
        // ===================================================

        public override void OnPickup()
        {
            HandlePickup();
        }

        public override void OnDrop()
        {
            HandleDrop();
        }

        public override void OnPickupUseDown()
        {
            HandlePickupUseDown();
        }

        public override void OnPickupUseUp()
        {
            HandlePickupUseUp();
        }

        // ===================================================
        // PUBLIC RELAY METHODS (appelées par PickupRelay)
        // ===================================================

        public void HandlePickup()
        {
            _isHeld = true;
            _localPlayer = Networking.LocalPlayer;

            // Recaler l'origine du recul (nouvelle position dans la main)
            CaptureRecoilOrigin();

            // Transférer l'ownership du GameObject "Logic" au porteur.
            // Nécessite un VRCObjectSync sur ce GameObject (à ajouter dans TOUS les prefabs d'arme).
            // Sans VRCObjectSync, SetOwner échoue silencieusement → RequestSerialization() ne marche pas.
            bool wasOwner = Networking.IsOwner(gameObject);
            if (_localPlayer != null && !wasOwner)
                Networking.SetOwner(_localPlayer, gameObject);

            this.Log($"[FireHandler] HandlePickup — localPlayer={(_localPlayer != null ? _localPlayer.displayName : "NULL")}, wasOwner={wasOwner}, isOwnerNow={Networking.IsOwner(gameObject)}");
        }

        public void HandleDrop()
        {
            _isHeld = false;
            _triggerHeld = false;
            _isCharging = false;
            if (_beamRenderer != null) _beamRenderer.enabled = false;
            _beamActive = false;

            // Reset recul
            ResetRecoil();
        }

        public void HandlePickupUseDown()
        {
            _triggerHeld = true;
            _triggerJustPressed = true;
        }

        public void HandlePickupUseUp()
        {
            _triggerHeld = false;
            _triggerJustPressed = false;
            if (_isCharging && _chargeTimer > 0.05f)
                DoFire();
            _isCharging = false;
        }

        /// <summary>
        /// Input externe (sans pickup).
        /// </summary>
        public void FireInput(bool pressed)
        {
            if (pressed)
            {
                if (!_triggerHeld) _triggerJustPressed = true;
                _triggerHeld = true;
            }
            else
            {
                _triggerHeld = false;
                _triggerJustPressed = false;
                if (_isCharging && _chargeTimer > 0.05f) DoFire();
                _isCharging = false;
            }
        }

        // ===================================================
        // UPDATE
        // ===================================================

        protected override void Update()
        {
            if (_weapon == null) return;

            if (_fireCooldown > 0f)
                _fireCooldown -= Time.deltaTime;

            // Beam timer (accumule pour éviter spam réseau)
            if (_beamTimer > 0f)
                _beamTimer -= Time.deltaTime;

            // Relay batch : envoyer 1 cible par frame (projectile splash multi-cibles)
            if (_batchCount > 0 && _batchIndex < _batchCount)
            {
                var recv = _batchReceivers[_batchIndex];
                float dmg = _batchDamages[_batchIndex];
                _batchIndex++;
                if (recv != null && dmg > 0f)
                    SendRelay(dmg, _weapon != null ? _weapon.DamageTypeAsInt : 0, recv);

                // Nettoyer le batch une fois tout envoyé
                if (_batchIndex >= _batchCount)
                {
                    _batchCount = 0;
                    _batchIndex = 0;
                }
            }

            // Recoil visuel : retour progressif à la position d'origine
            UpdateRecoil();

            // Bloom : se résorbe quand on ne tire pas
            if (!_triggerHeld && _bloomAccum > 0f)
            {
                _bloomAccum -= _bloomDecay * Time.deltaTime;
                if (_bloomAccum < 0f) _bloomAccum = 0f;
                UpdateSpreadMult();
            }

            // Reload state machine
            if (_isReloading)
            {
                // Désactiver le beam renderer pendant le rechargement
                // (UpdateBeam() n'est pas appelé pendant le reload, donc on le fait ici)
                if (_beamRenderer != null) _beamRenderer.enabled = false;
                _beamActive = false;
                _syncedBeamActive = false;

                _reloadTimer -= Time.deltaTime;

                switch (_reloadPhase)
                {
                    case ReloadPhase.Start:
                        // Phase Start : attendre la fin, puis passer à Loop (séquentiel) ou fin (mag)
                        if (_reloadTimer <= 0f)
                        {
                            if (_isSequentialReload)
                            {
                                // Insérer la première balle
                                InsertOneShell();
                                if (_weapon.CurrentAmmo >= _weapon.Magazine)
                                {
                                    // Déjà plein ? Passer à End
                                    BeginReloadEnd();
                                }
                                else
                                {
                                    _reloadPhase = ReloadPhase.Loop;
                                    _reloadTimer = GetShellDuration();
                                }
                            }
                            else
                            {
                                // Rechargement mag : terminé
                                FinishReload();
                            }
                        }
                        break;

                    case ReloadPhase.Loop:
                        // Insérer une balle par tick
                        if (_reloadTimer <= 0f)
                        {
                            InsertOneShell();
                            if (_weapon.CurrentAmmo >= _weapon.Magazine)
                            {
                                BeginReloadEnd();
                            }
                            else
                            {
                                _reloadTimer = GetShellDuration();
                            }
                        }
                        break;

                    case ReloadPhase.End:
                        if (_reloadTimer <= 0f)
                        {
                            FinishReload();
                        }
                        break;
                }

                // Pendant le reload, on bloque le tir (sauf si le joueur tire pour interrompre)
                if (_triggerJustPressed && _isSequentialReload && _reloadPhase == ReloadPhase.Loop)
                {
                    // Interruption : on passe directement à la phase End
                    BeginReloadEnd();
                }
                _triggerJustPressed = false;
                return;
            }

            // Auto-reload quand vide
            if (_autoReload && _weapon != null && _weapon.NeedsReload() && !_isReloading)
            {
                ReloadWeapon();
                _triggerJustPressed = false;
                return;
            }

#if UNITY_EDITOR
            // Rechargement manuel avec la touche E (éditeur uniquement)
            if (Input.GetKeyDown(KeyCode.E) && !_isReloading)
            {
                ReloadWeapon();
            }
#endif

            switch (_fireMode)
            {
                case FireMode.FullAuto:   UpdateFullAuto(); break;
                case FireMode.SemiAuto:   UpdateSemiAuto(); break;
                case FireMode.Burst:      UpdateBurst(); break;
                case FireMode.Charge:
                case FireMode.ChargeBow:  UpdateCharge(); break;
                case FireMode.SingleShot: UpdateSingleShot(); break;
                case FireMode.Beam:       UpdateBeam(); break;
                case FireMode.Melee:
                case FireMode.Hybrid:     UpdateMelee(); break;
            }

            // Son tir à vide (sauf beam qui le gère dans UpdateBeam)
            if (_triggerJustPressed && _fireMode != FireMode.Beam && !CanFire())
                PlayEmptySound();

            _triggerJustPressed = false;
        }

        // ===================================================
        // FIRE MODES
        // ===================================================

        private void UpdateFullAuto()
        {
            if (_triggerHeld && _fireCooldown <= 0f && CanFire())
            {
                // Accumuler le bloom
                _bloomAccum += _bloomPerShot;
                if (_bloomAccum > _bloomMaxMult) _bloomAccum = _bloomMaxMult;
                UpdateSpreadMult();

                DoFire();
                _fireCooldown = _fireInterval;
            }
        }

        private void UpdateSemiAuto()
        {
            if (_triggerJustPressed && _fireCooldown <= 0f && CanFire())
            {
                _bloomAccum += _bloomPerShot * 0.5f;
                if (_bloomAccum > _bloomMaxMult) _bloomAccum = _bloomMaxMult;
                UpdateSpreadMult();

                DoFire();
                _fireCooldown = _fireInterval;
            }
        }

        private void UpdateBurst()
        {
            if (_triggerJustPressed && _burstRemaining <= 0 && _fireCooldown <= 0f && CanFire())
            {
                _burstRemaining = FireModeMapping.GetBurstCount(_weaponType);
                _burstTotal = _burstRemaining;
            }
            if (_burstRemaining > 0 && _fireCooldown <= 0f && CanFire())
            {
                // Bloom s'accumule + progression intra-rafale
                _bloomAccum += _bloomPerShot * 0.5f;
                if (_bloomAccum > _bloomMaxMult) _bloomAccum = _bloomMaxMult;
                float progress = 1f - ((float)_burstRemaining / (float)_burstTotal);
                _currentSpreadMult = 1f + progress * 2f + _bloomAccum;
                if (_currentSpreadMult > _bloomMaxMult + 3f) _currentSpreadMult = _bloomMaxMult + 3f;

                DoFire();
                _burstRemaining = _burstRemaining - 1;
                _fireCooldown = _burstDelay;
                if (_burstRemaining <= 0)
                    _fireCooldown = _fireInterval;
            }
        }

        private void UpdateCharge()
        {
            if (_triggerJustPressed && !_isCharging && CanFire())
            {
                _isCharging = true;
                _chargeTimer = 0f;
            }
            if (_isCharging && _triggerHeld)
            {
                _chargeTimer += Time.deltaTime;
                float ct = _weapon.ChargeTime;
                if (ct <= 0f) ct = 500f;
                if (_chargeTimer >= ct / 1000f)
                {
                    DoFire();
                    _isCharging = false;
                    _fireCooldown = _fireInterval;
                }
            }
        }

        private void UpdateSingleShot()
        {
            if (_triggerJustPressed && _fireCooldown <= 0f)
            {
                _bloomAccum += _bloomPerShot * 0.5f;
                if (_bloomAccum > _bloomMaxMult) _bloomAccum = _bloomMaxMult;
                UpdateSpreadMult();

                DoFire();
                _fireCooldown = _fireInterval;
            }
        }

        private void UpdateBeam()
        {
            // Non-owners : le beam est piloté par les variables [UdonSynced], pas par l'input local.
            // On applique l'état synchronisé chaque frame pour un rendu fluide.
            if (!Networking.IsOwner(gameObject))
            {
                if (_beamRenderer != null && _beamRenderer.positionCount >= 2)
                {
                    if (_syncedBeamActive)
                    {
                        _beamRenderer.enabled = true;
                        _beamRenderer.SetPosition(0, new Vector3(_syncedBeamStartX, _syncedBeamStartY, _syncedBeamStartZ));
                        _beamRenderer.SetPosition(1, new Vector3(_syncedBeamEndX, _syncedBeamEndY, _syncedBeamEndZ));
                    }
                    else
                    {
                        _beamRenderer.enabled = false;
                    }
                }
                return;
            }

            if (_triggerHeld)
            {
                // Vérifier les munitions ; arrêter le beam si vide
                if (!CanFire())
                {
                    if (!_beamEmptyPlayed)
                    {
                        PlayEmptySound();
                        _beamEmptyPlayed = true;
                    }
                    _beamActive = false;
                    _beamDamageAccum = 0f;
                    _beamHitTarget = null;
                    _beamTimer = 0f;
                    if (_beamRenderer != null) _beamRenderer.enabled = false;
                    _syncedBeamActive = false;
                    RequestSerialization();
                    if (_autoReload && _weapon != null && _weapon.NeedsReload())
                        ReloadWeapon();
                    return;
                }

                _beamActive = true;
                if (_beamRenderer != null && _beamRenderer.positionCount >= 2)
                {
                    _beamRenderer.enabled = true;
                    Vector3 origin = GetMuzzlePos();
                    Vector3 dir = GetMuzzleDir();
                    float beamRange = GetEffectiveBeamRange();

                    _beamRenderer.SetPosition(0, origin);
                    Vector3 beamEnd;
                    RaycastHit hit;
                    if (Physics.Raycast(origin, dir, out hit, beamRange, _beamLayerMask, QueryTriggerInteraction.Collide))
                    {
                        beamEnd = hit.point;
                        _beamRenderer.SetPosition(1, beamEnd);

                        if (hit.collider.gameObject.layer == _hitboxLayer)
                        {
                            float dps = _weapon.Impact * Time.deltaTime * 10f;
                            _beamDamageAccum += dps;
                            _beamHitTarget = hit.collider;
                        }

                        // Consommer une munition par pulse du beam (même sans hitbox)
                        if (_beamTimer <= 0f)
                        {
                            bool didFire = _weapon.Fire();
                            if (didFire)
                            {
                                PlayFireSound();
                                ApplyRecoilKick();

                                if (_beamDamageAccum > 0f && _beamHitTarget != null)
                                {
                                    var beamReceiver = GetDamageReceiver(_beamHitTarget);
                                    if (beamReceiver != null)
                                    {
                                        beamReceiver.SendDamage(_beamDamageAccum, _weapon.DamageTypeAsInt, _localPlayer);
                                        SendRelay(_beamDamageAccum, _weapon.DamageTypeAsInt, beamReceiver);
                                    }
                                }
                            }
                            _beamDamageAccum = 0f;
                            _beamHitTarget = null;
                            _beamTimer = 0.1f;

                            // Auto-reload si vide après consommation
                            if (_autoReload && _weapon.NeedsReload())
                                ReloadWeapon();
                        }
                    }
                    else
                    {
                        beamEnd = origin + dir * beamRange;
                        _beamRenderer.SetPosition(1, beamEnd);

                        // Même sans collision, consommer une munition par pulse
                        if (_beamTimer <= 0f)
                        {
                            bool didFire = _weapon.Fire();
                            if (didFire)
                            {
                                PlayFireSound();
                                ApplyRecoilKick();
                            }
                            _beamDamageAccum = 0f;
                            _beamHitTarget = null;
                            _beamTimer = 0.1f;

                            if (_autoReload && _weapon.NeedsReload())
                                ReloadWeapon();
                        }
                    }

                    // Sync beam visuel pour les autres joueurs
                    SyncBeam(origin, beamEnd);
                }
            }
            else
            {
                _beamActive = false;
                _beamEmptyPlayed = false;
                _beamDamageAccum = 0f;
                _beamHitTarget = null;
                _beamTimer = 0f;
                if (_beamRenderer != null) _beamRenderer.enabled = false;

                // Sync beam off
                _syncedBeamActive = false;
                RequestSerialization();
            }
        }

        private void SyncBeam(Vector3 start, Vector3 end)
        {
            _syncedBeamActive = true;
            _syncedBeamStartX = start.x;
            _syncedBeamStartY = start.y;
            _syncedBeamStartZ = start.z;
            _syncedBeamEndX = end.x;
            _syncedBeamEndY = end.y;
            _syncedBeamEndZ = end.z;
            RequestSerialization();
        }

        private void UpdateMelee()
        {
            if (_triggerJustPressed && _fireCooldown <= 0f)
            {
                _fireCooldown = _fireInterval;
                Vector3 pos = GetMuzzlePos() + GetMuzzleDir() * (_meleeRange * 0.5f);
                Collider[] hits = Physics.OverlapSphere(pos, _meleeRadius);
                foreach (var col in hits)
                {
                    var receiver = GetDamageReceiver(col);
                    if (receiver != null)
                    {
                        float dmg = _weapon.Impact * 2f;
                        receiver.SendDamage(dmg, _weapon.DamageTypeAsInt, _localPlayer);
                        SendRelay(dmg, _weapon.DamageTypeAsInt, receiver);
                    }
                }
            }
        }

        // ===================================================
        // FIRE EXECUTION
        // ===================================================

        private void DoFire()
        {
            if (_weapon == null) return;

            if (!_weapon.Fire()) return; // pas de munitions → pas de tir

            // Sync l'event de tir pour les autres joueurs (VFX seulement)
            _fireTick = _fireTick + 1;
            _syncedSpreadX = Random.Range(-1f, 1f);
            _syncedSpreadY = Random.Range(-1f, 1f);
            RequestSerialization();

            // Muzzle flash VFX (local)
            SpawnMuzzleFlash();

            // Fire sound
            PlayFireSound();

            // Recoil visuel
            ApplyRecoilKick();

            if (_useHitscan)
                DoHitscan();
            else if (_useProjectile)
            {
                SpawnProjectile();

                // Sync le spawn du projectile pour les clients distants
                Vector3 mPos = GetMuzzlePos();
                Vector3 mFwd = GetMuzzleRot() * Vector3.forward;
                _projPosX = mPos.x; _projPosY = mPos.y; _projPosZ = mPos.z;
                _projFwdX = mFwd.x; _projFwdY = mFwd.y; _projFwdZ = mFwd.z;
                _projectileFireTick = _projectileFireTick + 1;
            }

            // Auto-reload si vide après le tir
            if (_autoReload && _weapon.NeedsReload())
                ReloadWeapon();
        }

        // ===================================================
        // NETWORK DESERIALIZATION (VFX pour autres joueurs)
        // ===================================================

        public override void OnDeserialization()
        {
            // --- VFX : muzzle flash pour les autres joueurs ---
            if (_fireTick != _lastFireTick)
            {
                _lastFireTick = _fireTick;

                if (!Networking.IsOwner(gameObject))
                {
                    PlayFireSound();

                    if (_muzzleFlashPrefab != null)
                    {
                        Vector3 pos = GetMuzzlePos();
                        float spreadAngle = GetSpreadAngle();
                        Quaternion rot = GetMuzzleRot() * Quaternion.Euler(
                            _syncedSpreadX * spreadAngle,
                            _syncedSpreadY * spreadAngle,
                            0f);

                        GameObject flash = Instantiate(_muzzleFlashPrefab);
                        flash.transform.SetPositionAndRotation(pos, rot);
                        Destroy(flash, _muzzleFlashLifetime);
                    }
                }
            }

            // --- DAMAGE RELAY : la cible applique les dégâts ---
            if (_relaySequence != _lastRelaySeq)
            {
                _lastRelaySeq = _relaySequence;

                float dmg = _relayDamage;
                int dmgType = _relayDamageType;
                int shooterId = _relayShooterId;
                int targetId = _relayTargetId;
                int entityId = _relayEntityId;
                bool isNpc = _relayIsNpc;

                _relayDamage = 0f;

                int myId = Networking.LocalPlayer != null ? Networking.LocalPlayer.playerId : -1;
                VRCPlayerApi shooter = shooterId >= 0 ? VRCPlayerApi.GetPlayerById(shooterId) : null;

                // Skip si on est le tireur (déjà appliqué localement)
                if (shooterId == myId) { /* skip */ }
                else if (isNpc)
                {
                    // NPC/destructible : tout le monde (sauf tireur) applique
                    if (Manager != null)
                    {
                        var npc = Manager.GetNpcReceiverByEntityId(entityId);
                        if (npc != null)
                        {
                            npc.ApplyNetworkDamage(dmg, dmgType, shooter);
                        }
                        else if (targetId == myId)
                        {
                            // Fallback PvP : le hitbox était enregistré comme NPC mais
                            // le entityId ne correspond pas (ex: hitbox du master sur
                            // un client distant). On utilise le targetId de l'owner.
                            var receiver = Manager.GetReceiverByPlayerID(targetId);
                            if (receiver != null) receiver.ApplyNetworkDamage(dmg, dmgType, shooter);
                        }
                    }
                }
                else if (targetId == myId)
                {
                    // PvP : seul le joueur cible applique
                    if (Manager != null)
                    {
                        var receiver = Manager.GetReceiverByPlayerID(targetId);
                        if (receiver != null) receiver.ApplyNetworkDamage(dmg, dmgType, shooter);
                    }
                }
            }

            // --- PROJECTILE REMOTE SPAWN : les autres joueurs voient le projectile ---
            if (_projectileFireTick != _lastProjectileFireTick)
            {
                _lastProjectileFireTick = _projectileFireTick;

                if (!Networking.IsOwner(gameObject) && _useProjectile && _projectilePrefab != null)
                {
                    Vector3 projPos = new Vector3(_projPosX, _projPosY, _projPosZ);
                    Vector3 projFwd = new Vector3(_projFwdX, _projFwdY, _projFwdZ);
                    Quaternion projRot = Quaternion.LookRotation(projFwd);
                    SpawnVisualProjectile(projPos, projRot);
                }
            }

            // --- BEAM REMOTE RENDERING : les autres joueurs voient le beam ---
            if (!Networking.IsOwner(gameObject) && _beamRenderer != null && _beamRenderer.positionCount >= 2)
            {
                if (_syncedBeamActive)
                {
                    _beamRenderer.enabled = true;
                    _beamRenderer.SetPosition(0, new Vector3(_syncedBeamStartX, _syncedBeamStartY, _syncedBeamStartZ));
                    _beamRenderer.SetPosition(1, new Vector3(_syncedBeamEndX, _syncedBeamEndY, _syncedBeamEndZ));
                }
                else
                {
                    _beamRenderer.enabled = false;
                }
            }
        }

        // ===================================================
        // DAMAGE RELAY (écrit les [UdonSynced] vars du FireHandler)
        // ===================================================

        /// <summary>Relai réseau unifié. Appelé par hitscan, beam, melee, projectile.</summary>
        private void SendRelay(float damage, int damageType, M2922_DamageReceiver receiver)
        {
            if (_localPlayer == null)
            {
                this.Warning("[FireHandler] SendRelay ignoré: _localPlayer est NULL (HandlePickup pas appelé?)");
                return;
            }
            if (receiver == null) return;

            // 1. Player Object lié (BoundPlayerId >= 0) — PRIORITAIRE
            //    Doit être vérifié AVANT EntityId car les hitboxes de joueurs
            //    distants peuvent recevoir un EntityId incorrect via _PollAutoBind
            //    timeout → cela les ferait passer par le chemin NPC et casser le PvP.
            if (receiver.HitboxSystem != null && receiver.HitboxSystem.BoundPlayerId >= 0)
            {
                int boundId = receiver.HitboxSystem.BoundPlayerId;
                if (boundId == _localPlayer.playerId) return; // pas de relai sur soi-même
                _relayTargetId = boundId;
                _relayIsNpc = false;
                _relayEntityId = -1;
            }
            // 2. NPC/destructible (identifié par le HitboxSystem.EntityId)
            //    On stocke aussi _relayTargetId depuis l'owner pour le fallback PvP
            //    (cas du master dont le hitbox est incorrectement enregistré comme NPC).
            else if (receiver.HitboxSystem != null && receiver.HitboxSystem.EntityId >= 0)
            {
                VRCPlayerApi npcOwner = Networking.GetOwner(receiver.gameObject);
                _relayTargetId = (npcOwner != null) ? npcOwner.playerId : -1;
                _relayIsNpc = true;
                _relayEntityId = receiver.HitboxSystem.EntityId;
            }
            // 3. Fallback : utiliser Networking.GetOwner (dernier recours)
            else
            {
                VRCPlayerApi targetOwner = Networking.GetOwner(receiver.gameObject);
                if (targetOwner != null && targetOwner != _localPlayer)
                {
                    _relayTargetId = targetOwner.playerId;
                    _relayIsNpc = false;
                    _relayEntityId = -1;
                }
                else
                {
                    this.Warning("[FireHandler] SendRelay ignoré: cible non identifiable");
                    return;
                }
            }

            _relayShooterId = _localPlayer.playerId;

            // Reset l'accumulateur au début de chaque frame (le propriétaire
            // ne recoit PAS OnDeserialization, donc _relayDamage ne serait
            // jamais réinitialisé sans ça → bug des dégâts ×10+)
            if (_relayFrame != Time.frameCount)
            {
                _relayFrame = Time.frameCount;
                _relayDamage = 0f;
            }
            _relayDamage = _relayDamage + damage;
            _relayDamageType = damageType;
            _relaySequence = _relaySequence + 1;
            RequestSerialization();
        }

        // ===================================================
        // HITSCAN (raycast instantané)
        // ===================================================

        private void DoHitscan()
        {
            int pellets = FireModeMapping.GetPelletCount(_weaponType);
            float range = GetEffectiveRange();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 origin = GetMuzzlePos();
                Vector3 dir = GetSpreadDirection(pellets);

                RaycastHit hit;
                if (Physics.Raycast(origin, dir, out hit, range, _hitscanLayerMask, QueryTriggerInteraction.Collide))
                {
                    // Vérifier que c'est bien une hitbox (bon layer)
                    if (hit.collider.gameObject.layer == _hitboxLayer)
                    {
                        float damage = ComputeDamage(hit.distance, range);
                        ApplyHitDamage(hit, damage);

                        // Impact visuel
                        if (_hitEffectPrefab != null)
                        {
                            GameObject effect = Instantiate(_hitEffectPrefab);
                            effect.transform.position = hit.point;
                            effect.transform.rotation = Quaternion.LookRotation(hit.normal);
                        }
                    }
                }
            }
        }

        private void ApplyHitDamage(RaycastHit hit, float damage)
        {
            float zoneMult = 1f;
            var dmgMult = hit.collider.GetComponent<M2922_DamageMultiplier>();
            if (dmgMult != null)
                zoneMult = dmgMult.Multiplier;

            float finalDmg = damage * zoneMult;

            var receiver = GetDamageReceiver(hit.collider);
            if (receiver == null) return;

            // Appliquer localement
            receiver.SendDamage(finalDmg, _weapon.DamageTypeAsInt, _localPlayer);

            // Relai réseau
            SendRelay(finalDmg, _weapon.DamageTypeAsInt, receiver);
        }

        /// <summary>
        /// Relaie des dégâts vers un joueur/NPC distant.
        /// Appelé par les projectiles (roquettes, GL) qui n'ont pas de sync eux-mêmes.
        /// </summary>
        public void RelayDamage(float damage, int damageType, M2922_DamageReceiver receiver)
        {
            SendRelay(damage, damageType, receiver);
        }

        /// <summary>
        /// Ajoute un relay à la file batch (pour les projectiles qui touchent
        /// plusieurs cibles en une frame : splash damage). Les relays batch
        /// sont envoyés un par frame pour éviter l'écrasement des vars [UdonSynced].
        /// </summary>
        public void RelayDamageBatch(float damage, int damageType, M2922_DamageReceiver receiver)
        {
            if (receiver == null || _batchCount >= 16) return;

            // Déduplication : additionner si déjà dans le batch
            for (int i = 0; i < _batchCount; i++)
            {
                if (_batchReceivers[i] == receiver)
                {
                    _batchDamages[i] = _batchDamages[i] + damage;
                    return;
                }
            }

            _batchReceivers[_batchCount] = receiver;
            _batchDamages[_batchCount] = damage;
            _batchCount++;
        }

        private float ComputeDamage(float distance, float maxRange)
        {
            // Falloff : dégâts complets à courte portée, réduits à longue portée
            float baseDamage = _weapon.Impact * GetHitscanDamageMultiplier(_weaponType);

            // Début du falloff à 65% de la range max (Open World)
            float falloffStart = maxRange * 0.65f;
            if (distance <= falloffStart) return baseDamage;

            // Falloff linéaire de 100% à 40% des dégâts (au lieu de 50%)
            float t = (distance - falloffStart) / (maxRange - falloffStart);
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(baseDamage, baseDamage * 0.4f, t);
        }

        private float GetEffectiveRange()
        {
            float baseRange = FireModeMapping.GetHitscanRange(_weaponType);
            return baseRange + (_weapon.Range * 0.8f) + (_weapon.Zoom * 1.2f);
        }

        /// <summary>Multiplicateur de dégâts pour les armes à projectile (lanceurs).</summary>
        private float GetLauncherDamageMultiplier(WeaponType wt)
        {
            switch (wt)
            {
                case WeaponType.RocketLauncher:               return 5f;
                case WeaponType.HeavyGrenadeLauncher:          return 3f;
                case WeaponType.BreechLoadedGrenadeLauncher:   return 2.5f;
                case WeaponType.RocketSidearm:                 return 2.5f;
                default:                                       return 2f; // fallback
            }
        }

        /// <summary>Multiplicateur de dégâts pour les armes hitscan (raycast).</summary>
        private float GetHitscanDamageMultiplier(WeaponType wt)
        {
            switch (wt)
            {
                case WeaponType.MachineGun:              return 1f;
                default:                                 return 0.5f;
            }
        }

        /// <summary>Portée du beam basée sur la stat Range de l'arme.</summary>
        private float GetEffectiveBeamRange()
        {
            float baseRange = FireModeMapping.GetHitscanRange(_weaponType);
            return baseRange + (_weapon.Range * 1.5f) + (_weapon.Zoom * 1.5f);
        }

        private Vector3 GetSpreadDirection(int pellets)
        {
            Quaternion baseRot = GetMuzzleRot();
            float baseAngle = GetSpreadAngle() * _currentSpreadMult;

            if (pellets <= 1)
            {
                float x = Random.Range(-baseAngle, baseAngle);
                float y = Random.Range(-baseAngle, baseAngle);
                return baseRot * Quaternion.Euler(x, y, 0f) * Vector3.forward;
            }

            // Multi-pellet (Shotgun/Fusion) : cône élargi
            float mult = 8f;
            float angleX = Random.Range(-mult, mult) * baseAngle;
            float angleY = Random.Range(-mult, mult) * baseAngle;
            return baseRot * Quaternion.Euler(angleX, angleY, 0f) * Vector3.forward;
        }

        /// <summary>
        /// Retourne l'angle de spread (demi-angle du cône) basé sur Range, Stability, AimAssistance.
        /// </summary>
        private float GetSpreadAngle()
        {
            float rangeFactor = Mathf.Clamp(1f - (_weapon.Range / 100f), 0.1f, 1f);
            float stabilityFactor = Mathf.Clamp(1f - (_weapon.Stability / 100f), 0.1f, 1f);
            float aaFactor = Mathf.Clamp(1f - (_weapon.AimAssistance / 100f), 0.1f, 1f);
            return 2f * rangeFactor * stabilityFactor * aaFactor;
        }

        /// <summary>
        /// Met à jour _currentSpreadMult selon le bloom accumulé.
        /// </summary>
        private void UpdateSpreadMult()
        {
            _currentSpreadMult = 1f + _bloomAccum;
        }

        // ===================================================
        // PROJECTILE (roquettes, grenades)
        // ===================================================

        private void SpawnProjectile()
        {
            if (_pool == null || _pool.Length == 0) return;

            M2922_Projectile proj = null;
            int attempts = 0;
            while (attempts < _poolSize)
            {
                if (!_pool[_poolIndex].IsActive)
                {
                    proj = _pool[_poolIndex];
                    _poolIndex = (_poolIndex + 1) % _poolSize;
                    break;
                }
                _poolIndex = (_poolIndex + 1) % _poolSize;
                attempts++;
            }
            if (proj == null) return;

            Vector3 pos = GetMuzzlePos();
            Quaternion rot = GetMuzzleRot();

            // --- Calculs depuis les stats de l'arme ---
            float dmgMult = GetLauncherDamageMultiplier(_weaponType);
            float baseImpact = _weapon.Impact * dmgMult;
            float blastStat = Mathf.Clamp(_weapon.BlastRadius, 0f, 100f);
            float velocityStat = Mathf.Clamp(_weapon.Velocity, 0f, 100f);

            // Ratio de répartition impact/explosion (Blast Radius pilote)
            float splashRatio = Mathf.Lerp(0.20f, 0.80f, blastStat / 100f);
            float directRatio = 1f - splashRatio;

            // Multiplicateur Velocity sur l'impact direct (+50% max si Velocity=100)
            float velocityMult = 1f + (velocityStat / 100f) * 0.5f;

            // Dégâts
            float directDmg = (baseImpact * directRatio) * velocityMult;
            float splashDmg = baseImpact * splashRatio;

            // Rayon d'explosion (2m → 8m)
            float explRadius = Mathf.Lerp(2.0f, 8.0f, blastStat / 100f);

            // Vitesse du projectile
            float speed = 15f + velocityStat * 0.5f;
            float lifetime = 5f;

            float stability = _weapon.Stability;
            float aimAssist = _weapon.AimAssistance;

            // Gravité : GL = 2.5×, Rockets = 0.5×, autres = 1×
            float gravityScale = IsGrenadeLauncher(_weaponType) ? 2.5f : (IsRocketLauncher(_weaponType) ? 0.5f : 1f);

            proj.Launch(pos, rot, speed,
                directDmg, splashDmg, explRadius,
                _weapon.DamageTypeAsInt, lifetime,
                stability, aimAssist, velocityStat,
                _weapon.WeaponTypeAsInt, gravityScale,
                _localPlayer, this);
        }

        private bool IsGrenadeLauncher(WeaponType wt)
        {
            return wt == WeaponType.BreechLoadedGrenadeLauncher
                || wt == WeaponType.HeavyGrenadeLauncher;
        }

        private bool IsRocketLauncher(WeaponType wt)
        {
            return wt == WeaponType.RocketLauncher
                || wt == WeaponType.RocketSidearm;
        }

        public void ReturnProjectile(M2922_Projectile proj) { }

        /// <summary>
        /// Spawn un projectile visuel uniquement pour les clients distants.
        /// Même pool, mêmes stats de trajectoire, mais aucun dégât.
        /// </summary>
        private void SpawnVisualProjectile(Vector3 pos, Quaternion rot)
        {
            if (_pool == null || _pool.Length == 0) return;

            M2922_Projectile proj = null;
            int attempts = 0;
            int idx = _poolIndex;
            while (attempts < _poolSize)
            {
                if (!_pool[idx].IsActive)
                {
                    proj = _pool[idx];
                    break;
                }
                idx = (idx + 1) % _poolSize;
                attempts++;
            }
            if (proj == null) return;

            // Mêmes calculs de stats que SpawnProjectile() pour la trajectoire
            float velocityStat = Mathf.Clamp(_weapon.Velocity, 0f, 100f);
            float blastStat = Mathf.Clamp(_weapon.BlastRadius, 0f, 100f);
            float speed = 15f + velocityStat * 0.5f;
            float explRadius = Mathf.Lerp(2.0f, 8.0f, blastStat / 100f);
            float lifetime = 5f;
            float stability = _weapon.Stability;
            float aimAssist = _weapon.AimAssistance;
            float gravityScale = IsGrenadeLauncher(_weaponType) ? 2.5f : (IsRocketLauncher(_weaponType) ? 0.5f : 1f);

            proj.LaunchVisual(pos, rot, speed,
                explRadius, lifetime,
                stability, aimAssist, velocityStat,
                _weapon.WeaponTypeAsInt, gravityScale,
                this);
        }

        // ===================================================
        // HELPERS
        // ===================================================

        private bool CanFire()
        {
            if (_isReloading) return false;
            if (_fireMode == FireMode.SingleShot) return true;
            return _weapon.CurrentAmmo > 0 || _weapon.InfiniteAmmo;
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.PlayOneShot(clip, volume);
        }

        private void PlayFireSound()   { PlaySound(_fireSound, _fireVolume); }
        private void PlayReloadSound() { PlaySound(_reloadSound, _reloadVolume); }
        private void PlayEmptySound()  { PlaySound(_emptySound, _emptyVolume); }

        // ===================================================
        // RECOIL VISUEL
        // ===================================================

        /// <summary>Calcule les valeurs de recul depuis les stats de l'arme.</summary>
        private void ComputeRecoilStats()
        {
            if (_weapon == null) return;

            float impact = _weapon.Impact;
            float stability = _weapon.Stability;
            float handling = _weapon.Handling;
            float recoilDir = _weapon.RecoilDirection;

            // KickBack : Impact élevé = plus de recul, Stability élevée = moins de recul
            float impactFactor = Mathf.Clamp(impact / 100f, 0.2f, 1.5f);
            float stabFactor = Mathf.Clamp(1f - (stability / 100f), 0.2f, 1f);
            _finalRecoilKickBack = _recoilKickBack > 0f
                ? _recoilKickBack
                : 0.008f + (impactFactor * stabFactor * 0.025f);

            // KickUp : Stability + RecoilDirection
            float dirFactor = Mathf.Clamp(recoilDir / 100f, 0f, 1f);
            _finalRecoilKickUp = _recoilKickUp > 0f
                ? _recoilKickUp
                : 1f + (stabFactor * 3f) - (dirFactor * 1.5f);

            // KickSide : RecoilDirection pilote le côté
            _finalRecoilKickSide = _recoilKickSide > 0f
                ? _recoilKickSide
                : 0.2f + (dirFactor * 1.5f);

            // Recovery : Handling élevé = retour plus rapide
            float handlingFactor = Mathf.Clamp(handling / 100f, 0.2f, 1.5f);
            _finalRecoilRecoverySpeed = _recoilRecoverySpeed > 0f
                ? _recoilRecoverySpeed
                : 6f + (handlingFactor * 10f);

            this.Log($"[Recoil] kickBack={_finalRecoilKickBack:F3} kickUp={_finalRecoilKickUp:F1} kickSide={_finalRecoilKickSide:F1} recovery={_finalRecoilRecoverySpeed:F1}");
        }

        private void CaptureRecoilOrigin()
        {
            if (_weaponModelRoot != null)
            {
                _recoilOriginalLocalPos = _weaponModelRoot.localPosition;
                _recoilOriginalLocalRot = _weaponModelRoot.localRotation;
            }
        }

        private void ResetRecoil()
        {
            if (_weaponModelRoot != null)
            {
                _weaponModelRoot.localPosition = _recoilOriginalLocalPos;
                _weaponModelRoot.localRotation = _recoilOriginalLocalRot;
            }
        }

        /// <summary>Applique le kick de recul instantané.</summary>
        private void ApplyRecoilKick()
        {
            if (_weaponModelRoot == null) return;
            if (!_isHeld) return; // pas de recul si l'arme n'est pas tenue (Rigidbody)

            float side = Random.Range(-_finalRecoilKickSide, _finalRecoilKickSide);
            _weaponModelRoot.localPosition += Vector3.back * _finalRecoilKickBack;
            _weaponModelRoot.localRotation *= Quaternion.Euler(-_finalRecoilKickUp, side, 0f);
        }

        /// <summary>Retour progressif à la position d'origine (appelé chaque frame).</summary>
        private void UpdateRecoil()
        {
            if (_weaponModelRoot == null) return;
            if (!_isHeld) return; // pas de recul si l'arme n'est pas tenue (Rigidbody)

            float t = 1f - Mathf.Exp(-_finalRecoilRecoverySpeed * Time.deltaTime);
            _weaponModelRoot.localPosition = Vector3.Lerp(
                _weaponModelRoot.localPosition, _recoilOriginalLocalPos, t);
            _weaponModelRoot.localRotation = Quaternion.Slerp(
                _weaponModelRoot.localRotation, _recoilOriginalLocalRot, t);
        }

        /// <summary>
        /// Trouve le DamageReceiver associé au collider touché.
        /// Utilise le M2922_HitboxSystem (même GameObject que le DamageReceiver)
        /// et sa méthode IsMyCollider() pour valider l'appartenance.
        /// </summary>
        private M2922_DamageReceiver GetDamageReceiver(Collider col)
        {
            var receiver = col.GetComponent<M2922_DamageReceiver>();
            if (receiver != null) return receiver;

            receiver = col.GetComponentInParent<M2922_DamageReceiver>();
            if (receiver != null) return receiver;

            // Fallback : le collider est sur une branche sœur (ex: Head Collider
            // sous Armature, alors que DamageReceiver + HitboxSystem sont sous
            // Player Logic). On cherche le HitboxSystem qui possède ce collider,
            // puis on prend le DamageReceiver sur le même GameObject.
            Transform t = col.transform.parent;
            while (t != null)
            {
                var hitboxSys = t.GetComponentInChildren<M2922_HitboxSystem>();
                if (hitboxSys != null && hitboxSys.IsMyCollider(col))
                    return hitboxSys.GetComponent<M2922_DamageReceiver>();
                t = t.parent;
            }
            return null;
        }

        private Vector3 GetMuzzlePos()
        {
            if (_muzzlePoint != null) return _muzzlePoint.position;
            return transform.position;
        }
        private Quaternion GetMuzzleRot()
        {
            if (_muzzlePoint != null) return _muzzlePoint.rotation;
            return transform.rotation;
        }
        private Vector3 GetMuzzleDir()
        {
            if (_muzzlePoint != null) return _muzzlePoint.forward;
            return transform.forward;
        }

        // ===================================================
        // VFX
        // ===================================================

        private void SpawnMuzzleFlash()
        {
            if (_muzzleFlashPrefab == null) return;

            Vector3 pos = GetMuzzlePos();
            // Même direction que le spread (GetSpreadDirection avec pellets=1 pour angle neutre)
            Quaternion rot = GetMuzzleRot() * GetSpreadRotation();

            GameObject flash = Instantiate(_muzzleFlashPrefab);
            flash.transform.SetPositionAndRotation(pos, rot);
            Destroy(flash, _muzzleFlashLifetime);
        }

        private Quaternion GetSpreadRotation()
        {
            float baseSpread = GetSpreadAngle();
            float spreadX = Random.Range(-baseSpread, baseSpread);
            float spreadY = Random.Range(-baseSpread, baseSpread);
            return Quaternion.Euler(spreadX, spreadY, 0f);
        }

        // ===================================================
        // PUBLIC API
        // ===================================================

        /// <summary>
        /// Active/désactive le tir continu. Pour tourelle ou arme montée.
        /// </summary>
        public void SetFiring(bool firing)
        {
            FireInput(firing);
        }

        /// <summary>
        /// Force un tir immédiat.
        /// </summary>
        public void ForceFire()
        {
            if (_fireCooldown <= 0f && CanFire())
            {
                DoFire();
                _fireCooldown = _fireInterval;
            }
        }

        /// <summary>
        /// Calcule la durée de rechargement mag (non-séquentiel).
        /// </summary>
        private float CalculateReloadDuration()
        {
            if (_weapon == null) return 2.5f;

            float baseTime = FireModeMapping.GetBaseReloadTime(_weaponType);
            float reloadStat = Mathf.Clamp(_weapon.ReloadSpeed, 0f, 100f);
            float reduction = (reloadStat * 0.5f) / 100f;
            float reloadTime = baseTime * (1f - reduction);

            // Tactical reload : 10% plus rapide si chargeur pas complètement vide
            if (_weapon.CurrentAmmo > 0)
                reloadTime *= 0.9f;

            return reloadTime;
        }

        /// <summary>
        /// Calcule la durée d'une phase de rechargement séquentiel.
        /// speedMultiplier = 1.0 + reloadStat/100  (1x à stat 0, 2x à stat 100)
        /// </summary>
        private float GetSequentialPhaseDuration(float baseDuration)
        {
            float reloadStat = Mathf.Clamp(_weapon != null ? _weapon.ReloadSpeed : 50f, 0f, 100f);
            float speedMultiplier = 1f + (reloadStat / 100f);
            return baseDuration / speedMultiplier;
        }

        private float GetShellDuration()
        {
            float ignoreStart, perShell, ignoreEnd;
            FireModeMapping.GetSequentialReloadTimes(_weaponType, out ignoreStart, out perShell, out ignoreEnd);
            return GetSequentialPhaseDuration(perShell);
        }

        /// <summary>Insère une balle dans le chargeur (rechargement séquentiel).</summary>
        private void InsertOneShell()
        {
            if (_weapon == null) return;
            _weapon.ReloadOne();
            this.Log("Reload +1 balle (" + _weapon.CurrentAmmo.ToString() + "/" + _weapon.Magazine.ToString() + ")");
        }

        /// <summary>Passe à la phase End du rechargement séquentiel.</summary>
        private void BeginReloadEnd()
        {
            _reloadPhase = ReloadPhase.End;
            float ignS, ignP, baseEnd;
            FireModeMapping.GetSequentialReloadTimes(_weaponType, out ignS, out ignP, out baseEnd);
            _reloadTimer = GetSequentialPhaseDuration(baseEnd);
        }

        /// <summary>Termine le rechargement (mag ou séquentiel).</summary>
        private void FinishReload()
        {
            _isReloading = false;
            _reloadPhase = ReloadPhase.None;

            if (!_isSequentialReload && _weapon != null)
                _weapon.Reload(); // Mag : remplir le chargeur au max

            _isSequentialReload = false;
            this.Log("Rechargement termine (" + (_weapon != null ? _weapon.CurrentAmmo.ToString() + "/" + _weapon.Magazine.ToString() : "?") + ")");
        }

        public void ForceReload()
        {
            ReloadWeapon();
        }

        /// <summary>
        /// Démarre le rechargement (mag complet ou séquentiel selon le type d'arme).
        /// </summary>
        public void ReloadWeapon()
        {
            if (_weapon == null) return;
            if (_weapon.InfiniteAmmo) return;
            if (_isReloading) return;
            if (_weapon.CurrentAmmo >= _weapon.Magazine) return;

            _isReloading = true;
            _isSequentialReload = IsSequential();

            // Reload sound
            PlayReloadSound();

            if (_isSequentialReload)
            {
                // Rechargement balle par balle : phase Start
                _reloadPhase = ReloadPhase.Start;
                float baseStart, ignP, ignE;
                FireModeMapping.GetSequentialReloadTimes(_weaponType, out baseStart, out ignP, out ignE);
                _reloadTimer = GetSequentialPhaseDuration(baseStart);
                this.Log("Rechargement seq. START (" + _reloadTimer.ToString("F1") + "s)");
            }
            else
            {
                // Rechargement mag classique
                _reloadPhase = ReloadPhase.Start;
                _reloadTimer = CalculateReloadDuration();
                this.Log("Rechargement mag (" + _reloadTimer.ToString("F1") + "s)");
            }
        }

        public bool IsFiring()
        {
            return _triggerHeld || _burstRemaining > 0 || _isCharging || _beamActive;
        }

        public bool IsReloading()
        {
            return _isReloading;
        }

        /// <summary>
        /// True si l'arme utilise le rechargement séquentiel (balle par balle).
        /// </summary>
        public bool IsSequentialReload()
        {
            return _isSequentialReload;
        }

        /// <summary>
        /// Résout le style de rechargement effectif :
        /// Priorité : Frame → type mapping. La frame peut override le défaut du type.
        /// </summary>
        private bool IsSequential()
        {
            if (_weapon == null) return false;
            int frameStyle = _weapon.FrameReloadStyle;
            if (frameStyle == (int)ReloadStyle.Sequential) return true;
            if (frameStyle == (int)ReloadStyle.Magazine) return false;
            // Default : suivre le mapping par type
            return FireModeMapping.IsSequentialReload(_weaponType);
        }

        /// <summary>
        /// Appelé par le MagazineWell : insère une balle (séquentiel)
        /// ou lance un rechargement complet (mag).
        /// </summary>
        public void TryInsertShellOrReload()
        {
            if (_weapon == null) return;
            if (_weapon.InfiniteAmmo) return;
            if (_weapon.CurrentAmmo >= _weapon.Magazine) return;

            bool sequential = IsSequential();

            if (sequential)
            {
                if (_isReloading && _reloadPhase == ReloadPhase.Loop)
                {
                    // Déjà en train de recharger en boucle : insérer une balle maintenant
                    InsertOneShell();
                    _reloadTimer = GetShellDuration(); // reset timer
                    if (_weapon.CurrentAmmo >= _weapon.Magazine)
                        BeginReloadEnd();
                }
                else if (!_isReloading)
                {
                    // Démarrer un rechargement séquentiel
                    ReloadWeapon();
                }
                // Si en phase Start ou End, on ignore (laisser l'animation se finir)
            }
            else
            {
                // Rechargement mag classique
                ReloadWeapon();
            }
        }

        public float ReloadProgress()
        {
            if (!_isReloading) return 0f;

            if (_isSequentialReload)
            {
                // Progress basé sur le nombre de balles insérées vs balles manquantes
                int needed = _weapon.Magazine - _weapon.CurrentAmmo;
                int totalMissing = _weapon.Magazine; // approximation
                if (totalMissing <= 0) return 1f;
                float shellProgress = 1f - ((float)needed / (float)totalMissing);
                return Mathf.Clamp01(shellProgress);
            }
            else
            {
                float total = CalculateReloadDuration();
                return total > 0f ? 1f - (_reloadTimer / total) : 1f;
            }
        }

        /// <summary>
        /// Pour usage externe (tourelle) : rotation libre visée.
        /// </summary>
        public void SetMuzzleRotation(Quaternion rot)
        {
            if (_muzzlePoint != null)
                _muzzlePoint.rotation = rot;
            else
                transform.rotation = rot;
        }

        public Vector3 GetMuzzlePosition()
        {
            return GetMuzzlePos();
        }

        public float GetFireRange()
        {
            if (_useHitscan)
                return GetEffectiveRange();
            return 50f;
        }

        // ===================================================
        // EDITOR
        // ===================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_weapon == null) _weapon = GetComponent<M2922_Weapon>();
            if (_beamRenderer == null) _beamRenderer = GetComponent<LineRenderer>();

            // Résoudre le layer hitbox depuis le nom
            _hitboxLayer = UnityEngine.LayerMask.NameToLayer(_hitboxLayerName);
            if (_hitboxLayer < 0) _hitboxLayer = 8;

            // Auto-set le layerMask pour le hitscan (hitbox uniquement)
            if (_hitboxLayer >= 0)
                _hitscanLayerMask = (1 << _hitboxLayer);

            // Beam layer mask : hitbox + Default (murs, environnement)
            int defaultLayer = 0; // Layer 0 = Default
            _beamLayerMask = (1 << _hitboxLayer) | (1 << defaultLayer);

            // Propager le layer au prefab projectile
            if (_projectilePrefab != null)
            {
                var proj = _projectilePrefab.GetComponent<M2922_Projectile>();
                if (proj != null)
                {
                    // Le projectile a son propre champ _hitboxLayer
                    // On le set via serializedProperty ou directement
                    var so = new UnityEditor.SerializedObject(proj);
                    so.FindProperty("_hitboxLayer").intValue = _hitboxLayer;
                    so.ApplyModifiedProperties();
                }
            }
        }

        protected override void AutoDetectReferences()
        {
            base.AutoDetectReferences();
            if (_weapon == null) _weapon = GetComponent<M2922_Weapon>();
            if (_muzzlePoint == null)
            {
                var t = transform.Find("Muzzle");
                if (t != null) _muzzlePoint = t;
            }
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            DrawSpreadGizmo(0.15f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            DrawSpreadGizmo(0.6f);
            DrawLauncherPreviewGizmo();
        }

        private void DrawLauncherPreviewGizmo()
        {
            if (_weapon == null) return;

            float blastStat = Mathf.Clamp(_weapon.BakedFrameBlastRadius, 0f, 100f);
            if (blastStat <= 0f) return;

            float splashRatio = Mathf.Lerp(0.20f, 0.80f, blastStat / 100f);
            float directRatio = 1f - splashRatio;
            float velStat = Mathf.Clamp(_weapon.BakedFrameVelocity, 0f, 100f);
            float velMult = 1f + (velStat / 100f) * 0.5f;
            float baseImpact = _weapon.BakedFrameImpact;
            float directDmg = (baseImpact * directRatio) * velMult;
            float splashDmg = baseImpact * splashRatio;
            float explRadius = Mathf.Lerp(2.0f, 8.0f, blastStat / 100f);

            Vector3 muzzlePos = GetMuzzlePos();
            float alpha = 0.8f;

            // Rayon d'explosion au muzzle
            UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, alpha * 0.5f);
            UnityEditor.Handles.DrawWireDisc(muzzlePos, Vector3.up, explRadius);
            UnityEditor.Handles.DrawWireDisc(muzzlePos, Vector3.right, explRadius);
            UnityEditor.Handles.DrawWireDisc(muzzlePos, Vector3.forward, explRadius);

            // Label stats lanceur
            UnityEditor.Handles.color = new Color(1f, 0.6f, 0.2f, alpha);
            float labelY = muzzlePos.y + explRadius + 0.3f;
            UnityEditor.Handles.Label(
                new Vector3(muzzlePos.x, labelY, muzzlePos.z),
                $"Rocket: Dir={directDmg:F0}  Splash={splashDmg:F0}  R={explRadius:F1}m  Spd={15f + velStat * 0.5f:F0}m/s");
        }

        private void DrawSpreadGizmo(float alpha)
        {
            if (_weapon == null) return;

            Vector3 origin = GetMuzzlePos();
            Quaternion baseRot = GetMuzzleRot();
            float range = 10f;

            // Utiliser les stats BAKÉES (finales = 0 en éditeur avant Start)
            float bkdRange = _weapon.BakedFrameRange;
            float bkdStab = _weapon.BakedFrameStability;
            float bkdAA = _weapon.BakedFrameAimAssistance;
            float rangeFactor = Mathf.Clamp(1f - (bkdRange / 100f), 0.1f, 1f);
            float stabilityFactor = Mathf.Clamp(1f - (bkdStab / 100f), 0.1f, 1f);
            float aaFactor = Mathf.Clamp(1f - (bkdAA / 100f), 0.1f, 1f);
            float baseSpread = 2f * rangeFactor * stabilityFactor * aaFactor;

            // Direction centrale
            Vector3 centerDir = baseRot * Vector3.forward;

            // Cône : lignes de bord à ±baseSpread degrés
            Quaternion rotUp = baseRot * Quaternion.Euler(0f, baseSpread, 0f);
            Quaternion rotDown = baseRot * Quaternion.Euler(0f, -baseSpread, 0f);
            Quaternion rotLeft = baseRot * Quaternion.Euler(baseSpread, 0f, 0f);
            Quaternion rotRight = baseRot * Quaternion.Euler(-baseSpread, 0f, 0f);

            Vector3 pCenter = origin + centerDir * range;
            Vector3 pUp = origin + rotUp * Vector3.forward * range;
            Vector3 pDown = origin + rotDown * Vector3.forward * range;
            Vector3 pLeft = origin + rotLeft * Vector3.forward * range;
            Vector3 pRight = origin + rotRight * Vector3.forward * range;

            // Ligne centrale
            Gizmos.color = new Color(1f, 1f, 1f, alpha);
            Gizmos.DrawLine(origin, pCenter);

            // Lignes de cône
            Gizmos.color = new Color(1f, 0.5f, 0f, alpha);
            Gizmos.DrawLine(origin, pUp);
            Gizmos.DrawLine(origin, pDown);
            Gizmos.DrawLine(origin, pLeft);
            Gizmos.DrawLine(origin, pRight);

            // Cercle à la fin du cône
            int segments = 16;
            Vector3 prevPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Quaternion rot = baseRot * Quaternion.Euler(
                    Mathf.Sin(angle) * baseSpread,
                    Mathf.Cos(angle) * baseSpread,
                    0f
                );
                Vector3 point = origin + rot * Vector3.forward * range;
                if (i > 0)
                    Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            // Cercle au milieu
            Gizmos.color = new Color(1f, 0.5f, 0f, alpha * 0.5f);
            prevPoint = Vector3.zero;
            float midRange = range * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Quaternion rot = baseRot * Quaternion.Euler(
                    Mathf.Sin(angle) * baseSpread,
                    Mathf.Cos(angle) * baseSpread,
                    0f
                );
                Vector3 point = origin + rot * Vector3.forward * midRange;
                if (i > 0)
                    Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            // Label spread
            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, alpha);
            UnityEditor.Handles.Label(pCenter + Vector3.up * 0.2f,
                "Base: " + baseSpread.ToString("F1") + "°");

            // === Cône MAX BLOOM (rouge, plus court) ===
            float maxSpread = baseSpread * (1f + _bloomMaxMult);
            float maxRange = range * 0.7f;
            Color maxCol = new Color(1f, 0.2f, 0.2f, alpha * 0.35f);

            Gizmos.color = maxCol;
            Gizmos.DrawLine(origin, origin + baseRot * Quaternion.Euler(0f, maxSpread, 0f) * Vector3.forward * maxRange);
            Gizmos.DrawLine(origin, origin + baseRot * Quaternion.Euler(0f, -maxSpread, 0f) * Vector3.forward * maxRange);
            Gizmos.DrawLine(origin, origin + baseRot * Quaternion.Euler(maxSpread, 0f, 0f) * Vector3.forward * maxRange);
            Gizmos.DrawLine(origin, origin + baseRot * Quaternion.Euler(-maxSpread, 0f, 0f) * Vector3.forward * maxRange);

            prevPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Quaternion r = baseRot * Quaternion.Euler(Mathf.Sin(a) * maxSpread, Mathf.Cos(a) * maxSpread, 0f);
                Vector3 pt = origin + r * Vector3.forward * maxRange;
                if (i > 0) Gizmos.DrawLine(prevPoint, pt);
                prevPoint = pt;
            }

            Vector3 mCtr = origin + centerDir * maxRange;
            UnityEditor.Handles.color = maxCol;
            UnityEditor.Handles.Label(mCtr + Vector3.up * 0.15f,
                "Max Bloom: " + maxSpread.ToString("F1") + "°");

        }

#endif
    }
}
