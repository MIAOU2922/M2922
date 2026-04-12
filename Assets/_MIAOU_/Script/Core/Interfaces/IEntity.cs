using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Interface de base pour toute entité dans le jeu (joueur, véhicule, objet)
    /// Tous vos modules peuvent référencer IEntity sans se connaître entre eux
    /// </summary>
    public interface IEntity
    {
        /// <summary>ID unique de l'entité</summary>
        int EntityId { get; }
        
        /// <summary>Nom de l'entité</summary>
        string EntityName { get; }
        
        /// <summary>Type d'entité (Player, Vehicle, Item, etc.)</summary>
        EntityType Type { get; }
        
        /// <summary>Transform de l'entité</summary>
        Transform EntityTransform { get; }
        
        /// <summary>L'entité est-elle active ?</summary>
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
        Spawner = 7
    }
}
