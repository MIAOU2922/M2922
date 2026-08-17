using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Méthode de tri de la liste d'inventaire.
    /// </summary>
    public enum M2922_InventorySorting
    {
        Latest, // Le plus récemment rangé en premier
        AZ,     // Ordre alphabétique
    }

    /// <summary>
    /// Inventaire M2922 — adaptation du système "Pickup Inventory" de Vowgan
    /// aux conventions M2922 (M2922_Base / M2922_Tickable, AddComponentMenu, debug, perf).
    ///
    /// Fonctionnement :
    ///   - 100% LOCAL (BehaviourSyncMode.None) : chaque joueur a SA propre liste d'items.
    ///   - Ouverture : touche "I" (PC) ou regard vers le bas (VR, InputLookVertical).
    ///   - Le menu se téléporte devant le joueur quand il s'ouvre.
    ///   - Fermeture auto si le joueur s'éloigne (HideDistance).
    ///
    /// PERF : Update() ne fait qu'un check de touche par frame + un check de
    /// distance tous les 2×N frames (FrameSkipCount). Tout le reste est événementiel.
    ///
    /// SETUP : voir README.md du dossier Inventory.
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Inventory")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_Inventory : M2922_Tickable
    {
        public const string ID_BUTTON = "Button";
        public const string ID_ITEM = "Item";
        public const string ID_STACK = "Stack";

        [Header("=== COMPORTEMENT ===")]
        [Tooltip("Méthode de tri de la liste (Latest = plus récent d'abord, AZ = alphabétique).")]
        public M2922_InventorySorting Sorting = M2922_InventorySorting.Latest;
        [Tooltip("Distance au-delà de laquelle le menu se ferme automatiquement.")]
        public float HideDistance = 5;
        [Tooltip("Poids maximum que l'inventaire peut contenir. -1 = pas de limite.")]
        public int MaxWeight = -1;

        [Header("=== RÉFÉRENCES ===")]
        [Tooltip("Prefab d'un bouton d'item (avec M2922_InventoryButtonUI).")]
        public GameObject ButtonPrefab;
        [Tooltip("Parent des boutons instanciés (contenu de la ScrollView).")]
        public Transform ButtonParent;
        [Tooltip("Point d'apparition des objets sortis de l'inventaire.")]
        public Transform SpawnPoint;
        [Tooltip("Zone d'insertion (M2922_InventoryInserter).")]
        public M2922_InventoryInserter Inserter;

        [Header("=== UI ===")]
        [Tooltip("Racine du menu (canvas).")]
        public GameObject MenuContainer;
        [Tooltip("Champ de recherche (TMP_InputField).")]
        public TMP_InputField SearchingField;
        [Tooltip("Dropdown de tri TMP (0 = Latest, 1 = AZ).")]
        public TMP_Dropdown SortingDropdown;
        [Tooltip("Icône de l'item sélectionné (panneau de détail).")]
        public Image ItemIcon;
        [Tooltip("Nom de l'item sélectionné.")]
        public TextMeshProUGUI ItemName;
        [Tooltip("Description de l'item sélectionné.")]
        public TextMeshProUGUI ItemDescription;
        [Tooltip("Bouton 'Spawn' du panneau de détail.")]
        public Button SpawnButton;
        [Tooltip("OPTIONNEL : TMP affichant le poids actuel (ex : '12 / 50', '12 / ∞' si illimité).")]
        public TextMeshProUGUI WeightText;

        [Header("=== RUNTIME (lecture seule) ===")]
        [Tooltip("Liste des items rangés. Chaque entrée : DataDictionary { Button, Item }.")]
        [HideInInspector] public DataList ItemList = new DataList();

        protected VRCPlayerApi _localPlayer;
        protected DataDictionary _selectedItem;
        private float _lastVertical;

        /// <summary>
        /// True = le menu se téléporte devant le joueur à l'ouverture (inventaire PERSONNEL).
        /// False = le menu reste à sa place dans le monde (inventaire MONDE / coffre).
        /// </summary>
        protected virtual bool _MenuFollowsPlayer()
        {
            return true;
        }

        /// <summary>
        /// Le check de distance (fermeture auto) tourne 2× moins souvent que
        /// le FrameSkip configuré (≈ 1×/sec par défaut → ≈ 1 check toutes les 2 sec).
        /// </summary>
        protected override int FrameSkipCount
        {
            get { return _updateEveryNFrames * 2; }
        }

        protected override void Start()
        {
            base.Start();

            _localPlayer = Networking.LocalPlayer;
            ClearMenu();
            _CloseMenu();
        }

        protected override void Update()
        {
            base.Update();

            // Ouverture clavier (PC) — checké chaque frame, coût négligeable.
            if (Input.GetKeyDown(KeyCode.I))
            {
                _ToggleMenu();
            }

            // Fermeture auto si le joueur est trop loin — seulement tous les N frames (perf).
            if (ShouldUpdate())
            {
                _UpdateDistanceCulling();
            }
        }

        /// <summary>
        /// Fermeture auto à distance façon M2922_LookAtCamera : distance
        /// TÊTE → système. Au-delà de HideDistance : fermeture COMPLÈTE du menu
        /// via _CloseMenu() (MenuContainer).
        /// </summary>
        private void _UpdateDistanceCulling()
        {
            VRCPlayerApi.TrackingData tracking = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 headPos = tracking.position;
            float sqrDist = (headPos - transform.position).sqrMagnitude;

            if (sqrDist > HideDistance * HideDistance)
                _CloseMenu();
        }

        /// <summary>
        /// Ouverture du menu en VR : regard vers le bas (axe vertical < -0.5).
        /// </summary>
        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (!_localPlayer.IsUserInVR()) return;

            if (_lastVertical > -0.5f && value <= -0.5f)
            {
                _ToggleMenu();
            }

            _lastVertical = value;
        }

        /// <summary>
        /// Bascule l'état du menu : ouvert → ferme, fermé → ouvre
        /// (le menu suit le joueur à l'ouverture via SpawnAtPlayer).
        /// </summary>
        public void _ToggleMenu()
        {
            if (MenuContainer == null) return;

            if (MenuContainer.activeSelf)
            {
                _CloseMenu();
            }
            else
            {
                SpawnAtPlayer();
            }
        }

        /// <summary>Ouvre le menu (ou le replace devant le joueur s'il est déjà ouvert).</summary>
        public void _OpenMenu()
        {
            SpawnAtPlayer();
        }

        /// <summary>Ferme le menu.</summary>
        public void _CloseMenu()
        {
            if (MenuContainer != null)
                MenuContainer.SetActive(false);
        }

        private void SpawnAtPlayer()
        {
            if (MenuContainer != null)
                MenuContainer.SetActive(true);

            if (!_MenuFollowsPlayer()) return;

            transform.localScale = Vector3.one * _localPlayer.GetAvatarEyeHeightAsMeters();
            transform.position = _localPlayer.GetPosition();
            transform.rotation = _localPlayer.GetRotation();
        }

        /// <summary>
        /// Range un item dans l'inventaire : prend sa propriété réseau, crée son
        /// bouton dans la liste, masque l'objet physique, met à jour sélection et tri.
        /// Virtual : l'inventaire MONDE le surcharge (relay réseau partagé).
        /// </summary>
        public virtual void _AddItem(M2922_InventoryItem item)
        {
            if (item == null) return;
            if (ButtonPrefab == null || ButtonParent == null)
            {
                this.Error("[Inventory] ButtonPrefab / ButtonParent non assignés, item ignoré.");
                return;
            }

            // Limite de poids (MaxWeight = -1 → illimité).
            if (!_CanStoreWeight(item))
            {
                this.Warning($"[Inventory] Poids max atteint ({_GetTotalWeight()}/{MaxWeight}) : '{item.ItemName}' non rangé.");
                return;
            }

            Networking.SetOwner(_localPlayer, item.gameObject);

            int stackIndex = _FindStackIndexFor(item);
            if (stackIndex >= 0)
            {
                DataDictionary stackEntry = ItemList[stackIndex].DataDictionary;
                DataList stack = stackEntry[ID_STACK].DataList;
                stack.Add(item);

                M2922_InventoryItem rep = (M2922_InventoryItem)stackEntry[ID_ITEM].Reference;
                if (rep != null) rep.StoredTimestamp = Time.realtimeSinceStartup;

                _RefreshButtonCount(stackEntry);
                _SelectItem(stackEntry);
            }
            else
            {
                DataList stack = new DataList();
                stack.Add(item);

                GameObject buttonObj = Instantiate(ButtonPrefab, ButtonParent);
                buttonObj.name = $"{item.name} Button";

                DataDictionary itemDictionary = new DataDictionary();
                itemDictionary[ID_BUTTON] = buttonObj;
                itemDictionary[ID_ITEM] = item;
                itemDictionary[ID_STACK] = stack;
                ItemList.Add(itemDictionary);

                M2922_InventoryButtonUI button = buttonObj.GetComponent<M2922_InventoryButtonUI>();
                if (button != null)
                    button._Init(this, itemDictionary);

                _SelectItem(itemDictionary);
            }

            item._Hide();
            _SortList();
            _RefreshWeightText();

            if (Inserter != null)
                Inserter._Highlight(false);

            this.VerboseLog($"[Inventory] Item rangé : {item.ItemName} (total : {_ItemCount()})");
        }

        /// <summary>Affiche les détails d'un item dans le panneau (icône, nom, description).</summary>
        public void _SelectItem(DataDictionary dataItem)
        {
            _selectedItem = dataItem;
            M2922_InventoryItem item = (M2922_InventoryItem)dataItem[ID_ITEM].Reference;

            if (ItemIcon != null)
            {
                ItemIcon.sprite = item.Icon;
                ItemIcon.color = Color.white;
            }
            if (ItemName != null) ItemName.text = item.ItemName;
            if (ItemDescription != null) ItemDescription.text = item.ItemDescription;
            if (SpawnButton != null) SpawnButton.interactable = true;
        }

        /// <summary>Fait réapparaître l'item sélectionné au SpawnPoint.
        /// Virtual : l'inventaire MONDE le surcharge (relay réseau partagé).</summary>
        public virtual void _SpawnItem()
        {
            if (_selectedItem == null) return;
            int index = ItemList.IndexOf(_selectedItem);
            if (index == -1) return;

            DataDictionary entry = ItemList[index].DataDictionary;
            DataList stack = entry[ID_STACK].DataList;
            if (stack == null || stack.Count == 0) return;

            // LIFO : sort le dernier item rangé du stack.
            M2922_InventoryItem item = (M2922_InventoryItem)stack[stack.Count - 1].Reference;
            _RemoveItemFromStack(index, item);

            Transform spawn = SpawnPoint != null ? SpawnPoint : transform;
            item._Spawn(spawn);
        }

        /// <summary>Retire un item de la liste et met à jour le panneau (sélection voisine).</summary>
        public void _RemoveItem(M2922_InventoryItem item)
        {
            if (item == null) return;

            for (int i = 0; i < ItemList.Count; i++)
            {
                DataDictionary entry = ItemList[i].DataDictionary;
                DataList stack = entry[ID_STACK].DataList;
                if (stack == null) continue;

                if (_IndexOfInStack(stack, item) >= 0)
                {
                    _RemoveItemFromStack(i, item);
                    return;
                }
            }
        }

        /// <summary>Filtre les boutons selon le texte du champ de recherche.</summary>
        public void _SearchForItems()
        {
            if (SearchingField == null) return;

            string searchText = SearchingField.text.ToLowerInvariant();

            for (int i = 0; i < ButtonParent.childCount; i++)
            {
                GameObject child = ButtonParent.GetChild(i).gameObject;
                M2922_InventoryButtonUI button = child.GetComponent<M2922_InventoryButtonUI>();
                if (button == null) continue;

                M2922_InventoryItem item = (M2922_InventoryItem)button.DataItem[ID_ITEM].Reference;
                bool show = searchText == string.Empty || item.ItemName.ToLowerInvariant().Contains(searchText);
                child.SetActive(show);
            }
        }

        /// <summary>Applique la valeur du Dropdown comme méthode de tri, puis trie.</summary>
        public void _SetSorting()
        {
            if (SortingDropdown == null) return;

            Sorting = (M2922_InventorySorting)SortingDropdown.value;
            _SortList();
        }

        /// <summary>Trie les boutons (tri à bulles sur les sibling indices).</summary>
        public void _SortList()
        {
            int childCount = ButtonParent.childCount;

            for (int x = 0; x < childCount - 1; x++)
            {
                for (int y = 0; y < childCount - x - 1; y++)
                {
                    Transform child1 = ButtonParent.GetChild(y);
                    Transform child2 = ButtonParent.GetChild(y + 1);

                    M2922_InventoryButtonUI button1 = child1.GetComponent<M2922_InventoryButtonUI>();
                    M2922_InventoryButtonUI button2 = child2.GetComponent<M2922_InventoryButtonUI>();
                    if (button1 == null || button2 == null) continue;

                    M2922_InventoryItem item1 = (M2922_InventoryItem)button1.DataItem[ID_ITEM].Reference;
                    M2922_InventoryItem item2 = (M2922_InventoryItem)button2.DataItem[ID_ITEM].Reference;

                    bool swap = false;
                    switch (Sorting)
                    {
                        case M2922_InventorySorting.AZ:
                            swap = string.CompareOrdinal(item1.ItemName, item2.ItemName) > 0;
                            break;

                        case M2922_InventorySorting.Latest:
                            swap = item1.StoredTimestamp < item2.StoredTimestamp;
                            break;
                    }

                    if (swap)
                    {
                        child1.SetSiblingIndex(y + 1);
                        child2.SetSiblingIndex(y);
                    }
                }
            }
        }

        // ============================================================
        // API QUÊTES / LOGIQUE EXTERNE
        // ============================================================

        /// <summary>True si l'inventaire contient au moins un item de ce nom.</summary>
        public bool _HasItem(string itemName)
        {
            return _CountItemByName(itemName) > 0;
        }

        /// <summary>Nombre total d'items actuellement rangés (tous stacks confondus).</summary>
        public int _ItemCount()
        {
            int total = 0;
            for (int i = 0; i < ItemList.Count; i++)
            {
                DataList stack = ItemList[i].DataDictionary[ID_STACK].DataList;
                if (stack != null) total += stack.Count;
            }
            return total;
        }

        /// <summary>Poids total des items actuellement rangés (count × poids).</summary>
        public int _GetTotalWeight()
        {
            int total = 0;
            for (int i = 0; i < ItemList.Count; i++)
            {
                DataDictionary entry = ItemList[i].DataDictionary;
                M2922_InventoryItem item = (M2922_InventoryItem)entry[ID_ITEM].Reference;
                DataList stack = entry[ID_STACK].DataList;
                int count = stack != null ? stack.Count : 1;
                if (item != null) total += item.Weight * count;
            }
            return total;
        }

        /// <summary>Nombre total d'items de ce nom, tous stacks confondus.</summary>
        public int _CountItemByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return 0;
            int total = 0;
            for (int i = 0; i < ItemList.Count; i++)
            {
                DataDictionary entry = ItemList[i].DataDictionary;
                M2922_InventoryItem item = (M2922_InventoryItem)entry[ID_ITEM].Reference;
                if (item == null || item.ItemName != itemName) continue;
                DataList stack = entry[ID_STACK].DataList;
                if (stack != null) total += stack.Count;
            }
            return total;
        }

        /// <summary>Consomme un item de ce nom (retire un exemplaire du premier stack trouvé).</summary>
        public bool _ConsumeItemByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return false;

            for (int i = 0; i < ItemList.Count; i++)
            {
                DataDictionary entry = ItemList[i].DataDictionary;
                M2922_InventoryItem item = (M2922_InventoryItem)entry[ID_ITEM].Reference;
                if (item == null || item.ItemName != itemName) continue;

                DataList stack = entry[ID_STACK].DataList;
                if (stack == null || stack.Count == 0) continue;

                M2922_InventoryItem toRemove = (M2922_InventoryItem)stack[stack.Count - 1].Reference;
                _RemoveItemFromStack(i, toRemove);
                return true;
            }
            return false;
        }

        // ============================================================
        // STACK HELPERS (utilisés aussi par M2922_WorldInventory)
        // ============================================================

        /// <summary>Index du stack qui peut accueillir item (-1 si aucun).</summary>
        protected int _FindStackIndexFor(M2922_InventoryItem item)
        {
            if (item == null) return -1;

            for (int i = 0; i < ItemList.Count; i++)
            {
                DataDictionary entry = ItemList[i].DataDictionary;
                M2922_InventoryItem rep = (M2922_InventoryItem)entry[ID_ITEM].Reference;
                if (rep == null) continue;
                if (!item._CanStackWith(rep)) continue;

                DataList stack = entry[ID_STACK].DataList;
                if (stack == null) continue;
                if (item._IsStackFull(stack.Count)) continue;

                return i;
            }
            return -1;
        }

        /// <summary>Index de item dans le stack (-1 si absent).</summary>
        protected int _IndexOfInStack(DataList stack, M2922_InventoryItem item)
        {
            if (stack == null || item == null) return -1;
            for (int i = 0; i < stack.Count; i++)
            {
                M2922_InventoryItem it = (M2922_InventoryItem)stack[i].Reference;
                if (it == item) return i;
            }
            return -1;
        }

        /// <summary>Nombre d'items d'un stack (0 si entrée invalide).</summary>
        protected int _GetStackCount(DataDictionary entry)
        {
            if (entry == null) return 0;
            DataList stack = entry[ID_STACK].DataList;
            return stack != null ? stack.Count : 0;
        }

        /// <summary>Met à jour le compteur affiché sur le bouton du stack.</summary>
        protected void _RefreshButtonCount(DataDictionary entry)
        {
            if (entry == null) return;
            GameObject buttonObj = (GameObject)entry[ID_BUTTON].Reference;
            if (buttonObj == null) return;
            M2922_InventoryButtonUI button = buttonObj.GetComponent<M2922_InventoryButtonUI>();
            if (button != null) button._RefreshCount(_GetStackCount(entry));
        }

        /// <summary>Retire un item de son stack ; détruit le stack s'il devient vide.</summary>
        protected void _RemoveItemFromStack(int stackIndex, M2922_InventoryItem item)
        {
            if (stackIndex < 0 || stackIndex >= ItemList.Count) return;

            bool wasSelected = _selectedItem != null && ItemList.IndexOf(_selectedItem) == stackIndex;

            DataDictionary entry = ItemList[stackIndex].DataDictionary;
            DataList stack = entry[ID_STACK].DataList;
            if (stack == null) return;

            int idx = _IndexOfInStack(stack, item);
            if (idx == -1) return;
            stack.RemoveAt(idx);

            M2922_InventoryItem rep = (M2922_InventoryItem)entry[ID_ITEM].Reference;
            if (rep == item && stack.Count > 0)
                entry[ID_ITEM] = (M2922_InventoryItem)stack[0].Reference;

            if (stack.Count == 0)
            {
                GameObject button = (GameObject)entry[ID_BUTTON].Reference;
                if (button != null) Destroy(button);

                ItemList.RemoveAt(stackIndex);

                if (ItemList.Count == 0)
                {
                    ClearMenu();
                }
                else
                {
                    int clamped = Mathf.Clamp(stackIndex, 0, ItemList.Count - 1);
                    _SelectItem(ItemList[clamped].DataDictionary);
                }
            }
            else
            {
                _RefreshButtonCount(entry);
                if (wasSelected) _SelectItem(entry);
            }

            _RefreshWeightText();
        }

        /// <summary>True si l'item peut être rangé (limite de poids). MaxWeight = -1 → illimité.</summary>
        public bool _CanStoreWeight(M2922_InventoryItem item)
        {
            if (item == null) return false;
            if (MaxWeight < 0) return true;
            return _GetTotalWeight() + item.Weight <= MaxWeight;
        }

        /// <summary>Met à jour l'affichage optionnel du poids (ex : '12 / 50').</summary>
        public void _RefreshWeightText()
        {
            if (WeightText == null) return;

            string limit = MaxWeight < 0 ? "∞" : MaxWeight.ToString();
            WeightText.text = $"{_GetTotalWeight()} / {limit}";
        }

        /// <summary>Vide le panneau de détail (icône, nom, description, bouton spawn).</summary>
        protected void ClearMenu()
        {
            if (ItemIcon != null)
            {
                ItemIcon.sprite = null;
                ItemIcon.color = Color.clear;
            }
            if (ItemName != null) ItemName.text = string.Empty;
            if (ItemDescription != null) ItemDescription.text = string.Empty;
            if (SpawnButton != null) SpawnButton.interactable = false;

            _RefreshWeightText();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Items", _ItemCount().ToString()),
                new M2922_GizmoDisplayInfo("Poids", MaxWeight < 0 ? $"{_GetTotalWeight()} / ∞" : $"{_GetTotalWeight()} / {MaxWeight}"),
                new M2922_GizmoDisplayInfo("Tri", Sorting.ToString()),
            };
        }
#endif
    }
}
