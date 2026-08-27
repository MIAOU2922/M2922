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
    /// ⚠ SYNC : mode MANUAL — AUCUN envoi réseau par tick (maps 500-5000 items) :
    /// Active / StoredInWorld ne partent QUE sur RequestSerialization()
    /// (déjà appelé par _Spawn / _Hide / _SetWorldStored).
    ///
    /// ⚠ RÈGLE SDK : un GameObject ne peut PAS porter à la fois un VRCObjectSync
    /// et un UdonBehaviour MANUAL (erreur "Object Sync cannot share an object
    /// with a manually synchronized Udon Behaviour" au build). L'encapsulation
    /// est donc OBLIGATOIRE : ce script sur la RACINE, VRCObjectSync sur le
    /// pickup ENFANT (jamais sur le même GameObject).
    ///
    /// HIÉRARCHIE REQUISE (encapsulation d'un pickup existant) :
    ///   Root   → M2922_InventoryItemSynced (ce script, TOUJOURS actif)
    ///     └─ Pickup → VRCPickup + VRCObjectSync + M2922_InventoryProxy + scripts du prop
    ///         └─ Contenu (mesh, colliders, renderers, particles, …)
    ///
    /// Le script vit sur la RACINE et désactive/réactive le sous-arbre du
    /// VRCPickup : la sync Manual continue de recevoir OnDeserialization
    /// (un UdonBehaviour sur un GameObject désactivé ne reçoit plus rien).
    /// ⚠ Le setup "À PLAT" (script sur le même GameObject que le pickup) n'est
    /// PLUS supporté : _Init() logge une Error et désactive le comportement.
    ///
    /// Comportement réseau :
    ///   - [UdonSynced] Active : état visible/masqué répliqué à tous.
    ///   - Si le propriétaire quitte avec l'objet rangé, le Master le fait
    ///     réapparaître (anti-perte d'objet).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item Synced")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_InventoryItemSynced : M2922_InventoryItem
    {
        [Header("=== DONNÉES SYNCHRONISÉES ===")]
        [UdonSynced] public bool Active;
        [Tooltip("True si l'item est rangé dans un inventaire MONDE (anti-respawn automatique).")]
        [UdonSynced] public bool StoredInWorld;

        private VRCObjectSync _objectSync;
        private bool _initialized;

        public override void _Init()
        {
            base._Init();
            if (Pickup == null) return;

            // Encapsulation OBLIGATOIRE en Manual : ce script sur la RACINE,
            // le VRCPickup sur un enfant (jamais le même GameObject).
            if (Pickup.gameObject == gameObject)
            {
                this.Error("[ItemSynced] Setup À PLAT non supporté : placez ce script sur la RACINE et le VRCPickup sur un enfant.");
                return;
            }

            _objectSync = Pickup.GetComponent<VRCObjectSync>();
            if (_objectSync == null)
                this.Warning($"[ItemSynced] Aucun VRCObjectSync trouvé sur '{Pickup.name}' !");

            // ⚠ MANUAL : le SDK refuse VRCObjectSync + UdonBehaviour MANUAL sur
            // le MÊME GameObject. Le VRCObjectSync doit être sur le pickup
            // ENFANT, jamais sur la racine de ce script.
            if (GetComponent<VRCObjectSync>() != null)
                this.Error("[ItemSynced] VRCObjectSync sur le MÊME GameObject que ce script Manual → erreur de build ! Déplacez-le sur le pickup enfant.");

            _initialized = true;
        }

        private void _SetVisualActive(bool active)
        {
            // Désactive/réactive tout le sous-arbre du pickup (scripts,
            // animators, colliders, renderers, particles…).
            Pickup.gameObject.SetActive(active);
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
            _SpawnAt(point.position, point.rotation);
        }

        public override void _SpawnAt(Vector3 position, Quaternion rotation)
        {
            // Position + activation + kinematic + délai de libération (base).
            base._SpawnAt(position, rotation);

            _SetVisualActive(true);
            Active = true;
            if (_objectSync != null)
            {
                _objectSync.SetKinematic(true);
                _objectSync.FlagDiscontinuity();
            }
            RequestSerialization();
        }

        public override void _ReleaseKinematicAfterSpawn()
        {
            if (_startsKinematic) return;
            // VRCObjectSync propage l'état kinematic aux autres clients.
            if (_objectSync != null) _objectSync.SetKinematic(false);
            if (_rigidbody != null) _rigidbody.isKinematic = false;
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
            // Pool : marqué comme « rangé » pour éviter l'auto-respawn à la
            // déconnexion du propriétaire, puis masqué. Les scripts enfants du
            // sous-arbre pickup initialisent leur Start au premier spawn
            // (première activation du GameObject) — pas de pulse d'init.
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
