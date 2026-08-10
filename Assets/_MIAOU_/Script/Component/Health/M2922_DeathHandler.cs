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
        [Tooltip("Cocher si ce DeathHandler est sur un joueur (utilise VRCPlayerApi.TeleportTo + Immobilize).\n" +
                 "Décocher pour un NPC (utilise transform.SetPositionAndRotation).")]
        [SerializeField] private bool _isPlayer = false;
        [SerializeField] private bool _autoRespawn = true;
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Transform _respawnPoint;

        private bool _isDead = false;
        private float _deathTime = 0f;
        private float _respawnedTime = -999f; // période de grâce anti-re-death
        private bool _healthRegenWasActive;   // mémorise l'état avant mort
        private bool _shieldRegenWasActive;

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

            // Seul le propriétaire de l'entité traite sa mort/respawn.
            if (!Networking.IsOwner(gameObject)) return;

            // Période de grâce après respawn : évite que l'entité remeure
            // immédiatement si _health.IsDead est encore true 1 frame après Revive().
            if (_health != null && _health.IsDead && !_isDead
                && Time.time - _respawnedTime > 0.5f)
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

            VRCPlayerApi localPlayer = Networking.LocalPlayer;

            // ── 0. STOP REGEN (mémoriser l'état avant) ─────────────────
            _healthRegenWasActive = _health != null && _health.IsRegenActive;
            _shieldRegenWasActive = _shield != null && _shield.IsRegenActive;
            if (_health != null) _health.SetRegenEnabled(false);
            if (_shield != null) _shield.SetRegenEnabled(false);

            // ── 1. FREEZE ──────────────────────────────────────────────
            if (_isPlayer && localPlayer != null && localPlayer.IsValid())
                localPlayer.Immobilize(true);

            // ── 2. DROP PICKUPS (joueur seulement) ─────────────────────
            if (_isPlayer)
                DropAllPickups();

            // ── 3. TP AU RESPAWN ───────────────────────────────────────
            if (_respawnPoint != null)
            {
                if (_isPlayer && localPlayer != null && localPlayer.IsValid())
                    localPlayer.TeleportTo(_respawnPoint.position, _respawnPoint.rotation);
                else
                    transform.SetPositionAndRotation(_respawnPoint.position, _respawnPoint.rotation);
            }

            this.Log(_isPlayer ? "Player died." : "Entity died.");
        }

        private void Respawn()
        {
            _isDead = false;
            _respawnedTime = Time.time;

            // ── 4. REGEN VIE + SHIELD (restaurer si actif avant mort) ──
            if (_health != null) { _health.Revive(); if (_healthRegenWasActive) _health.SetRegenEnabled(true); }
            if (_shield != null) { _shield.Revive(); if (_shieldRegenWasActive) _shield.SetRegenEnabled(true); }

            // ── 5. UNFREEZE ────────────────────────────────────────────
            if (_isPlayer)
            {
                VRCPlayerApi localPlayer = Networking.LocalPlayer;
                if (localPlayer != null && localPlayer.IsValid())
                    localPlayer.Immobilize(false);
            }

            this.Log(_isPlayer ? "Player respawned." : "Entity respawned.");
        }

        /// <summary>Drop les VRC_Pickup tenus par le joueur local (mains gauche+droite).</summary>
        private void DropAllPickups()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local == null || !local.IsValid()) return;

            VRC_Pickup left  = local.GetPickupInHand(VRC_Pickup.PickupHand.Left);
            VRC_Pickup right = local.GetPickupInHand(VRC_Pickup.PickupHand.Right);
            if (left  != null) left.Drop();
            if (right != null) right.Drop();
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
                new M2922_GizmoDisplayInfo("Target", _isPlayer ? "PLAYER" : "NPC", _isPlayer ? Color.cyan : Color.yellow),
                new M2922_GizmoDisplayInfo("Dead", _isDead ? "YES" : "NO", _isDead ? Color.red : Color.green),
                new M2922_GizmoDisplayInfo("Auto Respawn", _autoRespawn ? "ON" : "OFF"),
                new M2922_GizmoDisplayInfo("Respawn In", $"{_respawnDelay}s"),
            };
        }
#endif
    }
}
