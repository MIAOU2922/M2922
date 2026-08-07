using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Projectile tiré par une arme. Gère le déplacement, la durée de vie, et les dégâts.
    /// Utilisé avec un système de pooling : désactivé au lieu de détruit.
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Projectile")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Projectile : M2922_Base
    {
        [Header("=== PROJECTILE STATE ===")]
        private float _speed = 50f;
        private float _directDamage = 10f;
        private float _splashDamage = 5f;
        private float _explosionRadius = 3f;
        private int _damageType = 0;
        private float _lifetime = 3f;
        private float _elapsed = 0f;
        private bool _active = false;
        private Vector3 _velocity = Vector3.zero;
        private VRCPlayerApi _owner;

        [Header("=== TRAJECTOIRE ===")]
        private float _stability = 50f;       // 0-100, haut = vol plus droit
        private float _aimAssistance = 30f;   // 0-100, haut = cône d'aide à la visée plus large
        private float _velocityStat = 50f;    // 0-100, haut = réduit le cône d'aim assist
        private float _gravityScale = 1f;     // défini par Launch() selon le type d'arme
        private int _weaponType = 0;          // WeaponType : RocketLauncher=13 → aim assist, GL=11,12 → pas
        private bool _hasAimAssist = false;
        private Vector3 _initialDirection;

        [Header("=== COLLIDER ===")]
        [Tooltip("Rayon du collider.")]
        [SerializeField] private float _colliderRadius = 0.15f;
        private bool _colliderReady = false;
        private Rigidbody _rigidbody;

        [Header("=== VFX ===")]
        [Tooltip("Prefab d'explosion (particules). Spawné à la destruction du projectile.")]
        [SerializeField] private GameObject _explosionVfxPrefab;
        [Tooltip("Durée de vie du VFX avant destroy (secondes).")]
        [SerializeField] private float _explosionVfxLifetime = 2f;

        [Header("=== POOLING ===")]
        private M2922_WeaponFireHandler _pool;

        [Header("=== NETWORK ===")]
        /// <summary>Quand true, le projectile est visuel uniquement (spawné par un client distant).
        /// Les dégâts ne sont PAS appliqués — seul le VFX et la trajectoire sont joués.</summary>
        private bool _visualOnly = false;

        [Header("=== FILTRAGE ===")]
        [Tooltip("Nom du layer Unity pour les hitboxes.")]
        [SerializeField] private string _hitboxLayerName = "Hitbox";
        [Tooltip("Layer ID (auto-résolu, ne pas modifier).")]
        [SerializeField] public int _hitboxLayer = 8;

        // --- Propriétés ---
        public bool IsActive { get { return _active; } }

        // ===================================================
        // INIT (appelé par le pool)
        // ===================================================

        /// <param name="directDmg">Dégâts d'impact direct (calculés depuis Impact+Velocity+BlastRadius).</param>
        /// <param name="splashDmg">Dégâts de zone (calculés depuis Impact+BlastRadius).</param>
        /// <param name="explRadius">Rayon d'explosion en mètres.</param>
        /// <param name="stability">Stat Stability 0-100 (vol droit).</param>
        /// <param name="aimAssist">Stat AimAssistance 0-100 (cône d'aide à la visée).</param>
        /// <param name="velocityStat">Stat Velocity 0-100 (réduit le cône d'aim assist).</param>
        /// <param name="gravityScale">Multiplicateur de gravité (1=rocket, 2.5=GL).</param>
        public void Launch(Vector3 position, Quaternion rotation, float speed,
            float directDmg, float splashDmg, float explRadius,
            int damageType, float lifetime,
            float stability, float aimAssist, float velocityStat,
            int weaponType, float gravityScale,
            VRCPlayerApi owner, M2922_WeaponFireHandler pool)
        {
            transform.SetPositionAndRotation(position, rotation);
            _speed = speed;
            _directDamage = directDmg;
            _splashDamage = splashDmg;
            _explosionRadius = explRadius;
            _damageType = damageType;
            _lifetime = lifetime;
            _elapsed = 0f;
            _active = true;
            _owner = owner;
            _pool = pool;
            _stability = stability;
            _aimAssistance = aimAssist;
            _velocityStat = velocityStat;
            _gravityScale = gravityScale;
            _weaponType = weaponType;
            // Aim assist seulement pour les lance-roquettes (pas les GL)
            _hasAimAssist = (weaponType == 13 || weaponType == 18); // RocketLauncher=13, RocketSidearm=18
            _velocity = rotation * Vector3.forward * _speed;
            _initialDirection = _velocity.normalized;
            _visualOnly = false;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Lance un projectile visuel uniquement (pour les clients distants).
        /// Aucun dégât n'est appliqué — seule la trajectoire et les VFX sont joués.
        /// </summary>
        public void LaunchVisual(Vector3 position, Quaternion rotation, float speed,
            float explRadius, float lifetime,
            float stability, float aimAssist, float velocityStat,
            int weaponType, float gravityScale,
            M2922_WeaponFireHandler pool)
        {
            transform.SetPositionAndRotation(position, rotation);
            _speed = speed;
            _directDamage = 0f;
            _splashDamage = 0f;
            _explosionRadius = explRadius;
            _damageType = 0;
            _lifetime = lifetime;
            _elapsed = 0f;
            _active = true;
            _owner = null;
            _pool = pool;
            _stability = stability;
            _aimAssistance = aimAssist;
            _velocityStat = velocityStat;
            _gravityScale = gravityScale;
            _weaponType = weaponType;
            _hasAimAssist = (weaponType == 13 || weaponType == 18);
            _velocity = rotation * Vector3.forward * _speed;
            _initialDirection = _velocity.normalized;
            _visualOnly = true;
            gameObject.SetActive(true);
        }

        public void ReturnToPool()
        {
            _active = false;
            _elapsed = 0f;
            _velocity = Vector3.zero;
            if (_rigidbody != null)
                _rigidbody.velocity = Vector3.zero;
            gameObject.SetActive(false);
            if (_pool != null)
                _pool.ReturnProjectile(this);
        }

        protected override void Start()
        {
            base.Start();
            _rigidbody = GetComponent<Rigidbody>();
            var cols = GetComponents<Collider>();
            if (cols.Length == 0)
                this.Error("[Projectile] Aucun collider sur le prefab !");
            if (_rigidbody == null)
                this.Error("[Projectile] Aucun Rigidbody !");
            _colliderReady = cols.Length > 0;
        }

        // ===================================================
        // LIFECYCLE
        // ===================================================

        protected override void Update()
        {
            if (!_active) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                ReturnToPool();
                return;
            }

            // Gravité d'abord (appliquée à la vélocité actuelle)
            _velocity += Physics.gravity * _gravityScale * Time.deltaTime;

            // Corriger la direction (aim assist / stabilité), préserver la vitesse
            if (_velocity.sqrMagnitude > 0.001f)
            {
                Vector3 desiredDir = ComputeTrajectory();
                float currentSpeed = _velocity.magnitude;
                _velocity = desiredDir * currentSpeed;
            }

            // Appliquer la vélocité au Rigidbody (non-kinematic → Unity gère les collisions)
            if (_rigidbody != null)
                _rigidbody.velocity = _velocity;

            if (_velocity.sqrMagnitude > 0.001f)
                transform.forward = _velocity.normalized;
        }

        /// <summary>
        /// Calcule la direction corrigée du projectile.
        /// Stability → tend à garder le projectile droit.
        /// AimAssistance → cône de recherche de hitbox devant le projectile.
        /// Velocity → réduit la taille du cône d'aim assist.
        /// </summary>
        private Vector3 ComputeTrajectory()
        {
            Vector3 currentDir = _velocity.normalized;
            Vector3 targetDir = currentDir;

            // Aim assist : chercher une hitbox dans un cône devant le projectile
            // Uniquement pour les lance-roquettes (RocketLauncher=13, RocketSidearm=18)
            float aimConeAngle = 0f;
            float aimRange = 10f;
            if (_hasAimAssist)
            {
                aimConeAngle = Mathf.Lerp(15f, 3f, _aimAssistance / 100f);
                float velocityNarrow = Mathf.Lerp(1f, 0.3f, _velocityStat / 100f);
                aimConeAngle *= velocityNarrow;
                aimRange = Mathf.Lerp(5f, 20f, _aimAssistance / 100f);
            }

            if (aimConeAngle > 0.5f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, aimRange,
                    1 << _hitboxLayer, QueryTriggerInteraction.Collide);
                Collider best = null;
                float bestAngle = aimConeAngle;
                foreach (var col in hits)
                {
                    Vector3 toTarget = (col.transform.position - transform.position).normalized;
                    float angle = Vector3.Angle(currentDir, toTarget);
                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        best = col;
                    }
                }
                if (best != null)
                {
                    Vector3 toBest = (best.transform.position - transform.position).normalized;
                    targetDir = toBest;
                }
            }

            // Stability : lerp vers la direction cible (stabilité haute = correction lente = vol droit)
            float correctionSpeed = Mathf.Lerp(8f, 2f, _stability / 100f);
            float t = 1f - Mathf.Exp(-correctionSpeed * Time.deltaTime);
            return Vector3.Slerp(currentDir, targetDir, t).normalized;
        }

        // ===================================================
        // COLLISION
        // ===================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!_active) return;

            // Hitbox : appliquer les dégâts directs (sauf si visuel-only)
            if (!_visualOnly && other.gameObject.layer == _hitboxLayer)
            {
                float zoneMult = 1f;
                var dmgMult = other.GetComponent<M2922_DamageMultiplier>();
                if (dmgMult != null)
                    zoneMult = dmgMult.Multiplier;

                var receiver = other.GetComponent<M2922_DamageReceiver>();
                if (receiver == null)
                    receiver = other.GetComponentInParent<M2922_DamageReceiver>();
                if (receiver == null)
                {
                    Transform t = other.transform.parent;
                    while (t != null)
                    {
                        var hitboxSys = t.GetComponentInChildren<M2922_HitboxSystem>();
                        if (hitboxSys != null && hitboxSys.IsMyCollider(other))
                        {
                            receiver = hitboxSys.GetComponent<M2922_DamageReceiver>();
                            break;
                        }
                        t = t.parent;
                    }
                }
                if (receiver != null)
                    receiver.SendDamage(_directDamage * zoneMult, _damageType, _owner);
            }

            // Exploser au contact de n'importe quoi (hitbox, sol, mur...)
            Explode();
        }

        /// <summary>Collision physique (sol, murs, objets non-trigger).</summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!_active) return;
            Explode();
        }

        private void Explode()
        {
            // Dégâts de zone : toutes les hitboxes dans le rayon (sauf si visuel-only)
            if (!_visualOnly && _splashDamage > 0f && _explosionRadius > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, _explosionRadius,
                    1 << _hitboxLayer, QueryTriggerInteraction.Collide);

                // Déduplication : max 100 receivers uniques (pas de List<T> en Udon)
                M2922_DamageReceiver[] damaged = new M2922_DamageReceiver[100];
                int damagedCount = 0;

                foreach (var col in hits)
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    float falloff = 1f - Mathf.Clamp01(dist / _explosionRadius);
                    float dmg = _splashDamage * Mathf.Lerp(0.3f, 1f, falloff);

                    float zoneMult = 1f;
                    var dmgMult = col.GetComponent<M2922_DamageMultiplier>();
                    if (dmgMult != null) zoneMult = dmgMult.Multiplier;

                    var receiver = col.GetComponent<M2922_DamageReceiver>();
                    if (receiver == null) receiver = col.GetComponentInParent<M2922_DamageReceiver>();
                    if (receiver == null)
                    {
                        Transform t = col.transform.parent;
                        while (t != null)
                        {
                            var hs = t.GetComponentInChildren<M2922_HitboxSystem>();
                            if (hs != null && hs.IsMyCollider(col))
                            {
                                receiver = hs.GetComponent<M2922_DamageReceiver>();
                                break;
                            }
                            t = t.parent;
                        }
                    }

                    if (receiver == null) continue;

                    // Vérifier si déjà touché
                    bool alreadyHit = false;
                    for (int i = 0; i < damagedCount; i++)
                    {
                        if (damaged[i] == receiver) { alreadyHit = true; break; }
                    }
                    if (alreadyHit) continue;

                    if (damagedCount < damaged.Length)
                    {
                        damaged[damagedCount] = receiver;
                        damagedCount++;
                    }
                    receiver.SendDamage(dmg * zoneMult, _damageType, _owner);
                }
            }

            // VFX d'explosion
            if (_explosionVfxPrefab != null)
            {
                GameObject vfx = Instantiate(_explosionVfxPrefab);
                vfx.transform.position = transform.position;
                vfx.transform.rotation = Quaternion.identity;
                Destroy(vfx, _explosionVfxLifetime);
            }

            ReturnToPool();
        }

        // ===================================================
        // EDITOR
        // ===================================================

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Résoudre le layer hitbox depuis le nom
            _hitboxLayer = UnityEngine.LayerMask.NameToLayer(_hitboxLayerName);
            if (_hitboxLayer < 0) _hitboxLayer = 8;

            // S'assurer que les deux colliders existent (trigger + non-trigger)
            var cols = GetComponents<Collider>();
            bool hasTrigger = false;
            bool hasNonTrigger = false;
            foreach (var c in cols)
            {
                if (c.isTrigger) hasTrigger = true;
                else hasNonTrigger = true;
                var sphere = c as UnityEngine.SphereCollider;
                if (sphere != null) sphere.radius = _colliderRadius;
                var capsule = c as UnityEngine.CapsuleCollider;
                if (capsule != null) { capsule.radius = _colliderRadius; capsule.height = _colliderRadius * 3f; }
            }
            if (!hasTrigger)
            {
                var tc = gameObject.AddComponent<UnityEngine.SphereCollider>();
                tc.isTrigger = true;
                tc.radius = _colliderRadius * 1.2f;
                tc.center = Vector3.zero;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            if (!hasNonTrigger)
            {
                var sc = gameObject.AddComponent<UnityEngine.SphereCollider>();
                sc.isTrigger = false;
                sc.radius = _colliderRadius;
                sc.center = Vector3.zero;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            // Rigidbody non-kinematic (nécessaire pour OnCollisionEnter, piloté via .velocity)
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = false;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            DrawExplosionGizmo(1f);
            DrawTrajectoryGizmo(0.8f);
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            DrawExplosionGizmo(0.2f);
        }

        private void DrawExplosionGizmo(float alpha)
        {
            // Rayon d'explosion
            float radius = _explosionRadius > 0f ? _explosionRadius : 3f;
            UnityEditor.Handles.color = new Color(1f, 0.3f, 0f, alpha * 0.5f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radius);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.right, radius);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, radius);

            // Label
            if (alpha > 0.5f)
            {
                UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, alpha);
                UnityEditor.Handles.Label(transform.position + Vector3.up * (radius + 0.2f),
                    $"Explosion: {radius:F1}m  Direct:{_directDamage:F0}  Splash:{_splashDamage:F0}");
            }
        }

        private void DrawTrajectoryGizmo(float alpha)
        {
            if (!Application.isPlaying || !_active) return;

            Vector3 pos = transform.position;
            Vector3 dir = _velocity.normalized;

            // Ligne de trajectoire
            float trajLen = _speed * 1f;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, alpha);
            Gizmos.DrawLine(pos, pos + dir * trajLen);
            Gizmos.DrawWireSphere(pos + dir * trajLen, 0.15f);

            // Cône d'aim assist (rocket seulement)
            if (_hasAimAssist)
            {
                float aimConeAngle = Mathf.Lerp(15f, 3f, _aimAssistance / 100f);
                float velNarrow = Mathf.Lerp(1f, 0.3f, _velocityStat / 100f);
                aimConeAngle *= velNarrow;
                float aimRange = Mathf.Lerp(5f, 20f, _aimAssistance / 100f);

                if (aimConeAngle > 0.5f)
                {
                    Gizmos.color = new Color(0f, 1f, 0.5f, alpha * 0.4f);
                    int segs = 16;
                    Vector3 prev = Vector3.zero;
                    for (int i = 0; i <= segs; i++)
                    {
                        float a = (float)i / segs * Mathf.PI * 2f;
                        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(
                            Mathf.Sin(a) * aimConeAngle,
                            Mathf.Cos(a) * aimConeAngle, 0f);
                        Vector3 pt = pos + rot * Vector3.forward * aimRange;
                        if (i > 0) Gizmos.DrawLine(prev, pt);
                        prev = pt;
                    }
                    Gizmos.DrawLine(pos, pos + dir * aimRange);
                }
            }

            // Stats label
            if (alpha > 0.5f)
            {
                string label = _hasAimAssist
                    ? $"ROCKET | Spd:{_speed:F0} Stab:{_stability:F0} AA:{_aimAssistance:F0} Vel:{_velocityStat:F0}"
                    : $"GL | Spd:{_speed:F0} Stab:{_stability:F0}";
                UnityEditor.Handles.color = new Color(0.5f, 0.9f, 1f, alpha);
                Vector3 labelPos = pos + Vector3.up * 0.4f;
                UnityEditor.Handles.Label(labelPos, label);
            }
        }
#endif
    }
}
