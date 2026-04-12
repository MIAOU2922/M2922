using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// Interface de base pour toute entité dans le jeu (joueur, véhicule, objet)
    public interface IEntity
    {
        int EntityId { get; }
        string EntityName { get; }
        EntityType Type { get; }
        Transform EntityTransform { get; }
        bool IsActive { get; }
    }
    public enum EntityType
    {
        None = 0,
        Player = 1,
        Vehicle = 2,
        Weapon = 3,
        Projectile = 4,
        Item = 5,
        Zone = 6,
        Spawner = 7,
        Other = 8
    }
}
