using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.World
{
    /// <summary>
    /// Zone de trigger générique : entrée, sortie, stay.
    /// </summary>
    [AddComponentMenu("M2922/World/Trigger Zone")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_TriggerZone : M2922_Base
    {
        [Header("=== TRIGGER ===")]
        [SerializeField] private string _zoneTag = "Zone";
        [SerializeField] private bool _playerOnly = true;
        [SerializeField] private float _tickInterval = 0.5f;

        private int _entitiesInside = 0;
        private float _lastTick = 0f;

        public int EntitiesInside => _entitiesInside;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!player.isLocal) return;
            _entitiesInside++;
            this.Log($"Player entered zone: {_zoneTag}");
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (!player.isLocal) return;
            _entitiesInside = Mathf.Max(0, _entitiesInside - 1);
            this.Log($"Player exited zone: {_zoneTag}");
        }

        protected override void Update()
        {
            base.Update();
            if (_entitiesInside > 0 && Time.time - _lastTick > _tickInterval)
            {
                _lastTick = Time.time;
                // Logique périodique tant qu'une entité est dans la zone
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Tag", _zoneTag, Color.cyan),
                new M2922_GizmoDisplayInfo("Inside", _entitiesInside.ToString(), _entitiesInside > 0 ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("PlayerOnly", _playerOnly.ToString()),
            };
        }
#endif
    }
}
