using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// MENU ADMIN DE LA MAP — inventaire de TOUS les items de la map.
    ///
    /// Fonctionne COMME un M2922_WorldInventory (menu sur place, zone
    /// d'insertion, bouton Spawn, recherche, tri, panneau de détail, poids)
    /// mais sa liste contient TOUS les items enregistrés auprès du
    /// M2922_Manager (chaque item s'enregistre lui-même au Start) au lieu
    /// des seuls items rangés dans un coffre.
    ///
    /// - Un item SPAWNÉ reste dans la liste : son bouton affiche
    ///   "✔ Dans le monde" ; un item masqué affiche "✘ Rangé / absent".
    /// - Ranger = déposer l'objet dans la zone d'insertion : ownership
    ///   transférée (retry), StoredInWorld + masquage, l'item reste en liste.
    /// - Spawner = bouton Spawn du panneau de détail (ou _SpawnItem) :
    ///   l'item réapparaît au SpawnPoint (ou sur le menu) et reste en liste.
    /// - L'état vient des items eux-mêmes (M2922_InventoryItemSynced
    ///   synchronise Active) → AUCUN [UdonSynced] sur ce script
    ///   (BehaviourSyncMode.None OK). Statuts rafraîchis à l'ouverture,
    ///   après chaque action, et ≈1×/sec tant que le menu est ouvert.
    ///
    /// ⚠ Items : seuls les M2922_InventoryItemSynced peuvent être rangés
    /// (un item local ne synchronise pas son état avec les autres joueurs).
    ///
    /// PERF : hérite du tickable de M2922_Inventory (touche I + distance) ;
    /// le refresh de statuts ne tourne que lorsque le menu est ouvert.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Admin Inventory")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_AdminInventory : M2922_Inventory
    {
        private const int MAX_RETRIES = 5;
        private const int STATUS_REFRESH_FRAMES = 60;

        [Header("=== ADMIN ===")]
        [Tooltip("OPTIONNEL : spawner admin utilisé par _RestoreAll / _ResetWorld.")]
        public M2922_ItemAdminSpawner AdminSpawner;

        [Header("=== UI (optionnel) ===")]
        [Tooltip("TMP affichant le résumé 'X / Y items dans le monde'.")]
        public TextMeshProUGUI StatusText;

        // Relay d'ownership d'item (un item en attente à la fois).
        private M2922_InventoryItem _pendingHideItem;
        private int _pendingHideRetries = 0;
        private M2922_InventoryItem _pendingSpawnItem;
        private int _pendingSpawnRetries = 0;
        // Curseur de rotation du stack : garantit de parcourir TOUS les
        // exemplaires du stack à chaque spawn successif, même si Active ne
        // reflète pas l'état réel (bug réseau / ClientSim).
        private int _spawnCursor = 0;
        private int _cursorStackIndex = -1;

        private bool _built = false;
        private int _statusRefreshCounter = 0;

        /// <summary>Le menu reste à sa place dans le monde (il ne suit pas le joueur).</summary>
        protected override bool _MenuFollowsPlayer()
        {
            return false;
        }

        /// <summary>Cliquer le menu (interactable VRC) l'ouvre/le ferme.</summary>
        public override void Interact()
        {
            _ToggleMenu();
        }

        protected override void Update()
        {
            base.Update();

            if (MenuContainer == null || !MenuContainer.activeSelf) return;

            if (!_built)
            {
                // Construit la liste à la première ouverture (tous les Starts
                // sont déjà passés à ce moment-là : le registre est complet).
                _built = _RebuildFromRegistry();
            }
            else
            {
                // Rafraîchit les statuts ≈1×/sec tant que le menu est ouvert.
                _statusRefreshCounter++;
                if (_statusRefreshCounter >= STATUS_REFRESH_FRAMES)
                {
                    _statusRefreshCounter = 0;
                    _RefreshStatuses();
                }
            }
        }

        // ============================================================
        // RANGEMENT / SPAWN (comme WorldInventory, mais la liste garde tout)
        // ============================================================

        public override void _AddItem(M2922_InventoryItem item)
        {
            if (item == null) return;

            if (!item._IsNetworked())
            {
                this.Warning($"[MapItemMenu] '{item.ItemName}' est LOCAL : seuls les items synchronisés peuvent être rangés.");
                return;
            }

            if (Inserter != null)
                Inserter._Highlight(false);

            _RequestItemHide(item);
        }

        public override void _SpawnItem()
        {
            if (_selectedItem == null) return;

            DataList stack = _GetStackFromEntry(_selectedItem);
            if (stack == null || stack.Count == 0) return;

            // Reset le curseur quand on change de stack sélectionné.
            int stackIndex = ItemList.IndexOf(_selectedItem);
            if (stackIndex != _cursorStackIndex)
            {
                _cursorStackIndex = stackIndex;
                _spawnCursor = 0;
            }

            int start = _spawnCursor % stack.Count;

            // PASS 1 : premier item NON actif À PARTIR du curseur (sans boucler —
            // les items avant le curseur seront atteints à la rotation suivante).
            M2922_InventoryItem item = null;
            for (int i = start; i < stack.Count; i++)
            {
                M2922_InventoryItem candidate = (M2922_InventoryItem)stack[i].Reference;
                if (candidate == null) continue;
                if (!candidate._IsActive())
                {
                    item = candidate;
                    _spawnCursor = i + 1;
                    break;
                }
            }

            // PASS 2 : rien d'inactif après le curseur → respawn le suivant et
            // AVANCE le curseur (garantit de parcourir tous les exemplaires).
            if (item == null)
            {
                for (int offset = 0; offset < stack.Count; offset++)
                {
                    int i = (start + offset) % stack.Count;
                    item = (M2922_InventoryItem)stack[i].Reference;
                    if (item != null)
                    {
                        _spawnCursor = i + 1;
                        break;
                    }
                }
            }
            if (item == null) return;

            this.Log($"[MapItemMenu] Spawn de '{item.name}' (curseur {_spawnCursor - 1}/{stack.Count}, Active={item._IsActive()}).");

            // Spawn EXACTEMENT au SpawnPoint : l'étalement vient de la
            // libération kinematic 5 s après le spawn (items non-kinematic
            // par défaut qui retombent physiquement).
            _RequestItemSpawn(item);
        }

        /// <summary>Range l'item (ownership relay + retry), puis rafraîchit les statuts.</summary>
        private void _RequestItemHide(M2922_InventoryItem item)
        {
            if (Networking.IsOwner(item.gameObject))
            {
                item._SetWorldStored(true);
                item._Hide();
                _RefreshStatuses();
                return;
            }

            _pendingHideItem = item;
            _pendingHideRetries = 0;
            Networking.SetOwner(_localPlayer, item.gameObject);
            SendCustomEventDelayedSeconds("_TryHidePendingItem", 1f);
        }

        /// <summary>Retry différé du rangement (tant que l'ownership de l'item n'est pas effective).</summary>
        public void _TryHidePendingItem()
        {
            if (_pendingHideItem == null) return;

            if (Networking.IsOwner(_pendingHideItem.gameObject))
            {
                M2922_InventoryItem item = _pendingHideItem;
                _pendingHideItem = null;
                item._SetWorldStored(true);
                item._Hide();
                _RefreshStatuses();
                return;
            }

            _pendingHideRetries++;
            if (_pendingHideRetries > MAX_RETRIES)
            {
                this.Warning("[MapItemMenu] Ownership de l'item impossible, rangement annulé.");
                _pendingHideItem = null;
                return;
            }

            Networking.SetOwner(_localPlayer, _pendingHideItem.gameObject);
            SendCustomEventDelayedSeconds("_TryHidePendingItem", 1f);
        }

        /// <summary>Spawn l'item (ownership relay + retry), puis rafraîchit les statuts.</summary>
        private void _RequestItemSpawn(M2922_InventoryItem item)
        {
            if (Networking.IsOwner(item.gameObject))
            {
                item._SetWorldStored(false);
                SpawnAt(item);
                _RefreshStatuses();
                return;
            }

            _pendingSpawnItem = item;
            _pendingSpawnRetries = 0;
            Networking.SetOwner(_localPlayer, item.gameObject);
            SendCustomEventDelayedSeconds("_TrySpawnPendingItem", 1f);
        }

        /// <summary>Spawn l'item EXACTEMENT au SpawnPoint (ou sur le menu).</summary>
        private void SpawnAt(M2922_InventoryItem item)
        {
            Vector3 pos = SpawnPoint != null ? SpawnPoint.position : transform.position;
            Quaternion rot = SpawnPoint != null ? SpawnPoint.rotation : transform.rotation;
            item._SpawnAt(pos, rot);
            this.Log($"[MapItemMenu] SpawnAt '{item.name}' pos={pos} Active après spawn={item._IsActive()}");
        }

        /// <summary>Retry différé du spawn (tant que l'ownership de l'item n'est pas effective).</summary>
        public void _TrySpawnPendingItem()
        {
            if (_pendingSpawnItem == null) return;

            if (Networking.IsOwner(_pendingSpawnItem.gameObject))
            {
                M2922_InventoryItem item = _pendingSpawnItem;
                _pendingSpawnItem = null;
                item._SetWorldStored(false);
                SpawnAt(item);
                _RefreshStatuses();
                return;
            }

            _pendingSpawnRetries++;
            if (_pendingSpawnRetries > MAX_RETRIES)
            {
                this.Warning("[MapItemMenu] Ownership de l'item impossible, spawn annulé.");
                _pendingSpawnItem = null;
                return;
            }

            Networking.SetOwner(_localPlayer, _pendingSpawnItem.gameObject);
            SendCustomEventDelayedSeconds("_TrySpawnPendingItem", 1f);
        }

        // ============================================================
        // LISTE = REGISTRE DU MANAGER (tous les items de la map)
        // ============================================================

        /// <summary>
        /// Reconstruit la liste locale depuis le registre du Manager
        /// (tous les items de la map). Retourne false si le build est impossible.
        /// </summary>
        private bool _RebuildFromRegistry()
        {
            if (Manager == null)
            {
                this.Error("[MapItemMenu] Aucun Manager assigné !");
                return false;
            }
            if (ButtonPrefab == null || ButtonParent == null)
            {
                this.Error("[MapItemMenu] ButtonPrefab / ButtonParent non assignés.");
                return false;
            }

            // Nettoyer la liste locale (boutons + données).
            for (int i = 0; i < ItemList.Count; i++)
            {
                GameObject button = _GetButtonFromEntry(ItemList[i].DataDictionary);
                if (button != null) Destroy(button);
            }
            ItemList.Clear();
            ClearMenu();

            int count = Manager.GetInventoryItemCount();
            for (int i = 0; i < count; i++)
            {
                M2922_InventoryItem item = Manager.GetInventoryItemAt(i);
                if (item == null) continue;
                _AddItemToStackView(item);
            }

            _SortList();

            if (ItemList.Count > 0)
                _SelectItem(ItemList[0].DataDictionary);

            _RefreshWeightText();
            _RefreshStatuses();
            return true;
        }

        /// <summary>Alias public : reconstruit la liste (bouton de refresh manuel).</summary>
        public void _RebuildMenu()
        {
            _RebuildFromRegistry();
            _built = true;
        }

        /// <summary>Rafraîchit l'état ✔/✘ de chaque bouton + le résumé.</summary>
        public void _RefreshStatuses()
        {
            int visible = 0;
            int total = 0;

            for (int i = 0; i < ItemList.Count; i++)
            {
                DataDictionary entry = ItemList[i].DataDictionary;
                DataList stack = _GetStackFromEntry(entry);
                if (stack == null) continue;

                bool anyActive = false;
                for (int j = 0; j < stack.Count; j++)
                {
                    M2922_InventoryItem item = (M2922_InventoryItem)stack[j].Reference;
                    if (item == null) continue;

                    total++;
                    if (item._IsActive())
                    {
                        anyActive = true;
                        visible++;
                    }
                }

                GameObject buttonObj = _GetButtonFromEntry(entry);
                if (buttonObj == null) continue;

                M2922_InventoryButtonUI button = buttonObj.GetComponent<M2922_InventoryButtonUI>();
                if (button != null) button._SetStatus(anyActive);
            }

            if (StatusText != null)
                StatusText.text = $"{visible} / {total} items dans le monde";
        }

        /// <summary>Ajoute un item à la vue locale en le stackant (respecte MaxStackSize).</summary>
        private void _AddItemToStackView(M2922_InventoryItem item)
        {
            int stackIndex = _FindStackIndexFor(item);
            if (stackIndex >= 0)
            {
                DataDictionary entry = ItemList[stackIndex].DataDictionary;
                DataList stack = _GetStackFromEntry(entry);
                if (stack != null)
                {
                    stack.Add(item);
                    _RefreshButtonCount(entry);
                    return;
                }
                // Entrée corrompue (reliquat sérialisé) : retirée avant d'en créer une nouvelle.
                ItemList.RemoveAt(stackIndex);
            }

            DataList newStack = new DataList();
            newStack.Add(item);

            GameObject buttonObj = Instantiate(ButtonPrefab, ButtonParent);
            buttonObj.name = $"{item.name} Button";

            DataDictionary itemDictionary = new DataDictionary();
            itemDictionary[ID_BUTTON] = buttonObj;
            itemDictionary[ID_ITEM] = item;
            itemDictionary[ID_STACK] = newStack;
            ItemList.Add(itemDictionary);

            M2922_InventoryButtonUI button = buttonObj.GetComponent<M2922_InventoryButtonUI>();
            if (button != null) button._Init(this, itemDictionary);
        }

        // ============================================================
        // ADMIN (restauration / reset)
        // ============================================================

        /// <summary>Respawn tous les items non visibles (réparation de map).</summary>
        public void _RestoreAll()
        {
            if (AdminSpawner == null)
            {
                this.Warning("[MapItemMenu] Aucun AdminSpawner assigné : _RestoreAll ignoré.");
                return;
            }

            AdminSpawner._SpawnAll();
            _RefreshStatuses();
        }

        /// <summary>Respawn TOUS les items, même ceux déjà visibles (reset de monde).</summary>
        public void _ResetWorld()
        {
            if (AdminSpawner == null)
            {
                this.Warning("[MapItemMenu] Aucun AdminSpawner assigné : _ResetWorld ignoré.");
                return;
            }

            AdminSpawner._SpawnAllForced();
            _RefreshStatuses();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Items (registre)", Manager != null ? Manager.GetInventoryItemCount().ToString() : "-"),
                new M2922_GizmoDisplayInfo("Dans le monde", _CountActiveItems().ToString()),
                new M2922_GizmoDisplayInfo("Menu", MenuContainer != null && MenuContainer.activeSelf ? "Ouvert" : "Fermé"),
            };
        }

        private int _CountActiveItems()
        {
            int active = 0;
            for (int i = 0; i < ItemList.Count; i++)
            {
                DataList stack = _GetStackFromEntry(ItemList[i].DataDictionary);
                if (stack == null) continue;
                for (int j = 0; j < stack.Count; j++)
                {
                    M2922_InventoryItem item = (M2922_InventoryItem)stack[j].Reference;
                    if (item != null && item._IsActive()) active++;
                }
            }
            return active;
        }
#endif
    }
}
