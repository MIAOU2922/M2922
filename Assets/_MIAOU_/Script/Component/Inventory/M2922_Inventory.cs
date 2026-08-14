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

        [Header("=== COMPORTEMENT ===")]
        [Tooltip("Méthode de tri de la liste (Latest = plus récent d'abord, AZ = alphabétique).")]
        public M2922_InventorySorting Sorting = M2922_InventorySorting.Latest;
        [Tooltip("Au-delà de cette distance du système, rouvrir le menu le déplace devant le joueur au lieu de fermer.")]
        public float SpawnInsteadOfCloseDistance = 1;
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
        [Tooltip("Renderer d'un collider placé autour du système : sa visibilité sert de test de proximité.")]
        public MeshRenderer CanvasVisibleChecker;
        [Tooltip("Zone d'insertion (M2922_InventoryInserter).")]
        public M2922_InventoryInserter Inserter;

        [Header("=== UI ===")]
        [Tooltip("Racine du menu (canvas).")]
        public GameObject MenuContainer;
        [Tooltip("Champ de recherche.")]
        public InputField SearchingField;
        [Tooltip("Dropdown de tri (0 = Latest, 1 = AZ).")]
        public Dropdown SortingDropdown;
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
                float dist = Vector3.Distance(_localPlayer.GetPosition(), transform.position);
                if (dist > HideDistance)
                {
                    if (CanvasVisibleChecker == null || !CanvasVisibleChecker.isVisible)
                        _CloseMenu();
                }
            }
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
        /// Bascule l'état du menu. S'il est déjà ouvert :
        /// loin du système → le menu revient devant le joueur, près → fermeture.
        /// </summary>
        public void _ToggleMenu()
        {
            if (MenuContainer.activeSelf)
            {
                if (Vector3.Distance(_localPlayer.GetPosition(), transform.position) > SpawnInsteadOfCloseDistance)
                {
                    SpawnAtPlayer();
                }
                else
                {
                    _CloseMenu();
                }
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

            GameObject buttonObj = Instantiate(ButtonPrefab, ButtonParent);
            buttonObj.name = $"{item.name} Button";

            DataDictionary itemDictionary = new DataDictionary();
            itemDictionary[ID_BUTTON] = buttonObj;
            itemDictionary[ID_ITEM] = item;
            ItemList.Add(itemDictionary);

            M2922_InventoryButtonUI button = buttonObj.GetComponent<M2922_InventoryButtonUI>();
            if (button != null)
                button._Init(this, itemDictionary);

            item._Hide();
            _SelectItem(itemDictionary);
            _SortList();
            _RefreshWeightText();

            if (Inserter != null)
                Inserter._Highlight(false);

            this.VerboseLog($"[Inventory] Item rangé : {item.ItemName} (total : {ItemList.Count})");
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
            if (ItemList.Count == 0) return;
            if (_selectedItem == null) return;
            int index = ItemList.IndexOf(_selectedItem);
            if (index == -1) return;

            M2922_InventoryItem item = (M2922_InventoryItem)_selectedItem[ID_ITEM].Reference;
            _RemoveItem(item);

            Transform spawn = SpawnPoint != null ? SpawnPoint : transform;
            item._Spawn(spawn);
        }

        /// <summary>Retire un item de la liste et met à jour le panneau (sélection voisine).</summary>
        public void _RemoveItem(M2922_InventoryItem item)
        {
            int index = -1;
            for (int i = 0; i < ItemList.Count; i++)
            {
                M2922_InventoryItem listItem = (M2922_InventoryItem)ItemList[i].DataDictionary[ID_ITEM].Reference;
                if (listItem == item)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1) return;

            GameObject button = (GameObject)ItemList[index].DataDictionary[ID_BUTTON].Reference;
            Destroy(button);

            ItemList.RemoveAt(index);

            if (ItemList.Count == 0)
            {
                ClearMenu();
            }
            else
            {
                int clampedIndex = Mathf.Clamp(index, 0, ItemList.Count - 1);
                _SelectItem(ItemList[clampedIndex].DataDictionary);
            }

            _RefreshWeightText();
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
            for (int i = 0; i < ItemList.Count; i++)
            {
                M2922_InventoryItem item = (M2922_InventoryItem)ItemList[i].DataDictionary[ID_ITEM].Reference;
                if (item.ItemName == itemName) return true;
            }
            return false;
        }

        /// <summary>Nombre d'items actuellement rangés.</summary>
        public int _ItemCount()
        {
            return ItemList.Count;
        }

        /// <summary>Poids total des items actuellement rangés.</summary>
        public int _GetTotalWeight()
        {
            int total = 0;
            for (int i = 0; i < ItemList.Count; i++)
            {
                M2922_InventoryItem item = (M2922_InventoryItem)ItemList[i].DataDictionary[ID_ITEM].Reference;
                total += item.Weight;
            }
            return total;
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
                new M2922_GizmoDisplayInfo("Items", ItemList.Count.ToString()),
                new M2922_GizmoDisplayInfo("Poids", MaxWeight < 0 ? $"{_GetTotalWeight()} / ∞" : $"{_GetTotalWeight()} / {MaxWeight}"),
                new M2922_GizmoDisplayInfo("Tri", Sorting.ToString()),
            };
        }
#endif
    }
}
