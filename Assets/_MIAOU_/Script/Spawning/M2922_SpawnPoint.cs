using UdonSharp;
using UnityEngine;
using M2922.Core;

namespace M2922.Spawning
{
    /// <summary>
    /// Point de spawn simple
    /// Marque une position/rotation pour faire apparaître des joueurs
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_SpawnPoint : M2922_Base
    {
        [Header("=== SPAWN SETTINGS ===")]
        [Tooltip("Index de l'équipe (si mode team-based, -1 pour FFA)")]
        [SerializeField] private int _teamIndex = -1;
        
        [Tooltip("Priorité de ce spawn (plus élevé = plus prioritaire)")]
        [SerializeField] private int _priority = 0;
        
        [Tooltip("Ce spawn est-il actuellement actif?")]
        [SerializeField] private bool _isActive = true;

        // === PROPERTIES ===
        public int TeamIndex => _teamIndex;
        public int Priority => _priority;
        public bool IsActive => _isActive;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
        
        // === RUNTIME ===
        private float _lastSpawnTime = 0f;
        private int _spawnCount = 0;
        
        protected override void Start()
        {
            base.Start();
            this.VerboseLog($"SpawnPoint initialized. Team: {_teamIndex}, Priority: {_priority}");
        }
        
        /// <summary>
        /// Enregistrer qu'un spawn a eu lieu ici
        /// </summary>
        public void RecordSpawn()
        {
            _lastSpawnTime = Time.time;
            _spawnCount++;
            this.VerboseLog($"Spawn recorded. Total spawns: {_spawnCount}");
        }
        
        /// <summary>
        /// Obtenir le temps écoulé depuis le dernier spawn
        /// </summary>
        public float GetTimeSinceLastSpawn()
        {
            return Time.time - _lastSpawnTime;
        }
        
        /// <summary>
        /// Activer/désactiver ce spawn
        /// </summary>
        public void SetActive(bool active)
        {
            _isActive = active;
            this.VerboseLog($"SpawnPoint {(_isActive ? "activated" : "deactivated")}");
        }
        
        /// <summary>
        /// Définir l'index de l'équipe
        /// </summary>
        public void SetTeamIndex(int teamIndex)
        {
            _teamIndex = teamIndex;
        }
        
        // === DEBUG VISUALIZATION ===
        
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;
            // Dessiner la position
            Gizmos.color = _isActive ? _gizmoColor : Color.gray;
            Gizmos.DrawWireSphere(transform.position, _gizmoSize);
            
            // Dessiner la direction (flèche)
            Gizmos.color = _isActive ? _gizmoColor : Color.gray;
            Vector3 direction = transform.forward;
            Vector3 start = transform.position;
            Vector3 end = start + direction * (_gizmoSize * 2f);
            
            Gizmos.DrawLine(start, end);
            
            // Dessiner la pointe de la flèche
            Vector3 right = transform.right * (_gizmoSize * 0.3f);
            Vector3 arrowPoint1 = end - direction * (_gizmoSize * 0.5f) + right;
            Vector3 arrowPoint2 = end - direction * (_gizmoSize * 0.5f) - right;
            
            Gizmos.DrawLine(end, arrowPoint1);
            Gizmos.DrawLine(end, arrowPoint2);

            // Afficher les infos
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 3f),
                $"Spawn Point\nTeam: {(_teamIndex >= 0 ? _teamIndex.ToString() : "FFA")}\nPriority: {_priority}"
            );
        }
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
            // Afficher une zone plus large quand sélectionné
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.2f);
            Gizmos.DrawSphere(transform.position, _gizmoSize * 2f);
        }
#endif
    }
}
