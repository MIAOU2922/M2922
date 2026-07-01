using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Gère la mort : respawn, despawn, events OnDeath/OnRevive.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DeathHandler : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_HealthComponent _health;

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
            if (_health != null) _health.Revive();
            if (_respawnPoint != null)
                transform.SetPositionAndRotation(_respawnPoint.position, _respawnPoint.rotation);
            this.Log("Entity respawned.");
            // TODO: trigger OnRevive event
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
