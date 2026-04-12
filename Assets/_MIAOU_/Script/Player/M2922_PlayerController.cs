using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using CoreEventType = M2922.Core.EventType;

namespace M2922.Player
{
    /// <summary>
    /// Contrôleur joueur – hérite de M2922_Entity.
    /// Ajoute uniquement ce qui est spécifique au joueur :
    ///   - Friendly Fire check (via TeamManager)
    ///   - Attribution / changement d'équipe
    ///   - Stats (kills / deaths / score)
    ///   - Téléportation au respawn
    ///   - Abonnement aux événements PvP
    ///
    /// PIPELINE TakeDamage (override) :
    ///   ①  TeamManager FF check
    ///   → base.TakeDamage() : Buff(DamageIn) → Armor → HealthController
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [RequireComponent(typeof(M2922.Combat.M2922_HealthController))]
    public class M2922_PlayerController : M2922_Entity
    {
        // === TEAM ===
        [UdonSynced] private int _currentTeam = -1;
        public int TeamIndex => _currentTeam;

        [Header("=== PLAYER INFO ===")]
        [SerializeField] private VRCPlayerApi _player;
        private int _playerId = -1;

        [Header("=== PLAYER MODULES ===")]
        [SerializeField] private M2922.Teams.M2922_TeamManager _teamManager;
        [SerializeField] private M2922.Spawning.M2922_SpawnManager _spawnManager;

        // === STATS ===
        private int _kills  = 0;
        private int _deaths = 0;
        private int _score  = 0;

        // === LIFECYCLE ===

        protected override void Start()
        {
            base.Start(); // Entity.Start() : auto-détecte HealthController / ArmorSystem / BuffSystem

            if (_teamManager == null)  TryFindTeamManager();
            if (_spawnManager == null) TryFindSpawnManager();

            // Événements
            if (Manager != null && Manager.EventBus != null)
            {
                Manager.EventBus.Subscribe(CoreEventType.OnPlayerKilled, this);
                Manager.EventBus.Subscribe(CoreEventType.OnPlayerDied,   this);
                Manager.EventBus.Subscribe(CoreEventType.OnTeamChanged,  this);
                Manager.EventBus.Subscribe(CoreEventType.OnGameStarted,  this);
            }

            // Setup joueur local
            if (_player != null && _player.isLocal)
            {
                _playerId = _player.playerId;

                if (_healthController != null)
                    _healthController.SetEntityName(_player.displayName);

                if (_teamManager != null)
                {
                    int teamIndex = _teamManager.AssignPlayerAutoBalance(_playerId);
                    SetTeam(teamIndex);
                    this.Log($"Assigned to team {teamIndex}");
                }
                else
                {
                    this.Log("No TeamManager â€” FFA mode");
                }
            }

            this.Log($"PlayerController ready. PlayerId={_playerId} Team={TeamIndex}");
        }

        private void OnDestroy()
        {
            if (Manager != null && Manager.EventBus != null)
            {
                Manager.EventBus.Unsubscribe(CoreEventType.OnPlayerKilled, this);
                Manager.EventBus.Unsubscribe(CoreEventType.OnPlayerDied,   this);
                Manager.EventBus.Unsubscribe(CoreEventType.OnTeamChanged,  this);
                Manager.EventBus.Unsubscribe(CoreEventType.OnGameStarted,  this);
            }
        }

        // === PIPELINE DÉGÂTS (override) – ajoute le FF check ===

        public override void TakeDamage(float damage, int attackerId, DamageType damageType)
        {
            float ffMult = GetFFMultiplier(attackerId);
            if (ffMult == 0f) return;
            base.TakeDamage(damage * ffMult, attackerId, damageType);
        }

        private float GetFFMultiplier(int attackerId)
        {
            if (_teamManager == null) return 1f;
            float mult = _teamManager.GetDamageMultiplier(attackerId, _playerId);
            if (mult < 1f && mult > 0f)
                this.VerboseLog($"Friendly fire ×{mult} from {attackerId}");
            return mult;
        }

        // === RESPAWN ===

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (_player != null && _player.isLocal)
                _player.TeleportTo(position, rotation);

            base.ResetModules(); // HP max + Armor full + Buffs clear
            this.Log($"Respawned at {position}");
        }

        public void RespawnAtTeamSpawn()
        {
            if (_spawnManager != null)
            {
                Transform spawnPoint = _spawnManager.GetRandomTeamSpawn(TeamIndex);
                if (spawnPoint != null)
                {
                    Respawn(spawnPoint.position, spawnPoint.rotation);
                    return;
                }
            }
            base.ResetModules();
        }

        // === TEAM ===

        public void SetTeam(int teamIndex)
        {
            if (_currentTeam == teamIndex) return;
            int old = _currentTeam;
            _currentTeam = teamIndex;
            this.Log($"Team {old} → {teamIndex}");
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public bool IsFriendly(int otherTeamIndex)
            => _currentTeam != -1 && otherTeamIndex != -1 && _currentTeam == otherTeamIndex;

        public bool IsEnemy(int otherTeamIndex)
            => _currentTeam != -1 && otherTeamIndex != -1 && _currentTeam != otherTeamIndex;

        // === CALLBACKS ÉVÉNEMENTS ===

        public void OnPlayerKilled()
        {
            if (Manager == null || Manager.EventBus == null) return;
            var slot = Manager.EventBus.GetLastSlot(CoreEventType.OnPlayerKilled);
            if (slot == null || slot.KillerId != _playerId) return;

            _kills++;
            _score += 100;
            this.Log($"Kill! Total: {_kills}");
            if (_teamManager != null)
                _teamManager.AddTeamScore(TeamIndex, 10);
        }

        public void OnPlayerDied()
        {
            if (Manager == null || Manager.EventBus == null) return;
            var slot = Manager.EventBus.GetLastSlot(CoreEventType.OnPlayerDied);
            if (slot == null || slot.VictimId != _playerId) return;
            _deaths++;
            this.Log($"Died. Total: {_deaths}");
        }

        public void OnTeamChanged()
        {
            if (Manager == null || Manager.EventBus == null) return;
            var slot = Manager.EventBus.GetLastSlot(CoreEventType.OnTeamChanged);
            if (slot == null || slot.PlayerId != _playerId) return;
            this.Log($"Team changed → {slot.TeamIndex}");
        }

        public void OnGameStarted()
        {
            this.Log("Game started – resetting stats.");
            _kills = 0; _deaths = 0; _score = 0;
        }

        // === STATS ===

        public int   GetKills()   => _kills;
        public int   GetDeaths()  => _deaths;
        public int   GetScore()   => _score;
        public float GetKDRatio() => _deaths > 0 ? (float)_kills / _deaths : _kills;

        // === PRIVATE HELPERS ===

        private void TryFindTeamManager()
        {
            GameObject obj = GameObject.Find("TeamManager");
            if (obj != null) _teamManager = obj.GetComponent<M2922.Teams.M2922_TeamManager>();
        }

        private void TryFindSpawnManager()
        {
            GameObject obj = GameObject.Find("SpawnManager");
            if (obj != null) _spawnManager = obj.GetComponent<M2922.Spawning.M2922_SpawnManager>();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_teamManager  == null) TryFindTeamManager();
            if (_spawnManager == null) TryFindSpawnManager();
        }
#endif
    }
}
