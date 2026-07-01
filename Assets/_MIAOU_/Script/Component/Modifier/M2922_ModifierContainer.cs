using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Modifier
{
    /// <summary>
    /// Conteneur de buffs/debuffs temporaires.
    /// Stockage optimisé en arrays parallèles : (ModifierType, value, durée, timeAdded).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ModifierContainer : M2922_Base
    {
        [Header("=== MODIFIERS ===")]
        [SerializeField] private int _maxModifiers = 16;

        // Stockage arrays parallèles
        private int[]       _modifierTypes;       // (int)ModifierType
        private ModifierMode[] _modifierModes;    // Additive ou Multiplicative
        private float[]     _modifierValues;      // magnitude
        private float[]     _modifierDurations;   // secondes (-1 = infini)
        private float[]     _modifierTimesAdded;  // Time.time
        private DamageType[] _modifierDamageTypes; // pour PassiveDamage
        private int _activeCount;

        public int ActiveCount => _activeCount;
        public int MaxModifiers => _maxModifiers;

        protected override void Start()
        {
            base.Start();
            _modifierTypes      = new int[_maxModifiers];
            _modifierModes      = new ModifierMode[_maxModifiers];
            _modifierValues     = new float[_maxModifiers];
            _modifierDurations  = new float[_maxModifiers];
            _modifierTimesAdded = new float[_maxModifiers];
            _modifierDamageTypes = new DamageType[_maxModifiers];
            _activeCount = 0;
        }

        protected override void Update()
        {
            base.Update();
            CleanExpired();
        }

        /// <summary>Ajoute un modificateur (mode = Additive par défaut).</summary>
        public void AddModifier(ModifierType type, float duration, float value)
            => AddModifierFull(type, duration, value, ModifierMode.Additive, DamageType.None);

        /// <summary>Ajoute un modificateur avec mode et DamageType.</summary>
        public void AddModifierFull(ModifierType type, float duration, float value, ModifierMode mode, DamageType damageType)
        {
            if (_activeCount >= _maxModifiers)
            {
                this.Warning($"ModifierContainer full, cannot add: {type}");
                return;
            }

            int slot = _activeCount;
            _modifierTypes[slot]      = (int)type;
            _modifierModes[slot]      = mode;
            _modifierValues[slot]     = value;
            _modifierDurations[slot]  = duration;
            _modifierTimesAdded[slot] = Time.time;
            _modifierDamageTypes[slot] = damageType;
            _activeCount++;

            string modeStr = mode == ModifierMode.Multiplicative ? "x" : "+";
            this.Log($"Modifier: {type} {modeStr}{value:F1} ({duration:F1}s)");
        }

        /// <summary>Somme des valeurs ADDITIVES d'un type donné.</summary>
        public float GetAdditiveTotal(ModifierType type)
        {
            float total = 0f;
            int target = (int)type;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_modifierTypes[i] == target && _modifierModes[i] == ModifierMode.Additive)
                    total += _modifierValues[i];
            }
            return total;
        }

        /// <summary>Produit des valeurs MULTIPLICATIVES d'un type donné.</summary>
        public float GetMultiplicativeTotal(ModifierType type)
        {
            float total = 1f;
            int target = (int)type;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_modifierTypes[i] == target && _modifierModes[i] == ModifierMode.Multiplicative)
                    total *= _modifierValues[i];
            }
            return total;
        }

        /// <summary>DamageType du premier modificateur actif de ce type.</summary>
        public DamageType GetDamageType(ModifierType type)
        {
            int target = (int)type;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_modifierTypes[i] == target)
                    return _modifierDamageTypes[i];
            }
            return DamageType.None;
        }

        /// <summary>Nombre de modificateurs actifs d'un type donné.</summary>
        public int GetCount(ModifierType type)
        {
            int count = 0;
            int target = (int)type;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_modifierTypes[i] == target) count++;
            }
            return count;
        }

        /// <summary>Vérifie si au moins un modificateur de ce type est actif.</summary>
        public bool HasModifier(ModifierType type)
        {
            return GetCount(type) > 0;
        }

        /// <summary>Retire tous les modificateurs d'un type donné.</summary>
        public void RemoveModifier(ModifierType type)
        {
            int target = (int)type;
            for (int i = _activeCount - 1; i >= 0; i--)
            {
                if (_modifierTypes[i] == target)
                    RemoveAt(i);
            }
        }

        /// <summary>Nettoie automatiquement les modificateurs expirés. Appelé dans Update().</summary>
        public void CleanExpired()
        {
            float now = Time.time;
            for (int i = _activeCount - 1; i >= 0; i--)
            {
                float duration = _modifierDurations[i];
                if (duration < 0f) continue; // durée négative = infini

                if (now - _modifierTimesAdded[i] > duration)
                {
                    this.Log($"Modifier expired: {(ModifierType)_modifierTypes[i]}");
                    RemoveAt(i);
                }
            }
        }

        /// <summary>Retire tous les modificateurs.</summary>
        public void ClearAll()
        {
            _activeCount = 0;
        }

        /// <summary>Info sur un modificateur à un index donné.</summary>
        public bool TryGetModifierInfo(int index, out ModifierType type, out ModifierMode mode, out float value, out float remaining, out DamageType damageType)
        {
            type = ModifierType.Speed; mode = ModifierMode.Additive;
            value = 0f; remaining = 0f; damageType = DamageType.None;

            if (index < 0 || index >= _activeCount) return false;

            type       = (ModifierType)_modifierTypes[index];
            mode       = _modifierModes[index];
            value      = _modifierValues[index];
            damageType = _modifierDamageTypes[index];
            float duration = _modifierDurations[index];
            remaining = duration < 0f ? -1f : Mathf.Max(0f, duration - (Time.time - _modifierTimesAdded[index]));
            return true;
        }

        private void RemoveAt(int index)
        {
            int last = _activeCount - 1;
            if (index != last)
            {
                _modifierTypes[index]       = _modifierTypes[last];
                _modifierModes[index]       = _modifierModes[last];
                _modifierValues[index]      = _modifierValues[last];
                _modifierDurations[index]   = _modifierDurations[last];
                _modifierTimesAdded[index]  = _modifierTimesAdded[last];
                _modifierDamageTypes[index] = _modifierDamageTypes[last];
            }
            _activeCount--;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            var infos = new System.Collections.Generic.List<M2922_GizmoDisplayInfo>
            {
                new M2922_GizmoDisplayInfo("Active", $"{_activeCount} / {_maxModifiers}")
            };

            for (int i = 0; i < _activeCount && i < 8; i++)
            {
                var type  = (ModifierType)_modifierTypes[i];
                var mode  = _modifierModes[i];
                float val = _modifierValues[i];
                var dmg   = _modifierDamageTypes[i];
                float dur = _modifierDurations[i];
                float rem = dur < 0f ? -1f : Mathf.Max(0f, dur - (Time.time - _modifierTimesAdded[i]));
                string remStr = rem < 0f ? "inf" : $"{rem:F1}s";

                string prefix = mode == ModifierMode.Multiplicative ? "x" : "+";
                string suffix = dmg != DamageType.None ? $" [{dmg}]" : "";
                infos.Add(new M2922_GizmoDisplayInfo($"  [{type}]", $"{prefix}{val:F2}{suffix} | {remStr}"));
            }

            if (_activeCount > 8)
                infos.Add(new M2922_GizmoDisplayInfo("  ...", $"+{_activeCount - 8} more"));

            return infos.ToArray();
        }
#endif
    }
}
