using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Item d'inventaire SYNCHRONISÉ : visible/masqué pour tous les joueurs.
    ///
    /// ⚠ SYNC : mode CONTINUOUS (pas Manual) — le SDK interdit de partager un
    /// GameObject entre un VRCObjectSync et un UdonBehaviour MANUAL
    /// (erreur "Object Sync cannot share an object with a manually synchronized
    /// Udon Behaviour" au build).
    ///
    /// HIÉRARCHIE RECOMMANDÉE (encapsulation d'un pickup existant) :
    ///   Root   → M2922_InventoryItemSynced (ce script, TOUJOURS actif)
    ///     └─ Pickup → VRCPickup + VRCObjectSync + M2922_InventoryProxy + scripts du prop
    ///         └─ Contenu (mesh, colliders, renderers, particles, …)
    ///
    /// Le script vit sur la RACINE et désactive/réactive le sous-arbre du
    /// VRCPickup : la sync Continuous continue de recevoir OnDeserialization
    /// (un UdonBehaviour sur un GameObject désactivé ne reçoit plus rien).
    /// Si le script est posé À PLAT sur le pickup (ancien setup), un fallback
    /// cache uniquement renderers/colliders/particles.
    ///
    /// Comportement réseau :
    ///   - [UdonSynced] Active : état visible/masqué répliqué à tous.
    ///   - Si le propriétaire quitte avec l'objet rangé, le Master le fait
    ///     réapparaître (anti-perte d'objet).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item Synced")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class M2922_InventoryItemSynced : M2922_InventoryItem
    {
        [Header("=== DONNÉES SYNCHRONISÉES ===")]
        [UdonSynced] public bool Active;
        [Tooltip("True si l'item est rangé dans un inventaire MONDE (anti-respawn automatique).")]
        [UdonSynced] public bool StoredInWorld;

        private VRCObjectSync _objectSync;
        private bool _initialized;
        private bool _isOnPickupRoot;

        // Fallback (setup À PLAT : item sur le même GameObject que le pickup).
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private ParticleSystem[] _particles;

        public override void _Init()
        {
            base._Init();
            if (Pickup == null) return;

            _objectSync = Pickup.GetComponent<VRCObjectSync>();
            if (_objectSync == null)
                this.Warning($"[ItemSynced] Aucun VRCObjectSync trouvé sur '{Pickup.name}' !");

            // Hiérarchie encapsulée : on peut désactiver le sous-arbre du pickup
            // sans couper la sync de CE script (il reste sur la racine active).
            // Si le script est encore posé à plat sur le pickup, fallback composants.
            _isOnPickupRoot = Pickup.gameObject == gameObject;

            if (_isOnPickupRoot)
            {
                _renderers = Pickup.GetComponentsInChildren<Renderer>(true);
                _colliders = Pickup.GetComponentsInChildren<Collider>(true);
                _particles = Pickup.GetComponentsInChildren<ParticleSystem>(true);
            }

            _initialized = true;
        }

        private void _SetVisualActive(bool active)
        {
            if (_isOnPickupRoot)
            {
                // ⚠ Ne JAMAIS désactiver la racine d'un objet synchronisé : un
                // UdonBehaviour inactif ne reçoit plus OnDeserialization.
                if (_renderers != null)
                {
                    for (int i = 0; i < _renderers.Length; i++)
                        if (_renderers[i] != null) _renderers[i].enabled = active;
                }

                if (_colliders != null)
                {
                    for (int i = 0; i < _colliders.Length; i++)
                        if (_colliders[i] != null) _colliders[i].enabled = active;
                }

                if (!active && _particles != null)
                {
                    for (int i = 0; i < _particles.Length; i++)
                        if (_particles[i] != null) _particles[i].Stop();
                }
            }
            else
            {
                // Désactive/réactive tout le sous-arbre du pickup (scripts,
                // animators, colliders, renderers, particles…).
                Pickup.gameObject.SetActive(active);
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            // Rangé dans un inventaire MONDE : le coffre gère le cycle de vie,
            // on ne respawn PAS automatiquement (sinon l'objet réapparaîtrait
            // dans le monde à la déconnexion de son propriétaire).
            if (Active || StoredInWorld) return;

            if (player.isLocal && player.isMaster)
            {
                _Spawn(Pickup.transform);
                if (_objectSync != null) _objectSync.Respawn();
            }
        }

        public override void OnDeserialization()
        {
            if (!_initialized) _Init();
            if (!_initialized) return;

            // On ne touche PAS à l'activité du GameObject : uniquement le visuel.
            _SetVisualActive(Active);
            if (!Active) Pickup.Drop();
        }

        public override void _Spawn(Transform point)
        {
            JustSpawned = true;
            Pickup.transform.SetPositionAndRotation(point.position, point.rotation);
            _SetVisualActive(true);
            if (_rigidbody != null) _rigidbody.isKinematic = true;

            Active = true;
            if (_objectSync != null)
            {
                _objectSync.SetKinematic(true);
                _objectSync.FlagDiscontinuity();
            }
            RequestSerialization();
        }

        public override void _Hide()
        {
            Pickup.Drop();
            _SetVisualActive(false);
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }
            StoredTimestamp = Time.realtimeSinceStartup;

            Active = false;
            RequestSerialization();
        }

        public override void _RunFirstPickupAfterSpawn()
        {
            base._RunFirstPickupAfterSpawn();
            if (_objectSync != null)
                _objectSync.SetKinematic(_startsKinematic);
        }

        public override bool _IsNetworked()
        {
            return true;
        }

        public override bool _IsActive()
        {
            return Active;
        }

        public override void _OnStartHidden()
        {
            // Pool : marqué comme "rangé" pour éviter l'auto-respawn à la
            // déconnexion du propriétaire, puis masqué.
            _SetWorldStored(true);
            _Hide();
        }

        public override void _SetWorldStored(bool stored)
        {
            StoredInWorld = stored;
            RequestSerialization();
        }

        public override bool _IsWorldStored()
        {
            return StoredInWorld;
        }
    }
}
