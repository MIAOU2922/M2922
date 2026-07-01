namespace M2922.Component.Modifier
{
    /// <summary>
    /// Types de modificateurs supportés.
    /// L'ordre détermine l'index dans les arrays parallèles.
    /// </summary>
    public enum ModifierType
    {
        Speed,
        Damage,
        AttackSpeed,
        Recoil,
        MaxHealth,
        Regen,
        Shield,
        Armor,
        JumpForce,
        Gravity,
        FireRate,
        ReloadSpeed,
        Spread,
        Invincibility,
        Stun,
        Flash,
        Custom00,
        Custom01,
        Custom02,
        Custom03,
        Custom04,
        _COUNT // sentinel
    }

    /// <summary>Mode d'application du modifier.</summary>
    public enum ModifierMode
    {
        Additive,       // base + somme des modifiers
        Multiplicative  // base × produit des modifiers
    }

    /// <summary>Type de dégât (None = pas un dégât).</summary>
    public enum DamageType
    {
        None      = -1,
        Generic   = 0,
        Bullet    = 1,
        Explosion = 2,
        Melee     = 3,
        Fire      = 4,
        Energy    = 5,
        Fall      = 6,
    }
}
