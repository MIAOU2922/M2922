using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Entity;
using EventType = M2922.Core.EventType;

namespace M2922.Combat
{
    /// <summary>
    /// Projectile physique — lancé par M2922_WeaponEntity ou utilisé en standalone.
    ///
    /// ─── DÉGÂTS MULTI-TYPES ───────────────────────────────────────────────────
    ///   Le projectile peut combiner plusieurs types de dégâts simultanément,
    ///   identiquement à M2922_WeaponEntity.
    ///   - Inspector (_damageTypes + _baseDamageAmounts) : config standalone.
    ///   - Initialize() override les tableaux avec les valeurs de l'arme qui tire.
    ///   - SetDamageMultiplier() applique un multiplicateur en vol (buff/debuff).
    ///   - Un OnDamageDealt est publié par type à chaque impact.
    ///
    /// ─── IMPACT DIRECT vs EXPLOSION ──────────────────────────────────────────
    ///   _explosionRadius = 0 → dégâts ponctuels sur le premier collider touché.
    ///   _explosionRadius > 0 → Physics.OverlapSphere, falloff linéaire du centre
    ///                          au bord (100 % au centre, 0 % au bord).
    ///
    /// ─── PATTERN POOL ─────────────────────────────────────────────────────────
    ///   Les GOs sont désactivés par défaut.
    ///   WeaponEntity appelle Initialize() sur le premier projectile dont IsActive = false.
    ///   Le projectile se re-désactive après _lifetime secondes ou à l'impact.
    ///
    /// ─── SETUP SCÈNE ──────────────────────────────────────────────────────────
    ///   GO : M2922_Projectile + Rigidbody + Collider (Is Trigger optionnel)
    ///   Désactiver le GO dans la scène (état pool = inactive).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Projectile : M2922_Entity
    {
        // =====================================================================
        // DÉGÂTS MULTI-TYPES (config inspector — standalone ou standalone-override)
        // =====================================================================
        [Header("=== DAMAGE CONFIG ===")]
        [Tooltip("Types de dégâts appliqués à l'impact (parallèle avec _baseDamageAmounts).\n" +
                 "Sert en mode standalone. Écrasé par Initialize() si lancé par une arme.")]
        [SerializeField] private DamageType[] _damageTypes = { DamageType.Bullet };

        [Tooltip("Montant de dégâts de base par type (même index que _damageTypes).\n" +
                 "Multiplié par _damageMultiplier à l'impact.")]
        [SerializeField] private float[] _baseDamageAmounts = { 30f };

        [Tooltip("Multiplicateur de dégâts (modifiable en vol via SetDamageMultiplier)")]
        [SerializeField] private float _damageMultiplier = 1f;

        [Tooltip("Multiplicateur de dégâts si le collider touché a le tag 'Crit' (zone critique).")]
        [SerializeField] private float _critMultiplier = 2f;

        // =====================================================================
        // PROJECTILE SETTINGS
        // =====================================================================
        [Header("=== PROJECTILE SETTINGS ===")]
        [Tooltip("Durée de vie maximale avant désactivation automatique (secondes)")]
        [SerializeField] private float _lifetime = 5f;

        [Tooltip("Rayon d'explosion en mètres (0 = dégât ponctuel uniquement)")]
        [SerializeField] private float _explosionRadius = 0f;

        [Tooltip("Layer mask des cibles touchables")]
        [SerializeField] private LayerMask _hitLayers = -1;

        // =====================================================================
        // VFX
        // =====================================================================
        [Header("=== VFX ===")]
        [SerializeField] private ParticleSystem _trailEffect;
        [SerializeField] private ParticleSystem _impactEffect;
        [SerializeField] private AudioSource    _impactAudio;

        // =====================================================================
        // RUNTIME (injectés par Initialize ou calculés à partir des bases)
        // =====================================================================
        // Tableaux actifs utilisés à l'impact (copie des bases ou override de l'arme)
        private DamageType[] _activeDamageTypes;
        private float[]      _activeBaseDamageAmounts;   // base avant _damageMultiplier runtime
        private float        _activeDamageMultiplier;    // = _damageMultiplier * mult runtime
        private float        _activeCritMultiplier;

        private int          _attackerId;
        private float        _spawnTime;
        private bool         _hasExploded;

        private Rigidbody _rigidbody;

        // =====================================================================
        // ÉTAT POOL
        // =====================================================================
        // IsActive hérite de M2922_Entity → gameObject.activeInHierarchy

        // =====================================================================
        // LIFECYCLE
        // =====================================================================
        protected override void Awake()
        {
            _entityType = EntityType.Projectile;
            _rigidbody  = GetComponent<Rigidbody>();

            if (_rigidbody == null)
                M2922_Debug.Error("[Projectile] Rigidbody manquant ! Ajouter un Rigidbody sur ce GO.", this);

            // Copier les tableaux inspector comme valeurs par défaut pour le mode standalone
            CopyInspectorArrays();

            // Désactiver pour le pool
            gameObject.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
        }

        protected override void Update()
        {
            base.Update();
            if (_hasExploded) return;
            if (Time.time - _spawnTime >= _lifetime)
            {
                this.VerboseLog("[Projectile] Lifetime expirée — désactivation");
                Deactivate();
            }
        }

        // =====================================================================
        // BUFF / DEBUFF API
        // =====================================================================

        /// <summary>
        /// Multiplicateur de dégâts appliqué en vol (buff/debuff externe).
        /// Combiné avec le multiplicateur injecté par l'arme.
        /// </summary>
        public void SetDamageMultiplier(float multiplier)
        {
            _activeDamageMultiplier = Mathf.Max(0f, multiplier);
            this.VerboseLog($"[Projectile] DamageMultiplier → {_activeDamageMultiplier}");
        }

        // =====================================================================
        // POOL API
        // =====================================================================

        /// <summary>
        /// Active le projectile avec la config de dégâts de l'arme qui tire.
        /// Les tableaux de l'arme sont copiés pour éviter tout écrasement concurrent.
        /// </summary>
        /// <param name="position">Bouche du canon</param>
        /// <param name="direction">Direction normalisée</param>
        /// <param name="speed">Vitesse initiale (m/s)</param>
        /// <param name="attackerId">PlayerId du tireur</param>
        /// <param name="weaponDamageTypes">Types de dégâts de l'arme</param>
        /// <param name="weaponEffectiveDamageAmounts">Montants effectifs de l'arme (base * buff arme)</param>
        /// <param name="headshotMultiplier">Multiplicateur headshot de l'arme</param>
        public void Initialize(
            Vector3      position,
            Vector3      direction,
            float        speed,
            int          attackerId,
            DamageType[] weaponDamageTypes,
            float[]      weaponEffectiveDamageAmounts,
            float        critMultiplier)
        {
            _attackerId             = attackerId;
            _activeDamageMultiplier = 1f;          // reset mult runtime
            _activeCritMultiplier   = critMultiplier;

            // Copier les arrays de l'arme (UdonSharp passe par référence, on isole)
            if (weaponDamageTypes != null && weaponEffectiveDamageAmounts != null)
            {
                int count                 = Mathf.Min(weaponDamageTypes.Length, weaponEffectiveDamageAmounts.Length);
                _activeDamageTypes        = new DamageType[count];
                _activeBaseDamageAmounts  = new float[count];
                for (int i = 0; i < count; i++)
                {
                    _activeDamageTypes[i]       = weaponDamageTypes[i];
                    _activeBaseDamageAmounts[i]  = weaponEffectiveDamageAmounts[i];
                }
            }
            else
            {
                CopyInspectorArrays();  // fallback sur la config inspector
            }

            Activate(position, direction, speed);
        }

        /// <summary>
        /// Active le projectile en mode standalone : utilise la config inspector du projectile.
        /// </summary>
        public void InitializeStandalone(Vector3 position, Vector3 direction, float speed, int attackerId)
        {
            _attackerId             = attackerId;
            _activeDamageMultiplier = _damageMultiplier;
            _activeCritMultiplier   = _critMultiplier;
            CopyInspectorArrays();
            Activate(position, direction, speed);
        }

        private void Activate(Vector3 position, Vector3 direction, float speed)
        {
            _hasExploded       = false;
            _spawnTime         = Time.time;

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction.normalized);
            gameObject.SetActive(true);

            if (_rigidbody != null)
            {
                _rigidbody.velocity        = direction.normalized * speed;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (_trailEffect != null) _trailEffect.Play();

            this.VerboseLog($"[Projectile] Activé | Spd:{speed} | Att:{_attackerId} | Types:{(_activeDamageTypes != null ? _activeDamageTypes.Length : 0)}");
        }

        private void Deactivate()
        {
            _hasExploded = true;

            if (_rigidbody != null)
            {
                _rigidbody.velocity        = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (_trailEffect != null) _trailEffect.Stop();

            gameObject.SetActive(false);
        }

        private void CopyInspectorArrays()
        {
            int count = (_damageTypes != null && _baseDamageAmounts != null)
                ? Mathf.Min(_damageTypes.Length, _baseDamageAmounts.Length)
                : 0;

            _activeDamageTypes       = new DamageType[count];
            _activeBaseDamageAmounts = new float[count];

            for (int i = 0; i < count; i++)
            {
                _activeDamageTypes[i]       = _damageTypes[i];
                _activeBaseDamageAmounts[i] = _baseDamageAmounts[i];
            }

            _activeDamageMultiplier = _damageMultiplier;
            _activeCritMultiplier   = _critMultiplier;
        }

        // =====================================================================
        // COLLISION
        // =====================================================================

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasExploded) return;

            Vector3 contactPoint  = collision.GetContact(0).point;
            Vector3 contactNormal = collision.GetContact(0).normal;

            PlayImpactEffects(contactPoint);

            if (_explosionRadius > 0f)
                ExplodeAtPosition(contactPoint);
            else
                ApplyDirectHit(collision.collider, contactPoint, contactNormal);

            Deactivate();
        }

