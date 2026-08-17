using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Inventaire "admin / spawner" : référence TOUS les items de la map via le
    /// M2922_Manager (chaque item s'enregistre LUI-MÊME au Start) et permet de
    /// les (re)spawner — reset de monde, réapparition d'items perdus…
    ///
    /// Items synced (MANUAL) : le spawn passe par une FILE traitée
    /// progressivement — on attend l'ownership avant de spawner pour que l'état
    /// Active soit bien diffusé (RequestSerialization), par lots de 10, sans
    /// jamais bloquer une frame (maps 500-5000 items).
    ///
    /// PERF : 100% événementiel (aucun Update, uniquement des events différés).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Admin Spawner")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_ItemAdminSpawner : M2922_Base
    {
        [Header("=== SPAWN ===")]
        [Tooltip("Point de spawn optionnel. Si vide, chaque item réapparaît à sa position d'origine.")]
        public Transform SpawnPoint;

        // File des items en attente d'ownership avant spawn (sync MANUAL :
        // Active ne part que si on possède l'objet au moment du RequestSerialization).
        private DataList _pendingSpawnQueue = new DataList();
        private int _pendingRetries = 0;
        private const int MAX_RETRIES = 5;
        private const int BATCH = 10;

        /// <summary>Nombre d'items enregistrés dans la map.</summary>
        public int _GetItemCount()
        {
            return Manager != null ? Manager.GetInventoryItemCount() : 0;
        }

        /// <summary>Nom de l'item à l'index donné ("" si hors bornes).</summary>
        public string _GetItemNameAt(int index)
        {
            if (Manager == null) return "";
            M2922_InventoryItem item = Manager.GetInventoryItemAt(index);
            return item != null ? item.ItemName : "";
        }

        /// <summary>True si l'item à l'index donné est actuellement visible dans le monde.</summary>
        public bool _IsItemActiveAt(int index)
        {
            if (Manager == null) return false;
            M2922_InventoryItem item = Manager.GetInventoryItemAt(index);
            return item != null && item._IsActive();
        }

        /// <summary>Respawn les items de la map qui ne sont pas actuellement visibles.</summary>
        public void _SpawnAll()
        {
            if (Manager == null)
            {
                this.Error("[AdminSpawner] Aucun Manager assigné !");
                return;
            }

            int count = Manager.GetInventoryItemCount();
            for (int i = 0; i < count; i++)
            {
                M2922_InventoryItem item = Manager.GetInventoryItemAt(i);
                if (item == null || item._IsActive()) continue;
                _SpawnItem(item);
            }
        }

        /// <summary>Respawn TOUS les items (même ceux déjà visibles) — reset de monde.</summary>
        public void _SpawnAllForced()
        {
            if (Manager == null)
            {
                this.Error("[AdminSpawner] Aucun Manager assigné !");
                return;
            }

            int count = Manager.GetInventoryItemCount();
            for (int i = 0; i < count; i++)
            {
                M2922_InventoryItem item = Manager.GetInventoryItemAt(i);
                if (item == null) continue;
                _SpawnItem(item);
            }
        }

        /// <summary>Respawn le premier item portant ce nom.</summary>
        public void _SpawnItemByName(string itemName)
        {
            if (Manager == null || string.IsNullOrEmpty(itemName)) return;

            int count = Manager.GetInventoryItemCount();
            for (int i = 0; i < count; i++)
            {
                M2922_InventoryItem item = Manager.GetInventoryItemAt(i);
                if (item != null && item.ItemName == itemName)
                {
                    _SpawnItem(item);
                    return;
                }
            }
        }

        /// <summary>Respawn l'item à l'index donné (pour le menu de la map).</summary>
        public void _SpawnItemAt(int index)
        {
            if (Manager == null) return;
            M2922_InventoryItem item = Manager.GetInventoryItemAt(index);
            if (item != null) _SpawnItem(item);
        }

        private void _SpawnItem(M2922_InventoryItem item)
        {
            if (item == null) return;

            // File traitée progressivement : les items non possédés attendent
            // leur transfert d'ownership avant le spawn (sync MANUAL).
            _pendingSpawnQueue.Add(item);
            SendCustomEventDelayedFrames("_TrySpawnNextPending", 1);
        }

        /// <summary>
        /// Traite la file de spawn par lots (BATCH items max) :
        /// - item possédé (ou local) → spawn immédiat ;
        /// - item synced non possédé → demande d'ownership puis retry différé.
        /// Se reprogramme tant que la file n'est pas vide (jamais plus d'un
        /// batch par frame → aucun pic de calcul, même avec des milliers d'items).
        /// </summary>
        public void _TrySpawnNextPending()
        {
            int processed = 0;

            while (_pendingSpawnQueue.Count > 0 && processed < BATCH)
            {
                M2922_InventoryItem item = (M2922_InventoryItem)_pendingSpawnQueue[0].Reference;
                if (item == null)
                {
                    _pendingSpawnQueue.RemoveAt(0);
                    continue;
                }

                if (item._IsNetworked() && !Networking.IsOwner(item.gameObject))
                {
                    _pendingRetries++;
                    if (_pendingRetries > MAX_RETRIES)
                    {
                        this.Warning("[AdminSpawner] Ownership de l'item impossible, spawn annulé.");
                        _pendingSpawnQueue.RemoveAt(0);
                        _pendingRetries = 0;
                        continue;
                    }

                    Networking.SetOwner(Networking.LocalPlayer, item.gameObject);
                    SendCustomEventDelayedSeconds("_TrySpawnNextPending", 0.5f);
                    return;
                }

                _pendingSpawnQueue.RemoveAt(0);
                _pendingRetries = 0;

                // Libère l'item d'un éventuel inventaire MONDE, puis spawn.
                item._SetWorldStored(false);

                Transform point = SpawnPoint != null ? SpawnPoint : item.transform;
                item._Spawn(point);

                processed++;
            }

            if (_pendingSpawnQueue.Count > 0)
                SendCustomEventDelayedFrames("_TrySpawnNextPending", 1);
        }
    }
}
