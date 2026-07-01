using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Modifier
{
    /// <summary>
    /// Applique des modificateurs à soi-même ou à d'autres entités.
    /// </summary>
    [AddComponentMenu("M2922/Modifier/Modifier Applier")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ModifierApplier : M2922_Base
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_ModifierContainer _selfContainer;

        protected override void Start()
        {
            base.Start();
        }

        protected override void AutoDetectReferences()
        {
            if (_selfContainer == null) _selfContainer = GetComponent<M2922_ModifierContainer>();
        }

        /// <summary>Applique un modificateur additif à soi-même.</summary>
        public void ApplyToSelf(ModifierType type, float duration, float value)
            => ApplyToSelfFull(type, duration, value, ModifierMode.Additive, DamageType.None);

        /// <summary>Applique un modificateur additif à une autre entité.</summary>
        public void ApplyToTarget(GameObject target, ModifierType type, float duration, float value)
            => ApplyToTargetFull(target, type, duration, value, ModifierMode.Additive, DamageType.None);

        /// <summary>Applique un modificateur complet (mode + DamageType) à soi-même.</summary>
        public void ApplyToSelfFull(ModifierType type, float duration, float value, ModifierMode mode, DamageType damageType)
        {
            if (_selfContainer != null)
                _selfContainer.AddModifierFull(type, duration, value, mode, damageType);
        }

        /// <summary>Applique un modificateur complet (mode + DamageType) à une autre entité.</summary>
        public void ApplyToTargetFull(GameObject target, ModifierType type, float duration, float value, ModifierMode mode, DamageType damageType)
        {
            M2922_ModifierContainer container = target.GetComponent<M2922_ModifierContainer>();
            if (container != null)
                container.AddModifierFull(type, duration, value, mode, damageType);
        }

        /// <summary>Retire un modificateur de soi-même.</summary>
        public void RemoveFromSelf(ModifierType type)
        {
            if (_selfContainer != null)
                _selfContainer.RemoveModifier(type);
        }

        /// <summary>Vérifie si on a un modificateur actif.</summary>
        public bool HasModifier(ModifierType type)
        {
            return _selfContainer != null && _selfContainer.HasModifier(type);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Container", _selfContainer != null ? "Linked" : "None", _selfContainer != null ? Color.green : Color.gray),
            };
        }
#endif
    }
}