        // =====================================================================
        // DAMAGE INTERNALS
        // =====================================================================

        private void ApplyDirectHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
        {
            bool isCrit = hitCollider.GetComponent<M2922_CritZone>() != null;

            // Fonctionne pour tout type d'entité — victimId = -1 si le GO n'a pas de M2922_Entity (sac de sable…)
            M2922_Entity target = hitCollider.GetComponentInParent<M2922_Entity>();
            int victimId = target != null ? target.EntityId : -1;

            float distance = Vector3.Distance(transform.position, hitPoint);
            PublishAllDamageEvents(victimId, isCrit, 1f, hitPoint, hitNormal, distance);

            this.VerboseLog($"[Projectile] Impact sur {(target != null ? target.EntityName : hitCollider.name)} | Crit: {isCrit}");
        }

        /// <summary>
        /// Dégâts de zone : falloff linéaire du centre (1.0) au bord du rayon (0.0).
        /// Headshot non applicable en explosion.
        /// </summary>
        private void ExplodeAtPosition(Vector3 center)
        {
            Collider[] hitColliders = Physics.OverlapSphere(center, _explosionRadius, _hitLayers);

            this.VerboseLog($"[Projectile] Explosion @ {center} | R:{_explosionRadius}m | Cibles:{hitColliders.Length}");

            for (int i = 0; i < hitColliders.Length; i++)
            {
                float dist    = Vector3.Distance(center, hitColliders[i].transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / _explosionRadius);
                if (falloff <= 0f) continue;

                // Fonctionne pour tout type d'entité — victimId = -1 si aucune M2922_Entity
                M2922_Entity target = hitColliders[i].GetComponentInParent<M2922_Entity>();
                int victimId = target != null ? target.EntityId : -1;

                Vector3 dir = (hitColliders[i].transform.position - center).normalized;
                PublishAllDamageEvents(victimId, false, falloff, center, dir, dist);

                this.VerboseLog($"[Projectile] Explosion hit {(target != null ? target.EntityName : hitColliders[i].name)} | Falloff:{falloff:F2} | Dist:{dist:F1}m");
            }
        }

