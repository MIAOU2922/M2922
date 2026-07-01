using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Ammo
{
    /// <summary>
    /// Définit un type de munition compatible avec certaines armes/magazines.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_AmmoTypeComponent : M2922_Base
    {
        [Header("=== AMMO TYPE ===")]
        [SerializeField] private string _ammoName = "5.56mm";
        [SerializeField] private float _baseDamageMultiplier = 1f;
        [SerializeField] private float _armorPenetration = 0f;

        public string AmmoName => _ammoName;
        public float BaseDamageMultiplier => _baseDamageMultiplier;
        public float ArmorPenetration => _armorPenetration;

        /// <summary>Vérifie la compatibilité entre deux types de munition.</summary>
        public bool IsCompatible(M2922_AmmoTypeComponent other)
        {
            if (other == null) return true; // pas de restriction
            return _ammoName == other._ammoName;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Name", _ammoName, Color.cyan),
                new M2922_GizmoDisplayInfo("Damage x", $"{_baseDamageMultiplier:F1}"),
                new M2922_GizmoDisplayInfo("Penetration", $"{_armorPenetration:F1}"),
            };
        }
#endif
    }
}
