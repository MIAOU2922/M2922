using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Inventaire "admin / spawner" : référence TOUS les items de la map via le
    /// M2922_Manager (chaque item s'enregistre LUI-MÊME au Start) et permet de
    /// les (re)spawner — reset de monde, réapparition d'items perdus…
    ///
    /// Items synced : on demande l'ownership avant de spawner pour que l'état
    /// Active soit diffusé aux autres joueurs (spawn local immédiat, propagation
    /// réseau au tick Continuous suivant).
    ///
    /// PERF : 100% événementiel (aucun Update).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Admin Spawner")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_ItemAdminSpawner : M2922_Base
    {
        [Header("=== SPAWN ===")]
        [Tooltip("Point de spawn optionnel. Si vide, chaque item réapparaît à sa position d'origine.")]
        public Transform SpawnPoint;

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
            // Libère l'item d'un éventuel inventaire MONDE avant de le spawner.
            item._SetWorldStored(false);

            // Items synced : ownership nécessaire pour diffuser l'état Active.
            if (item._IsNetworked() && !Networking.IsOwner(item.gameObject))
                Networking.SetOwner(Networking.LocalPlayer, item.gameObject);

            Transform point = SpawnPoint != null ? SpawnPoint : item.transform;
            item._Spawn(point);
        }
    }
}
