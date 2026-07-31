using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Weapon
{
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
        [SerializeField] private float _beamRange = 50f;

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

        // --- OWNERSHIP ---
        private VRCPlayerApi _localPlayer;
        private bool _isHeld = false;
        private WeaponType _weaponType;
        private FireMode _fireMode;
        private bool _useHitscan;
        private bool _useProjectile;

        // ===================================================
        // LIFECYCLE
        // ===================================================

        protected override void Start()
        {
            base.Start();

            _localPlayer = Networking.LocalPlayer;

            if (_weapon == null)
                _weapon = GetComponent<M2922_Weapon>();

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
                _beamRenderer.enabled = false;

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
        // VRChat PICKUP CALLBACKS
        // ===================================================

        public override void OnPickup()
        {
            _isHeld = true;
            _localPlayer = Networking.LocalPlayer;
        }

        public override void OnDrop()
        {
            _isHeld = false;
            _triggerHeld = false;
            _isCharging = false;
            if (_beamRenderer != null) _beamRenderer.enabled = false;
            _beamActive = false;
        }

        public override void OnPickupUseDown()
        {
            _triggerHeld = true;
            _triggerJustPressed = true;
        }

        public override void OnPickupUseUp()
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

            // Bloom : se résorbe quand on ne tire pas
            if (!_triggerHeld && _bloomAccum > 0f)
            {
                _bloomAccum -= _bloomDecay * Time.deltaTime;
                if (_bloomAccum < 0f) _bloomAccum = 0f;
                UpdateSpreadMult();
            }

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
                if (_beamRenderer != null)
                {
                    _beamRenderer.enabled = true;
                    Vector3 origin = GetMuzzlePos();
                    Vector3 dir = GetMuzzleDir();

                    _beamRenderer.SetPosition(0, origin);
                    RaycastHit hit;
                    if (Physics.Raycast(origin, dir, out hit, _beamRange, _hitscanLayerMask))
                    {
                        _beamRenderer.SetPosition(1, hit.point);
                        float dps = _weapon.Impact * Time.deltaTime * 10f;
                        ApplyHitDamage(hit, dps);
                    }
                    else
                    {
                        _beamRenderer.SetPosition(1, origin + dir * _beamRange);
                    }
                }
            }
            else
            {
                _beamActive = false;
                if (_beamRenderer != null) _beamRenderer.enabled = false;
            }
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
                        receiver.ApplyTypedDamage(_weapon.Impact * 2f, _weapon.DamageTypeAsInt, _localPlayer);
                }
            }
        }

        // ===================================================
        // FIRE EXECUTION
        // ===================================================

        private void DoFire()
        {
            if (_weapon == null) return;

            _weapon.Fire(); // consomme munition
            RequestSerialization();

            // Muzzle flash VFX (toujours, quel que soit le mode)
            SpawnMuzzleFlash();

            if (_useHitscan)
                DoHitscan();
            else if (_useProjectile)
                SpawnProjectile();
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
                if (Physics.Raycast(origin, dir, out hit, range, _hitscanLayerMask))
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
            // Récupérer le multiplicateur de zone depuis M2922_DamageMultiplier
            float zoneMult = 1f;
            var dmgMult = hit.collider.GetComponent<M2922_DamageMultiplier>();
            if (dmgMult != null)
                zoneMult = dmgMult.Multiplier;

            float finalDmg = damage * zoneMult;

            // Chercher le DamageReceiver sur la cible
            var receiver = hit.collider.GetComponent<M2922_DamageReceiver>();
            if (receiver == null)
                receiver = hit.collider.GetComponentInParent<M2922_DamageReceiver>();

            if (receiver != null)
                receiver.ApplyTypedDamage(finalDmg, _weapon.DamageTypeAsInt, _localPlayer);
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
            // Le Range stat et le Zoom étendent la portée (coefficients Open World)
            return baseRange + (_weapon.Range * 0.8f) + (_weapon.Zoom * 1.2f);
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
            if (_fireMode == FireMode.SingleShot) return true;
            return _weapon.CurrentAmmo > 0;
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

        public void ForceReload()
        {
            if (_weapon != null) _weapon.Reload();
        }

        public bool IsFiring()
        {
            return _triggerHeld || _burstRemaining > 0 || _isCharging || _beamActive;
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

            // Auto-set le layerMask pour ne cibler que ce layer
            if (_hitboxLayer >= 0)
                _hitscanLayerMask = (1 << _hitboxLayer);

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
