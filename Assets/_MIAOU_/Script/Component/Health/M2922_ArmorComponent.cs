using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Réduction de dégâts : valeur fixe (flat) et/ou pourcentage.
    /// </summary>
    [AddComponentMenu("M2922/Health/Armor Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ArmorComponent : M2922_Base
    {
        [Header("=== ARMOR ===")]
        [SerializeField] private float _flatReduction = 0f;
        [SerializeField] private float _percentReduction = 0f; // 0.0 à 1.0

        public float FlatReduction => _flatReduction;
        public float PercentReduction => _percentReduction;

        /// <summary>Applique la réduction d'armure et retourne les dégâts réduits.</summary>
        public float ReduceDamage(float incomingDamage)
        {
            float reduced = incomingDamage - _flatReduction;
            reduced *= (1f - _percentReduction);
            return Mathf.Max(0f, reduced);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Flat Reduction", $"{_flatReduction:F1}"),
                new M2922_GizmoDisplayInfo("% Reduction", $"{_percentReduction * 100f:F0}%"),
            };
        }
#endif
    }
}
