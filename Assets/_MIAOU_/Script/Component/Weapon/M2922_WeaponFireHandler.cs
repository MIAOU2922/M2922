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

        // --- NETWORK BEAM (Trace Rifle) ---
        [UdonSynced] private bool _syncedBeamActive = false;
        [UdonSynced] private float _syncedBeamStartX, _syncedBeamStartY, _syncedBeamStartZ;
        [UdonSynced] private float _syncedBeamEndX, _syncedBeamEndY, _syncedBeamEndZ;

        // --- NETWORK PLAYER DAMAGE RELAY ---
        [UdonSynced] private int _relayedTargetID = -1;
        [UdonSynced] private float _relayedDamage = 0f;
        [UdonSynced] private int _relayedDamageType = 0;
        [UdonSynced] private int _relaySequence = 0;
        private int _lastRelaySequence = -1;

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
                obj.transform.SetParent(transform);
                obj.transform.localPosition = Vector3.zero;
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

            // Transférer ownership du child (WPN_Data) au joueur local.
            // Le VRC Pickup ne transfère que le parent — l'enfant
            // doit être transféré manuellement pour que RequestSerialization() marche.
            if (_localPlayer != null && !Networking.IsOwner(gameObject))
                Networking.SetOwner(_localPlayer, gameObject);
        }

        public void HandleDrop()
        {
            _isHeld = false;
            _triggerHeld = false;
            _isCharging = false;
            if (_beamRenderer != null) _beamRenderer.enabled = false;
            _beamActive = false;
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
            if (_triggerHeld)
            {
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
                            if (_beamTimer <= 0f)
                            {
                                if (_beamDamageAccum > 0f && _beamHitTarget != null)
                                {
                                    var beamReceiver = _beamHitTarget.GetComponent<M2922_DamageReceiver>();
                                    if (beamReceiver == null)
                                        beamReceiver = _beamHitTarget.GetComponentInParent<M2922_DamageReceiver>();
                                    if (beamReceiver != null)
                                    {
                                        beamReceiver.SendDamage(_beamDamageAccum, _weapon.DamageTypeAsInt, _localPlayer);

                                        VRCPlayerApi targetOwner = Networking.GetOwner(beamReceiver.gameObject);
                                        if (targetOwner != null && targetOwner != _localPlayer)
                                        {
                                            _relayedTargetID = targetOwner.playerId;
                                            _relayedDamage = _beamDamageAccum;
                                            _relayedDamageType = _weapon.DamageTypeAsInt;
                                            _relaySequence = _relaySequence + 1;
                                            RequestSerialization();
                                        }
                                    }
                                }
                                _beamDamageAccum = 0f;
                                _beamTimer = 0.1f;
                            }
                        }
                    }
                    else
                    {
                        beamEnd = origin + dir * beamRange;
                        _beamRenderer.SetPosition(1, beamEnd);
                    }

                    // Sync beam visuel pour les autres joueurs
                    SyncBeam(origin, beamEnd);
                }
            }
            else
            {
                _beamActive = false;
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
                    var receiver = col.GetComponent<M2922_DamageReceiver>();
                    if (receiver != null)
                        receiver.SendDamage(_weapon.Impact * 2f, _weapon.DamageTypeAsInt, _localPlayer);
                }
            }
        }

        // ===================================================
        // FIRE EXECUTION
        // ===================================================

        private void DoFire()
        {
            if (_weapon == null) return;

            _weapon.Fire(); // consomme munition + son propre RequestSerialization

            // Sync l'event de tir pour les autres joueurs (VFX seulement)
            _fireTick = _fireTick + 1;
            _syncedSpreadX = Random.Range(-1f, 1f);
            _syncedSpreadY = Random.Range(-1f, 1f);
            RequestSerialization();

            // Muzzle flash VFX (local)
            SpawnMuzzleFlash();

            if (_useHitscan)
                DoHitscan();
            else if (_useProjectile)
                SpawnProjectile();

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

            // --- PLAYER DAMAGE RELAY : la cible applique les dégâts ---
            if (_relaySequence != _lastRelaySequence)
            {
                _lastRelaySequence = _relaySequence;

                int myID = Networking.LocalPlayer != null ? Networking.LocalPlayer.playerId : -1;
                if (_relayedTargetID == myID)
                {
                    var receiver = Manager != null
                        ? Manager.GetReceiverByPlayerID(myID)
                        : null;
                    if (receiver != null)
                        receiver.ApplyTypedDamage(_relayedDamage, _relayedDamageType, null);
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

            var receiver = hit.collider.GetComponent<M2922_DamageReceiver>();
            if (receiver == null)
                receiver = hit.collider.GetComponentInParent<M2922_DamageReceiver>();

            if (receiver == null) return;

            // Appliquer localement (tireur voit les dégâts)
            receiver.SendDamage(finalDmg, _weapon.DamageTypeAsInt, _localPlayer);

            // Relai réseau pour les cibles JOUEUR (dont on ne peut pas prendre l'ownership)
            VRCPlayerApi targetOwner = Networking.GetOwner(receiver.gameObject);
            if (targetOwner != null && targetOwner != _localPlayer)
            {
                _relayedTargetID = targetOwner.playerId;
                _relayedDamage = finalDmg;
                _relayedDamageType = _weapon.DamageTypeAsInt;
                _relaySequence = _relaySequence + 1;
                RequestSerialization();
            }
        }

        private float ComputeDamage(float distance, float maxRange)
        {
            // Falloff : dégâts complets à courte portée, réduits à longue portée
            float baseDamage = _weapon.Impact * 0.5f;

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

            float speed = 20f + _weapon.Velocity * 0.3f;
            float damage = _weapon.Impact * 2f; // Les lanceurs ont un Impact élevé
            float lifetime = 5f;

            proj.Launch(pos, rot, speed, damage, _weapon.DamageTypeAsInt, lifetime, _localPlayer, this);
        }

        public void ReturnProjectile(M2922_Projectile proj) { }

        // ===================================================
        // HELPERS
        // ===================================================

        private bool CanFire()
        {
            if (_isReloading) return false;
            if (_fireMode == FireMode.SingleShot) return true;
            return _weapon.CurrentAmmo > 0 || _weapon.InfiniteAmmo;
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
            _isSequentialReload = false;
            if (_weapon != null) _weapon.Reload(); // Remplit le chargeur au max
            this.Log("Rechargement termine.");
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
