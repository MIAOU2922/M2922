using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Item rangeable dans l'inventaire M2922 — version LOCALE (non synchronisée).
    ///
    /// SETUP : mettre ce script sur le même GameObject que le VRCPickup de l'objet
    /// (ou assigner Pickup manuellement). Remplir Icon / ItemName / ItemDescription.
    ///
    /// PERF : 100% événementiel (M2922_Base, aucun Update par frame).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_InventoryItem : M2922_Base
    {
        [Header("=== IDENTIFIANT ===")]
        [Tooltip("Clé unique de l'item (GUID 32 hex). Utilisée par les inventaires MONDE pour retrouver l'item.")]
        public string Key = "";
        [Tooltip("TRUE (défaut) = clé d'instance GUID générée au Start (hash déterministe des indices de siblings, unique par objet et identique sur tous les clients).\n" +
                 "FALSE = utilise la clé serialisée telle quelle (chaque instance de préfab doit alors avoir SA propre clé, sinon doublons).")]
        public bool UseInstanceKey = true;

        [Header("=== DONNÉES ITEM ===")]
        [Tooltip("Icône affichée dans le menu et dans les boutons de la liste.")]
        public Sprite Icon;
        [Tooltip("Nom de l'item (utilisé pour le tri, la recherche et les requêtes).")]
        public string ItemName;
        [Tooltip("Description affichée dans le panneau de détail.")]
        public string ItemDescription;

        [Header("=== POIDS ===")]
        [Tooltip("Poids de l'item (int). Compte dans la limite MaxWeight des inventaires.")]
        public int Weight = 1;

        [Header("=== ÉTAT INITIAL ===")]
        [Tooltip("TRUE = l'item démarre masqué (pool spawnable par le menu). FALSE = visible au démarrage.")]
        [SerializeField] private bool _startHidden = false;

        [Header("=== STACK ===")]
        [Tooltip("Identifiant de stack : deux items avec le même StackId (et NotStackable=false) se stackent.")]
        public string StackId = "";
        [Tooltip("TRUE = cet item ne se stacke jamais (objet unique/nommé).")]
        public bool NotStackable = false;
        [Tooltip("Taille maximale d'un stack. -1 = illimité.")]
        public int MaxStackSize = -1;

        [Header("=== RÉFÉRENCES ===")]
        [Tooltip("VRCPickup de l'objet (auto-détecté si vide).")]
        public VRCPickup Pickup;

        [Header("=== RUNTIME (lecture seule) ===")]
        [Tooltip("Timestamp du dernier rangement (tri 'Latest').")]
        public float StoredTimestamp;
        [Tooltip("True si l'objet vient d'être spawné (premier ramassage spécial).")]
        public bool JustSpawned;

        protected GameObject _pickupObj;
        protected Rigidbody _rigidbody;
        protected bool _startsKinematic;

        protected override void Start()
        {
            // Clé déterministe AVANT l'enregistrement auprès du Manager
            // (base.Start → OnManagerReady → RegisterInventoryItem). Sinon l'item
            // serait enregistré sous sa clé serialisée (partagée par les instances
            // d'un même préfab).
            if (UseInstanceKey)
                Key = _BuildPathKey();

            base.Start();
            _Init();

            // Pool : démarre masqué, comme s'il était rangé dans un inventaire.
            if (_startHidden && Pickup != null)
                _OnStartHidden();
        }

        /// <summary>
        /// S'enregistre auprès du M2922_Manager dès qu'il est disponible.
        /// Appelé par M2922_Base au Start OU via son retry différé si le
        /// Manager n'était pas encore prêt — les inventaires MONDE résolvent
        /// ensuite les clés (Key) sans aucun "tracking" manuel.
        /// </summary>
        protected override void OnManagerReady()
        {
            // La clé d'instance est déjà calculée au Start (_Init). Ce fallback
            // couvre uniquement le cas où le Start n'a pas pu la générer (clé vide).
            if (UseInstanceKey && string.IsNullOrEmpty(Key))
                Key = _BuildPathKey();

            if (Manager != null)
                Manager.RegisterInventoryItem(this);
        }

        /// <summary>
        /// Clé d'instance déterministe au format GUID (32 hex sans tirets, ex :
        /// "fe87c0e1cc204ed48ad3b37840f39efc").
        /// Hash FNV-1a des indices de siblings (chemin racine → objet) :
        /// - Unique par instance (deux objets ne peuvent pas occuper la même position).
        /// - Identique sur tous les clients (même scène chargée).
        /// - Capturée au Start (stable même si l'objet est re-parenté ensuite).
        /// - Sans virgule (séparateur des StoredKeys).
        /// </summary>
        private string _BuildPathKey()
        {
            // 4 hash FNV-1a (seeds distincts) des indices de siblings → 128 bits
            // déterministes formatés en 32 hex. Même valeur sur tous les clients.
            int h0 = 1732584193;
            int h1 = 305419896;
            int h2 = 1518500249;
            int h3 = 1859775393;

            int idx = transform.GetSiblingIndex();
            h0 = _Mix(h0, idx);
            h1 = _Mix(h1, idx);
            h2 = _Mix(h2, idx);
            h3 = _Mix(h3, idx);

            Transform parent = transform.parent;
            while (parent != null)
            {
                int p = parent.GetSiblingIndex();
                h0 = _Mix(h0, p);
                h1 = _Mix(h1, p);
                h2 = _Mix(h2, p);
                h3 = _Mix(h3, p);
                parent = parent.parent;
            }

            return _Hex8(h0) + _Hex8(h1) + _Hex8(h2) + _Hex8(h3);
        }

        /// <summary>Mélange déterministe (FNV-1a) : wrap int32, aucun aléa.</summary>
        private int _Mix(int hash, int value)
        {
            return (hash ^ value) * 16777619;
        }

        /// <summary>Formate un int en 8 caractères hexadécimaux (sans aléa).</summary>
        private string _Hex8(int value)
        {
            string result = "";
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                int nibble = (value >> shift) & 0xF;
                if (nibble < 10)
                    result += nibble.ToString();
                else if (nibble == 10)
                    result += "a";
                else if (nibble == 11)
                    result += "b";
                else if (nibble == 12)
                    result += "c";
                else if (nibble == 13)
                    result += "d";
                else if (nibble == 14)
                    result += "e";
                else
                    result += "f";
            }
            return result;
        }

        /// <summary>
        /// Régénère la clé d'instance (appelé par le Manager en cas de doublon).
        /// </summary>
        public void _RegenerateKey()
        {
            Key = _BuildPathKey();
        }

        protected override void AutoDetectReferences()
        {
            if (Pickup == null) Pickup = GetComponent<VRCPickup>();
            if (Pickup == null) Pickup = GetComponentInParent<VRCPickup>();
            // Hiérarchie encapsulée : le pickup est sur un ENFANT de la racine.
            if (Pickup == null) Pickup = GetComponentInChildren<VRCPickup>();
        }

        /// <summary>Initialisation (appelée au Start). À surcharger par la version synced.</summary>
        public virtual void _Init()
        {
            // Clé d'instance générée dès le Start : deux instances du même préfab
            // partagent la clé serialisée (GUID généré par OnValidate). On la
            // remplace immédiatement par une clé de chemin déterministe, sans
            // dépendre du timing d'enregistrement auprès du Manager.
            if (UseInstanceKey)
                Key = _BuildPathKey();

            if (Pickup == null)
            {
                this.Error("[Item] Aucun VRCPickup trouvé !");
                return;
            }

            _rigidbody = Pickup.GetComponent<Rigidbody>();
            _startsKinematic = _rigidbody != null && _rigidbody.isKinematic;
            _pickupObj = Pickup.gameObject;
        }

        /// <summary>Fait réapparaître l'objet au point donné (kinematic, prêt à être ramassé).</summary>
        public virtual void _Spawn(Transform point)
        {
            _SpawnAt(point.position, point.rotation);
        }

        /// <summary>Fait réapparaître l'objet à une position/rotation données (kinematic, prêt à être ramassé).</summary>
        public virtual void _SpawnAt(Vector3 position, Quaternion rotation)
        {
            JustSpawned = true;
            Pickup.transform.SetPositionAndRotation(position, rotation);
            _pickupObj.SetActive(true);
            if (_rigidbody != null) _rigidbody.isKinematic = true;

            // Item NON kinematic par défaut : libéré de l'état kinematic 5 s
            // après le spawn (retombe physiquement), quoi qu'il se passe.
            if (!_startsKinematic)
                SendCustomEventDelayedSeconds("_ReleaseKinematicAfterSpawn", 5f);
        }

        /// <summary>
        /// Libère l'état kinematic 5 s après un spawn, SI l'item n'est pas
        /// kinematic par défaut (_startsKinematic capturé au Start). No-op
        /// pour les items kinematic par défaut. Virtual : la version synced
        /// passe par VRCObjectSync pour propager l'état aux autres clients.
        /// </summary>
        public virtual void _ReleaseKinematicAfterSpawn()
        {
            if (_startsKinematic) return;
            if (_rigidbody != null) _rigidbody.isKinematic = false;
        }

        /// <summary>Range l'objet : drop le pickup, désactive le GameObject et fige la physique.</summary>
        public virtual void _Hide()
        {
            Pickup.Drop();
            _pickupObj.SetActive(false);
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            StoredTimestamp = Time.realtimeSinceStartup;
        }

        /// <summary>Appelé au premier ramassage après un spawn : restaure l'état kinematic d'origine.</summary>
        public virtual void _RunFirstPickupAfterSpawn()
        {
            if (_rigidbody != null) _rigidbody.isKinematic = _startsKinematic;
        }

        /// <summary>
        /// Appelé au Start si l'item démarre masqué (pool) : masque l'objet.
        /// ⚠ Les scripts enfants du sous-arbre pickup (armes, pools de
        /// projectiles...) exécuteront leur Start à la PREMIÈRE ACTIVATION,
        /// c'est-à-dire au premier spawn : Unity déclenche Start quand le
        /// GameObject devient actif pour la première fois — aucun pré-spawn
        /// ni pulse d'init n'est nécessaire.
        /// Surchargé par la version synced (marquage « rangé » en plus).
        /// </summary>
        public virtual void _OnStartHidden()
        {
            _Hide();
        }

        /// <summary>True si l'item est synchronisé (REQUIS pour un inventaire MONDE partagé).</summary>
        public virtual bool _IsNetworked()
        {
            return false;
        }

        /// <summary>
        /// Marque l'item comme rangé dans un inventaire MONDE.
        /// No-op pour les items locaux — surchargé par la version synced.
        /// </summary>
        public virtual void _SetWorldStored(bool stored)
        {
        }

        /// <summary>True si l'item est actuellement rangé dans un inventaire MONDE.</summary>
        public virtual bool _IsWorldStored()
        {
            return false;
        }

        /// <summary>True si l'item est actuellement visible/spawné dans le monde.</summary>
        public virtual bool _IsActive()
        {
            return _pickupObj != null && _pickupObj.activeSelf;
        }

        /// <summary>True si deux items peuvent être empilés (même StackId, non-NotStackable).</summary>
        public virtual bool _CanStackWith(M2922_InventoryItem other)
        {
            if (other == null) return false;
            if (NotStackable || other.NotStackable) return false;
            if (string.IsNullOrEmpty(StackId) || StackId != other.StackId) return false;
            return true;
        }

        /// <summary>True si un stack de currentCount items est plein (limite MaxStackSize). -1 = illimité.</summary>
        public virtual bool _IsStackFull(int currentCount)
        {
            if (MaxStackSize < 1) return false;
            return currentCount >= MaxStackSize;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Clé unique auto-générée pour les inventaires MONDE.
            if (string.IsNullOrEmpty(Key))
                Key = System.Guid.NewGuid().ToString("N");

            // Identifiant de stack auto-généré par préfab (surclassable manuellement).
            if (string.IsNullOrEmpty(StackId))
                StackId = System.Guid.NewGuid().ToString("N");
        }
#endif
    }
}
