using UnityEngine;

namespace M2922.Core
{
    /// Interface pour tout système d'armure absorbant les dégâts.
    public interface IArmored
    {
        float ArmorPoints { get; }
        float MaxArmorPoints { get; }
        bool HasArmor { get; }
        float AbsorbDamage(float incomingDamage, DamageType damageType);
        void RepairArmor(float amount);
        void RepairFull();
    }
}
