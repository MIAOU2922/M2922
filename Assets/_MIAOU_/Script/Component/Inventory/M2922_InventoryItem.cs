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
        [Tooltip("Clé unique de l'item. Utilisée par les inventaires MONDE pour retrouver l'item.")]
        public string Key = "";
        [Tooltip("TRUE (défaut) = clé d'instance générée au runtime (chaîne d'indices de siblings, unique par objet et identique sur tous les clients).\n" +
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
            base.Start();
            _Init();
        }

        /// <summary>
        /// S'enregistre auprès du M2922_Manager dès qu'il est disponible.
        /// Appelé par M2922_Base au Start OU via son retry différé si le
        /// Manager n'était pas encore prêt — les inventaires MONDE résolvent
        /// ensuite les clés (Key) sans aucun "tracking" manuel.
        /// </summary>
        protected override void OnManagerReady()
        {
            // Les préfabs identiques partagent la MÊME clé serialisée : on régénère
            // une clé d'instance DÉTERMINISTE (même valeur sur tous les clients)
            // pour que les inventaires MONDE retrouvent le bon objet.
            if (UseInstanceKey)
                Key = _BuildPathKey();

            Manager.RegisterInventoryItem(this);
        }

        /// <summary>
        /// Clé d'instance déterministe : chaîne des indices de siblings depuis la
        /// racine de la scène jusqu'à cet objet (ex : "i0/3/1").
        /// - Unique par instance (deux objets ne peuvent pas occuper la même position).
        /// - Identique sur tous les clients (même scène chargée).
        /// - Capturée au Start (stable même si l'objet est re-parenté ensuite).
        /// - Sans virgule (séparateur des StoredKeys).
        /// </summary>
        private string _BuildPathKey()
        {
            string path = transform.GetSiblingIndex().ToString();
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.GetSiblingIndex() + "/" + path;
                parent = parent.parent;
            }
            return "i" + path;
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
        }

        /// <summary>Initialisation (appelée au Start). À surcharger par la version synced.</summary>
        public virtual void _Init()
        {
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
            JustSpawned = true;
            Pickup.transform.SetPositionAndRotation(point.position, point.rotation);
            _pickupObj.SetActive(true);
            if (_rigidbody != null) _rigidbody.isKinematic = true;
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

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Clé unique auto-générée pour les inventaires MONDE.
            if (string.IsNullOrEmpty(Key))
                Key = System.Guid.NewGuid().ToString("N");
        }
#endif
    }
}
