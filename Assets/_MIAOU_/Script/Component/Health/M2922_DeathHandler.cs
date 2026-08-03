using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Gère la mort : respawn, despawn, events OnDeath/OnRevive.
    /// </summary>
    [AddComponentMenu("M2922/Health/Death Handler")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DeathHandler : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_HealthComponent _health;
        [SerializeField] private M2922_ShieldComponent _shield;

        [Header("=== RESPAWN ===")]
        [SerializeField] private bool _autoRespawn = true;
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Transform _respawnPoint;

        private bool _isDead = false;
        private float _deathTime = 0f;

        public bool IsDead => _isDead;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_health == null) _health = GetComponent<M2922_HealthComponent>();
            if (_shield == null) _shield = GetComponent<M2922_ShieldComponent>();
        }

        protected override void Update()
        {
            base.Update();

            if (_health != null && _health.IsDead && !_isDead)
            {
                Die();
            }

            if (_isDead && _autoRespawn && Time.time - _deathTime > _respawnDelay)
            {
                Respawn();
            }
        }

        private void Die()
        {
            _isDead = true;
            _deathTime = Time.time;
            this.Log("Entity died.");
            // TODO: trigger OnDeath event, ragdoll, disable colliders...
        }

        private void Respawn()
        {
            _isDead = false;

            // Restaurer vie et shield
            if (_health != null) _health.Revive();
            if (_shield != null) _shield.Revive();

            // Drop tous les VRC Pickup tenus
            DropAllPickups();

            // TP au point de respawn
            if (_respawnPoint != null)
                transform.SetPositionAndRotation(_respawnPoint.position, _respawnPoint.rotation);

            this.Log("Entity respawned.");
        }

        /// <summary>Drop tous les VRC_Pickup actuellement tenus par ce GameObject/enfants.</summary>
        private void DropAllPickups()
        {
            VRC_Pickup[] pickups = GetComponentsInChildren<VRC_Pickup>();
            if (pickups != null)
            {
                for (int i = 0; i < pickups.Length; i++)
                {
                    if (pickups[i] != null && pickups[i].IsHeld)
                        pickups[i].Drop();
                }
            }
        }

        public void ForceRespawn()
        {
            if (_isDead) Respawn();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Dead", _isDead ? "YES" : "NO", _isDead ? Color.red : Color.green),
                new M2922_GizmoDisplayInfo("Auto Respawn", _autoRespawn ? "ON" : "OFF"),
                new M2922_GizmoDisplayInfo("Respawn In", $"{_respawnDelay}s"),
            };
        }
#endif
    }
}
