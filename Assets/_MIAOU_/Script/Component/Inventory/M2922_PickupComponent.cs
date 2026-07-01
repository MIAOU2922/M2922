using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Inventory
{
    /// <summary>
    /// Interaction de ramassage / drop d'objet.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_PickupComponent : M2922_Base
    {
        [Header("=== PICKUP ===")]
        [SerializeField] private float _pickupRange = 3f;
        [SerializeField] private KeyCode _pickupKey = KeyCode.E;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_InventoryComponent _inventory;

        private GameObject _lookTarget;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_inventory == null) _inventory = GetComponent<M2922_InventoryComponent>();
        }

        protected override void Update()
        {
            base.Update();

            // Détection du regard
            RaycastHit hit;
            Vector3 eyePos = Networking.LocalPlayer != null
                ? Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position
                : transform.position;

            if (UnityEngine.Physics.Raycast(eyePos, transform.forward, out hit, _pickupRange))
            {
                M2922_ItemComponent item = hit.collider.GetComponent<M2922_ItemComponent>();
                if (item != null)
                    _lookTarget = hit.collider.gameObject;
                else
                    _lookTarget = null;
            }
            else
            {
                _lookTarget = null;
            }

            // Pickup
            if (Input.GetKeyDown(_pickupKey) && _lookTarget != null && _inventory != null)
            {
                if (_inventory.AddItem(_lookTarget))
                    _lookTarget.SetActive(false);
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Range", $"{_pickupRange:F1}m"),
                new M2922_GizmoDisplayInfo("Key", _pickupKey.ToString()),
            };
        }
#endif
    }
}