        /// <summary>
        /// Publie un OnDamageDealt par type configuré.
        /// <param name="damageScale">Multiplicateur appliqué en plus (ex: falloff d'explosion).</param>
        /// </summary>
        private void PublishAllDamageEvents(
            int victimId, bool isHeadshot, float damageScale,
            Vector3 hitPoint, Vector3 hitNormal, float distance)
        {
            if (_activeDamageTypes == null || _activeBaseDamageAmounts == null) return;

            int count = Mathf.Min(_activeDamageTypes.Length, _activeBaseDamageAmounts.Length);
            float headshotMult = isHeadshot ? _activeCritMultiplier : 1f;

            for (int i = 0; i < count; i++)
            {
                float finalDmg = _activeBaseDamageAmounts[i]
                               * _activeDamageMultiplier
                               * damageScale
                               * headshotMult;

                if (finalDmg <= 0f) continue;
                PublishDamageEvent(victimId, finalDmg, _activeDamageTypes[i], isHeadshot, hitPoint, hitNormal, distance);
            }
        }

        private void PublishDamageEvent(
            int victimId, float damage, DamageType damageType, bool isHeadshot,
            Vector3 hitPoint, Vector3 hitNormal, float distance)
        {
            if (Manager == null || Manager.EventBus == null) return;

            M2922_EventData slot = Manager.EventBus.RentSlot();
            if (slot == null) return;

            slot.AttackerId  = _attackerId;
            slot.VictimId    = victimId;
            slot.Damage      = damage;
            slot.DamageType  = (int)damageType;
            slot.IsHeadshot  = isHeadshot;
            slot.Position    = hitPoint;
            slot.HitNormal   = hitNormal;
            slot.Distance    = distance;

            Manager.EventBus.PublishNetwork(EventType.OnDamageDealt, slot);
        }

