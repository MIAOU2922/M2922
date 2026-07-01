using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Projectile
{
    public enum DamageType { Physical, Energy, Fire, Explosive, Poison }

    /// <summary>
    /// Source de dégâts : qui, quel type, combien.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DamageSource : M2922_Base
    {
        [Header("=== DAMAGE ===")]
        [SerializeField] private float _damage = 25f;
        [SerializeField] private DamageType _damageType = DamageType.Physical;

        [Header("=== OWNER ===")]
        [UdonSynced] private string _ownerName = "";
        private VRCPlayerApi _owner;

        public VRCPlayerApi Owner => _owner;
        public DamageType Type => _damageType;

        public void SetOwner(VRCPlayerApi player)
        {
            _owner = player;
            _ownerName = player != null ? player.displayName : "";
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        public void SetDamage(float amount)
        {
            _damage = amount;
        }

        public float GetDamage()
        {
            return _damage;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Damage", $"{_damage:F1}", Color.red),
                new M2922_GizmoDisplayInfo("Type", _damageType.ToString()),
            };
        }
#endif
    }
}
