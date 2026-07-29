using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Contrôleur de tourelle automatique.
    /// Détecte les cibles dans un cône de vision, tourne vers elles, et tire.
    /// Utilise un M2922_WeaponFireHandler pour le tir (hitscan ou projectile).
    /// 
    /// SETUP :
    /// 1. GameObject avec M2922_Weapon + M2922_WeaponFireHandler
    /// 2. Ajouter ce composant
    /// 3. Configurer le scan (range, angle, layer cibles)
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Turret Controller")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_TurretController : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_WeaponFireHandler _fireHandler;
        [SerializeField] private Transform _turretHead; // partie qui tourne (optionnel, sinon transform)

        [Header("=== TARGET DETECTION ===")]
        [SerializeField] private float _scanRange = 50f;
        [SerializeField] private float _scanAngle = 45f; // demi-angle du cône
        [SerializeField] private float _scanInterval = 0.25f; // délai entre scans
        [SerializeField] private LayerMask _targetLayerMask = ~0;

        [Header("=== TRACKING ===")]
        [SerializeField] private float _rotationSpeed = 90f; // degrés/sec
        [SerializeField] private float _aimTolerance = 3f; // degrés avant de considérer "visé"
        [SerializeField] private float _loseTargetDelay = 1.5f; // secondes avant de perdre la cible

        [Header("=== FIRING ===")]
        [SerializeField] private bool _requireAimedToFire = true;
        [SerializeField] private float _acquireDelay = 0.3f; // délai avant de tirer après acquisition

        // --- STATE ---
        private Transform _currentTarget;
        private float _targetLostTimer = 0f;
        private float _acquireTimer = 0f;
        private float _scanTimer = 0f;
        private bool _hasTarget = false;
        private bool _isAimed = false;

        // ===================================================
        // LIFECYCLE
        // ===================================================

        protected override void Start()
        {
            base.Start();

            if (_fireHandler == null)
                _fireHandler = GetComponent<M2922_WeaponFireHandler>();

            if (_turretHead == null)
                _turretHead = transform;

            this.Log("TurretController pret. Range=" + _scanRange.ToString()
                + " Angle=" + _scanAngle.ToString());
        }

        protected override void Update()
        {
            base.Update();

            if (_fireHandler == null) return;

            // Scan périodique
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= _scanInterval)
            {
                _scanTimer = 0f;
                ScanForTargets();
            }

            // Perte de cible
            if (_currentTarget != null)
            {
                if (!IsTargetValid(_currentTarget))
                {
                    _targetLostTimer += Time.deltaTime;
                    if (_targetLostTimer >= _loseTargetDelay)
                    {
                        LoseTarget();
                    }
                }
                else
                {
                    _targetLostTimer = 0f;
                }
            }

            // Poursuite et visée
            if (_hasTarget && _currentTarget != null)
            {
                RotateToward(_currentTarget.position);
                _isAimed = IsAimedAt(_currentTarget.position);

                // Tir
                if (_isAimed || !_requireAimedToFire)
                {
                    _acquireTimer += Time.deltaTime;
                    if (_acquireTimer >= _acquireDelay)
                    {
                        _fireHandler.SetFiring(true);
                    }
                }
                else
                {
                    _fireHandler.SetFiring(false);
                    _acquireTimer = 0f;
                }
            }
            else
            {
                _fireHandler.SetFiring(false);
                _isAimed = false;
            }
        }

        // ===================================================
        // SCAN
        // ===================================================

        private void ScanForTargets()
        {
            Vector3 headPos = _turretHead.position;
            Vector3 headFwd = _turretHead.forward;

            // Si on a déjà une cible valide, on la garde
            if (_hasTarget && _currentTarget != null && IsTargetValid(_currentTarget))
                return;

            // Chercher une nouvelle cible
            Transform bestTarget = null;
            float bestAngle = _scanAngle + 1f;

            Collider[] cols = Physics.OverlapSphere(headPos, _scanRange, _targetLayerMask);
            for (int i = 0; i < cols.Length; i++)
            {
                Transform t = cols[i].transform;
                if (t == _turretHead) continue;

                Vector3 dirToTarget = (t.position - headPos).normalized;
                float angle = Vector3.Angle(headFwd, dirToTarget);

                if (angle <= _scanAngle && angle < bestAngle)
                {
                    bestTarget = t;
                    bestAngle = angle;
                }
            }

            if (bestTarget != null)
                AcquireTarget(bestTarget);
        }

        private bool IsTargetValid(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            Vector3 headPos = _turretHead.position;
            float dist = Vector3.Distance(headPos, target.position);
            if (dist > _scanRange * 1.2f) return false; // marge

            Vector3 dirToTarget = (target.position - headPos).normalized;
            float angle = Vector3.Angle(_turretHead.forward, dirToTarget);
            if (angle > _scanAngle * 1.5f) return false;

            return true;
        }

        private void AcquireTarget(Transform target)
        {
            _currentTarget = target;
            _hasTarget = true;
            _targetLostTimer = 0f;
            _acquireTimer = 0f;
            this.Log("Cible acquise: " + target.name);
        }

        private void LoseTarget()
        {
            _currentTarget = null;
            _hasTarget = false;
            _targetLostTimer = 0f;
            _acquireTimer = 0f;
            _fireHandler.SetFiring(false);
            this.Log("Cible perdue.");
        }

        // ===================================================
        // ROTATION
        // ===================================================

        private void RotateToward(Vector3 targetPos)
        {
            Vector3 headPos = _turretHead.position;
            Vector3 dirToTarget = (targetPos - headPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget, Vector3.up);

            _turretHead.rotation = Quaternion.RotateTowards(
                _turretHead.rotation,
                targetRot,
                _rotationSpeed * Time.deltaTime
            );

            // Propager la rotation au muzzle (via fire handler)
            _fireHandler.SetMuzzleRotation(_turretHead.rotation);
        }

        private bool IsAimedAt(Vector3 targetPos)
        {
            Vector3 dirToTarget = (targetPos - _turretHead.position).normalized;
            float angle = Vector3.Angle(_turretHead.forward, dirToTarget);
            return angle <= _aimTolerance;
        }

        // ===================================================
        // PUBLIC API
        // ===================================================

        /// <summary>
        /// Active/désactive la tourelle.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                LoseTarget();
                _fireHandler.SetFiring(false);
            }
            this.enabled = enabled;
        }

        /// <summary>
        /// Force une cible spécifique.
        /// </summary>
        public void ForceTarget(Transform target)
        {
            AcquireTarget(target);
        }

        public bool HasTarget()
        {
            return _hasTarget;
        }

        // ===================================================
        // EDITOR
        // ===================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_fireHandler == null) _fireHandler = GetComponent<M2922_WeaponFireHandler>();
            if (_turretHead == null) _turretHead = transform;
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (_turretHead == null) return;

            Vector3 headPos = _turretHead.position;
            Vector3 headFwd = _turretHead.forward;

            // Cône de vision
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            DrawGizmoCone(headPos, headFwd, _scanRange, _scanAngle);

            // Cercle de range
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(headPos, _scanRange);

            // Cible actuelle
            if (_hasTarget && _currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(headPos, _currentTarget.position);
                Gizmos.DrawWireSphere(_currentTarget.position, 0.3f);
            }
        }

        private void DrawGizmoCone(Vector3 origin, Vector3 dir, float range, float angle)
        {
            int segments = 20;
            float step = (angle * 2f) / segments;
            Vector3 prevPoint = Vector3.zero;

            for (int i = 0; i <= segments; i++)
            {
                float a = -angle + step * i;
                Quaternion rot = Quaternion.AngleAxis(a, Vector3.up);
                Vector3 point = origin + rot * dir * range;

                if (i > 0)
                    Gizmos.DrawLine(origin, point);
                prevPoint = point;
            }
        }

#endif
    }
}
