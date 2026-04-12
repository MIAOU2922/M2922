using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Slot de données d'événement réutilisable — pool géré par M2922_EventBus.
    /// 
    /// SETUP SCÈNE:
    ///   Créer N GameObjects enfants sous le GameObject EventBus.
    ///   Attacher ce script sur chacun. L'EventBus les détecte via GetComponentsInChildren.
    ///   N recommandé: 8 à 16 (nombre d'events simultanés maximum).
    /// 
    /// UTILISATION (Publisher):
    ///   var slot = Manager.EventBus.RentSlot();
    ///   slot.KillerId = 5;
    ///   slot.VictimId = 3;
    ///   Manager.EventBus.PublishNetwork(EventType.OnPlayerKilled, slot);
    /// 
    /// UTILISATION (Subscriber — dans le callback):
    ///   var slot = Manager.EventBus.GetLastSlot(EventType.OnPlayerKilled);
    ///   if (slot == null) return;
    ///   if (slot.KillerId == _playerId) { ... }
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_EventData : UdonSharpBehaviour
    {
        // === COMBAT / KILL / DAMAGE ===
        [UdonSynced] public int VictimId = -1;
        [UdonSynced] public int KillerId = -1;
        [UdonSynced] public int AttackerId = -1;
        [UdonSynced] public float Damage = 0f;
        [UdonSynced] public float RemainingHealth = 0f;
        [UdonSynced] public float Distance = 0f;
        [UdonSynced] public int DamageType = 0;         // M2922.Core.DamageType enum → int
        [UdonSynced] public int WeaponType = 0;         // M2922.Core.WeaponType enum → int
        [UdonSynced] public bool IsHeadshot = false;
        [UdonSynced] public bool IsFriendlyFire = false;
        [UdonSynced] public bool IsSuicide = false;
        [UdonSynced] public bool IsMelee = false;
        [UdonSynced] public Vector3 Position = Vector3.zero;
        [UdonSynced] public Vector3 HitNormal = Vector3.up;

        // === PLAYER / SPAWN ===
        [UdonSynced] public int PlayerId = -1;
        [UdonSynced] public bool IsRespawn = false;
        [UdonSynced] public int SpawnPointIndex = -1;
        [UdonSynced] public Quaternion SpawnRotation = Quaternion.identity;

        // === TEAM ===
        [UdonSynced] public int TeamIndex = -1;
        [UdonSynced] public int PreviousTeamIndex = -1;
        [UdonSynced] public int TeamScore = 0;
        [UdonSynced] public int ScoreChange = 0;
        [UdonSynced] public int WinningTeamIndex = -1;

        // === GAME STATE ===
        [UdonSynced] public int NewGameState = 0;       // M2922.Core.GameState enum → int
        [UdonSynced] public int PreviousGameState = 0;
        [UdonSynced] public float TimeLimit = 0f;
        [UdonSynced] public int ScoreLimit = 0;

        // === VEHICLE / WEAPON ===
        [UdonSynced] public int VehicleId = -1;
        [UdonSynced] public int SeatIndex = -1;
        [UdonSynced] public int CurrentAmmo = 0;
        [UdonSynced] public int ReserveAmmo = 0;
        [UdonSynced] public string WeaponName = "";
        [UdonSynced] public string VehicleName = "";

        // Non-synced — références scène locale uniquement
        public GameObject WeaponObject;
        public GameObject VehicleObject;

        /// <summary>
        /// Réinitialiser tous les champs pour réutilisation.
        /// Appelé automatiquement par EventBus.RentSlot() avant chaque usage.
        /// </summary>
        public void Reset()
        {
            VictimId = -1;
            KillerId = -1;
            AttackerId = -1;
            Damage = 0f;
            RemainingHealth = 0f;
            Distance = 0f;
            DamageType = 0;
            WeaponType = 0;
            IsHeadshot = false;
            IsFriendlyFire = false;
            IsSuicide = false;
            IsMelee = false;
            Position = Vector3.zero;
            HitNormal = Vector3.up;

            PlayerId = -1;
            IsRespawn = false;
            SpawnPointIndex = -1;
            SpawnRotation = Quaternion.identity;

            TeamIndex = -1;
            PreviousTeamIndex = -1;
            TeamScore = 0;
            ScoreChange = 0;
            WinningTeamIndex = -1;

            NewGameState = 0;
            PreviousGameState = 0;
            TimeLimit = 0f;
            ScoreLimit = 0;

            VehicleId = -1;
            SeatIndex = -1;
            CurrentAmmo = 0;
            ReserveAmmo = 0;
            WeaponName = "";
            VehicleName = "";

            WeaponObject = null;
            VehicleObject = null;
        }
    }
}