        // =====================================================================
        // VFX
        // =====================================================================

        private void PlayImpactEffects(Vector3 position)
        {
            if (_impactEffect != null)
            {
                _impactEffect.transform.position = position;
                _impactEffect.Play();
            }
            if (_impactAudio != null && _impactAudio.clip != null)
                _impactAudio.PlayOneShot(_impactAudio.clip);
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _entityType      = EntityType.Projectile;
            _lifetime        = Mathf.Max(0.1f, _lifetime);
            _explosionRadius = Mathf.Max(0f,   _explosionRadius);
            _damageMultiplier = Mathf.Max(0f,  _damageMultiplier);

            if (_damageTypes != null && _baseDamageAmounts != null
                && _damageTypes.Length != _baseDamageAmounts.Length)
            {
                UnityEngine.Debug.LogWarning(
                    "[M2922_Projectile] _damageTypes et _baseDamageAmounts doivent avoir la même longueur !");
            }
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo || _explosionRadius <= 0f) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;

            if (_explosionRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                Gizmos.DrawWireSphere(transform.position, _explosionRadius);
            }

            string dmgSummary = "";
            if (_damageTypes != null && _baseDamageAmounts != null)
            {
                int count = Mathf.Min(_damageTypes.Length, _baseDamageAmounts.Length);
                for (int i = 0; i < count; i++)
                    dmgSummary += $"{_damageTypes[i]}:{_baseDamageAmounts[i]} ";
            }

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 5f),
                $"[Projectile] Dégâts: {dmgSummary}\n" +
                $"Mult: x{_damageMultiplier} | Crit: x{_critMultiplier}\n" +
                $"Rayon explosion: {_explosionRadius}m | Durée vie: {_lifetime}s"
            );
        }
#endif
    }
}
