using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using M2922.Core;
using System;

namespace M2922.Component.Network
{
    /// <summary>
    /// Bus d'événements central : OnHit, OnKill, OnDeath, etc.
    /// Permet à tous les composants de s'abonner sans référence directe.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_EventBus : M2922_Base
    {
        // Événements disponibles
        public const string EVENT_HIT = "OnHit";
        public const string EVENT_KILL = "OnKill";
        public const string EVENT_DEATH = "OnDeath";
        public const string EVENT_RESPAWN = "OnRespawn";
        public const string EVENT_ROUND_START = "OnRoundStart";
        public const string EVENT_ROUND_END = "OnRoundEnd";
        public const string EVENT_ITEM_PICKUP = "OnItemPickup";
        public const string EVENT_DOOR_OPEN = "OnDoorOpen";

        // Stockage simplifié : liste des listeners par événement
        private DataDictionary _listeners;

        protected override void Start()
        {
            base.Start();
            _listeners = new DataDictionary();
        }

        /// <summary>Abonne un composant à un événement.</summary>
        public void Subscribe(string eventName, UdonSharpBehaviour listener)
        {
            if (!_listeners.ContainsKey(eventName))
                _listeners.Add(eventName, new DataList());

            DataList list = (DataList)_listeners[eventName];
            list.Add(listener);
        }

        /// <summary>Émet un événement à tous les abonnés.</summary>
        public void Emit(string eventName, DataDictionary data = null)
        {
            if (!_listeners.ContainsKey(eventName)) return;

            DataList list = (DataList)_listeners[eventName];
            for (int i = 0; i < list.Count; i++)
            {
                UdonSharpBehaviour listener = (UdonSharpBehaviour)list[i].Reference;
                if (listener != null)
                    listener.SendCustomEvent("OnEvent_" + eventName);
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Listeners", _listeners != null ? _listeners.Count.ToString() : "0"),
            };
        }
#endif
    }
}
