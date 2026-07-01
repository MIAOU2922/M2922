using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Health;

namespace M2922.Component.Game
{
    /// <summary>
    /// Points de spawn et système de respawn.
    /// </summary>
    [AddComponentMenu("M2922/Game/Spawn Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_SpawnManager : M2922_Base
    {
        [Header("=== SPAWN POINTS ===")]
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private float _spawnProtectionDuration = 3f;

        public Transform GetRandomSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return null;
            return _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        }

        public Transform GetTeamSpawnPoint(int teamIndex)
        {
            // Utilise un spawn indexé par équipe
            if (_spawnPoints == null) return null;
            int index = Mathf.Clamp(teamIndex, 0, _spawnPoints.Length - 1);
            return _spawnPoints[index];
        }

        public void SpawnPlayer(VRCPlayerApi player)
        {
            Transform spawn = GetRandomSpawnPoint();
            if (spawn != null)
            {
                player.TeleportTo(spawn.position, spawn.rotation);
                this.Log($"Player {player.displayName} spawned at {spawn.name}");
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            int count = _spawnPoints != null ? _spawnPoints.Length : 0;
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Spawns", count.ToString(), Color.cyan),
                new M2922_GizmoDisplayInfo("Protection", $"{_spawnProtectionDuration}s"),
            };
        }
#endif
    }
}
