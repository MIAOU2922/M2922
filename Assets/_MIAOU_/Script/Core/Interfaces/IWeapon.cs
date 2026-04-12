using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour toutes les armes du jeu
    /// </summary>
    public interface IWeapon
    {
        /// <summary>Nom de l'arme</summary>
        string WeaponName { get; }
        
        /// <summary>Type d'arme</summary>
        WeaponType Type { get; }
        
        /// <summary>Munitions actuelles</summary>
        int CurrentAmmo { get; }
        
        /// <summary>Munitions max par chargeur</summary>
        int MaxAmmo { get; }
        
        /// <summary>Munitions de réserve</summary>
        int ReserveAmmo { get; }
        
        /// <summary>Dégâts par tir</summary>
        float Damage { get; }
        
        /// <summary>Cadence de tir (coups/seconde)</summary>
        float FireRate { get; }
        
        /// <summary>Portée maximale</summary>
        float Range { get; }
        
        /// <summary>L'arme est-elle équipée ?</summary>
        bool IsEquipped { get; }
        
        /// <summary>L'arme peut-elle tirer ?</summary>
        bool CanFire { get; }
        
        /// <summary>
        /// Tirer avec l'arme
        /// </summary>
        void Fire();
        
        /// <summary>
        /// Recharger l'arme
        /// </summary>
        void Reload();
        
        /// <summary>
        /// Équiper l'arme
        /// </summary>
        void Equip(VRCPlayerApi player);
        
        /// <summary>
        /// Déséquiper l'arme
        /// </summary>
        void Unequip();
    }
    
    public enum WeaponType
    {
        None = 0,
        Pistol = 1,
        Rifle = 2,
        Shotgun = 3,
        Sniper = 4,
        SMG = 5,
        Melee = 6,
        Grenade = 7,
        Launcher = 8
    }
}
