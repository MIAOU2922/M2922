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
    /// Udon Behaviour" au build). En Continuous, ce script peut rester sur la
    /// RACINE avec le VRCPickup + VRCObjectSync (comme sur les armes M2922).
    /// Les bools [UdonSynced] (Active / StoredInWorld) font ~2 octets : largement
    /// dans la limite Continuous (~200 octets).
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

        public override void _Init()
        {
            base._Init();
            _objectSync = Pickup.GetComponent<VRCObjectSync>();
            if (_objectSync == null)
                this.Warning($"[ItemSynced] Aucun VRCObjectSync trouvé sur '{Pickup.name}' !");
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
                _objectSync.Respawn();
            }
        }

        public override void OnDeserialization()
        {
            Pickup.gameObject.SetActive(Active);
            if (!Active) Pickup.Drop();
        }

        public override void _Spawn(Transform point)
        {
            base._Spawn(point);

            Active = true;
            _objectSync.SetKinematic(true);
            _objectSync.FlagDiscontinuity();
            RequestSerialization();
        }

        public override void _Hide()
        {
            base._Hide();

            Active = false;
            RequestSerialization();
        }

        public override void _RunFirstPickupAfterSpawn()
        {
            base._RunFirstPickupAfterSpawn();
            _objectSync.SetKinematic(_startsKinematic);
        }

        public override bool _IsNetworked()
        {
            return true;
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
