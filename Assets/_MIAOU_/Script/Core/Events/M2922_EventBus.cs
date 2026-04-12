using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace M2922.Core
{
    /// <summary>
    /// Système d'événements centralisé pour UdonSharp
    /// Permet la communication entre modules sans dépendances directes
    /// 
    /// UTILISATION:
    /// 1. Subscribe: EventBus.Subscribe(EventType.OnPlayerKilled, this);
    /// 2. Dans votre classe, créer la méthode: public void OnPlayerKilled() { }
    /// 3. Récupérer un slot: var slot = EventBus.RentSlot();
    /// 4. Remplir: slot.KillerId = 5; slot.VictimId = 3;
    /// 5. Publish: EventBus.Publish(EventType.OnPlayerKilled, slot);
    /// 6. Dans le callback: var slot = EventBus.GetLastSlot(EventType.OnPlayerKilled);
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_EventBus : M2922_Base
    {
        [Header("=== EVENT BUS SETTINGS ===")]
        [SerializeField] private int maxListenersPerEvent = 20;
        
        [Header("=== EVENT SYSTEM CONFIG ===")]
        [Tooltip("Nombre de types d'événements (calculé automatiquement dans l'éditeur)")]
        [SerializeField] private int _eventTypeCount = 0;
        [Tooltip("Valeur max de l'enum EventType + 1 (calculé automatiquement dans l'éditeur)")]
        [SerializeField] private int _maxEventTypeValue = 64;
        
        [Header("=== EVENT DATA POOL ===")]
        [Tooltip("Slots M2922_EventData — enfants du GameObject EventBus (auto-découverts si vide)")]
        [SerializeField] private M2922_EventData[] _pool;
        
        // Stockage des listeners pour chaque type d'événement
        private UdonSharpBehaviour[][] _listeners;
        private int[] _listenerCounts;
        
        // Slot actif par type d'événement (indexé par valeur enum)
        private M2922_EventData[] _lastSlot;
        
        // Sync réseau: index du pool slot actif par type d'événement
        [UdonSynced] private int[] _lastSlotIndex;
        
        // Ring buffer head
        private int _poolHead = 0;
        
        // Note: UdonSharp ne supporte pas les champs statiques
        // Utilisez Manager.EventBus pour accéder à l'EventBus
        
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        /// <summary>
        /// Calculer automatiquement le nombre de types d'événements dans l'éditeur
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateEventTypeCount();
        }
        
        private void UpdateEventTypeCount()
        {
            var values = System.Enum.GetValues(typeof(EventType));
            int count = values.Length;
            int maxVal = 0;
            foreach (var v in values)
            {
                int iv = (int)v;
                if (iv > maxVal) maxVal = iv;
            }
            if (_eventTypeCount != count)
            {
                _eventTypeCount = count;
                UnityEngine.Debug.Log($"[M2922 EventBus] Event count: {_eventTypeCount}");
            }
            if (_maxEventTypeValue != maxVal + 1)
            {
                _maxEventTypeValue = maxVal + 1;
                UnityEngine.Debug.Log($"[M2922 EventBus] Max event value: {_maxEventTypeValue}");
            }
        }
#endif
        
        protected override void Start()
        {
            base.Start();
            
            // Auto-découverte du pool depuis les enfants si non assigné en inspector
            if (_pool == null || _pool.Length == 0)
                _pool = GetComponentsInChildren<M2922_EventData>();
            
            InitializeEventSystem();
            this.Log($"EventBus initialized. Pool: {(_pool != null ? _pool.Length : 0)} slots");
        }
        
        private void InitializeEventSystem()
        {
            int arraySize = _maxEventTypeValue;
            
            _listeners = new UdonSharpBehaviour[arraySize][];
            _listenerCounts = new int[arraySize];
            _lastSlot = new M2922_EventData[arraySize];
            _lastSlotIndex = new int[arraySize];
            
            for (int i = 0; i < arraySize; i++)
            {
                _listeners[i] = new UdonSharpBehaviour[maxListenersPerEvent];
                _listenerCounts[i] = 0;
                _lastSlot[i] = null;
                _lastSlotIndex[i] = -1;
            }
        }
        
        /// <summary>
        /// Emprunter un slot du pool. Remplir ses champs, puis appeler Publish/PublishNetwork.
        /// Le slot est automatiquement réinitialisé (Reset) avant d'être retourné.
        /// </summary>
        public M2922_EventData RentSlot()
        {
            if (_pool == null || _pool.Length == 0)
            {
                this.Warning("Event pool vide! Ajoutez des enfants M2922_EventData sous l'EventBus.");
                return null;
            }
            M2922_EventData slot = _pool[_poolHead % _pool.Length];
            _poolHead++;
            slot.Reset();
            return slot;
        }
        
        /// <summary>
        /// Récupérer le dernier slot publié pour un type d'événement.
        /// À appeler DANS le callback de l'événement.
        /// </summary>
        public M2922_EventData GetLastSlot(EventType eventType)
        {
            return _lastSlot[(int)eventType];
        }
        
        /// <summary>
        /// S'abonner à un événement
        /// </summary>
        /// <param name="eventType">Type d'événement</param>
        /// <param name="listener">Script qui écoute (doit avoir une méthode publique nommée comme l'événement)</param>
        public void Subscribe(EventType eventType, UdonSharpBehaviour listener)
        {
            if (listener == null)
            {
                this.Warning($"Trying to subscribe null listener to {eventType}");
                return;
            }
            
            int typeIndex = (int)eventType;
            int currentCount = _listenerCounts[typeIndex];
            
            if (currentCount >= maxListenersPerEvent)
            {
                this.Warning($"Max listeners reached for {eventType}. Increase maxListenersPerEvent.");
                return;
            }
            
            // Vérifier si déjà abonné
            for (int i = 0; i < currentCount; i++)
            {
                if (_listeners[typeIndex][i] == listener)
                {
                    this.VerboseWarning($"{listener.name} already subscribed to {eventType}");
                    return;
                }
            }
            
            _listeners[typeIndex][currentCount] = listener;
            _listenerCounts[typeIndex]++;
            
            this.VerboseLog($"{listener.name} subscribed to {eventType}");
        }
        
        /// <summary>
        /// Se désabonner d'un événement
        /// </summary>
        public void Unsubscribe(EventType eventType, UdonSharpBehaviour listener)
        {
            if (listener == null) return;
            
            int typeIndex = (int)eventType;
            int currentCount = _listenerCounts[typeIndex];
            
            for (int i = 0; i < currentCount; i++)
            {
                if (_listeners[typeIndex][i] == listener)
                {
                    // Décaler tous les éléments après celui-ci
                    for (int j = i; j < currentCount - 1; j++)
                    {
                        _listeners[typeIndex][j] = _listeners[typeIndex][j + 1];
                    }
                    
                    _listeners[typeIndex][currentCount - 1] = null;
                    _listenerCounts[typeIndex]--;
                    
                    this.VerboseLog($"{listener.name} unsubscribed from {eventType}");
                    return;
                }
            }
        }
        
        /// <summary>
        /// Publier un événement (local seulement).
        /// Appeler RentSlot() + remplir le slot AVANT d'appeler cette méthode.
        /// </summary>
        public void Publish(EventType eventType, M2922_EventData slot)
        {
            int typeIndex = (int)eventType;
            _lastSlot[typeIndex] = slot;
            
            int listenerCount = _listenerCounts[typeIndex];
            string eventName = eventType.ToString();
            
            this.VerboseLog($"Publishing {eventType} to {listenerCount} listeners");
            
            for (int i = 0; i < listenerCount; i++)
            {
                UdonSharpBehaviour listener = _listeners[typeIndex][i];
                if (listener != null && listener.gameObject.activeInHierarchy)
                {
                    listener.SendCustomEvent(eventName);
                }
            }
        }
        
        /// <summary>
        /// Publier un événement à tous les joueurs (synced).
        /// Le slot et son index sont synchronisés avant l'envoi réseau.
        /// </summary>
        public void PublishNetwork(EventType eventType, M2922_EventData slot)
        {
            int typeIndex = (int)eventType;
            _lastSlotIndex[typeIndex] = GetSlotPoolIndex(slot);
            
            // Dispatch local
            Publish(eventType, slot);
            
            // Synchroniser le slot et l'index du slot
            if (slot != null && Networking.IsOwner(slot.gameObject))
                slot.RequestSerialization();
            if (Networking.IsOwner(gameObject))
                RequestSerialization();
            
            // Propager via réseau
            SendCustomNetworkEvent(NetworkEventTarget.All, $"_Network{eventType}");
            
            this.VerboseLog($"Network publishing {eventType}");
        }
        
        /// <summary>
        /// Reçu lors d'une sync réseau: reconstruit _lastSlot depuis _lastSlotIndex.
        /// </summary>
        public override void OnDeserialization()
        {
            if (_pool == null || _lastSlotIndex == null || _lastSlot == null) return;
            for (int i = 0; i < _lastSlotIndex.Length; i++)
            {
                int idx = _lastSlotIndex[i];
                if (idx >= 0 && idx < _pool.Length)
                    _lastSlot[i] = _pool[idx];
            }
        }
        
        /// <summary>
        /// Obtenir le nombre de listeners pour un événement.
        /// </summary>
        public int GetListenerCount(EventType eventType)
        {
            return _listenerCounts[(int)eventType];
        }
        
        private int GetSlotPoolIndex(M2922_EventData slot)
        {
            if (slot == null || _pool == null) return -1;
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i] == slot) return i;
            return -1;
        }
    }
    
    /// <summary>
    /// Types d'événements du système PvP
    /// IMPORTANT: Le nom de l'enum DOIT correspondre au nom de la méthode publique dans les listeners
    /// </summary>
    public enum EventType
    {
        // === EVENTS CORE ===
        OnGameStarted = 0,
        OnGameEnded = 1,
        OnRoundStarted = 2,
        OnRoundEnded = 3,
        
        // === EVENTS JOUEUR ===
        OnPlayerJoined = 10,
        OnPlayerLeft = 11,
        OnPlayerSpawned = 12,
        OnPlayerDied = 13,
        OnPlayerRespawned = 14,
        OnPlayerDamaged = 15,
        OnPlayerHealed = 16,
        
        // === EVENTS COMBAT ===
        OnWeaponFired = 20,
        OnWeaponReloaded = 21,
        OnWeaponEquipped = 22,
        OnWeaponDropped = 23,
        OnPlayerKilled = 24,
        OnDamageDealt = 25,
        OnHeadshotScored = 26,
        
        // === EVENTS TEAMS ===
        OnTeamChanged = 30,
        OnTeamScoreChanged = 31,
        OnTeamWon = 32,
        
        // === EVENTS VÉHICULES ===
        OnVehicleEntered = 40,
        OnVehicleExited = 41,
        OnVehicleDestroyed = 42,
        OnVehicleDamaged = 43,
        
        // === EVENTS OBJECTIFS ===
        OnObjectiveCaptured = 50,
        OnObjectiveLost = 51,
        OnObjectiveNeutralized = 52,
        
        // === EVENTS POWERUPS ===
        OnPowerupCollected = 60,
        OnPowerupExpired = 61,
        OnPowerupSpawned = 62
    }
}
