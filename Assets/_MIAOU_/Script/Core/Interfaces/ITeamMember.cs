using UdonSharp;
using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface pour tout ce qui appartient à une équipe
    /// (joueurs, véhicules, zones capturables)
    /// 
    /// NOTE ARCHITECTURE:
    /// - Cette interface est OPTIONNELLE. Utilisez-la uniquement pour les modes team-based.
    /// - Les modes FFA (Free-for-all/Chacun pour soi) n'ont PAS besoin du TeamManager
    ///   ni de cette interface.
    /// 
    /// LIMITATION UDONSHARP:
    /// UdonSharp ne supporte PAS l'héritage d'interface au runtime.
    /// Cette interface est principalement pour la documentation et l'architecture.
    /// Les classes qui "implémentent" ITeamMember doivent implémenter les méthodes
    /// manuellement sans utiliser le mot-clé "interface" dans la déclaration.
    /// 
    /// SYSTÈME N-TEAM:
    /// Les équipes sont des index int (0-15). NO_TEAM = -1.
    /// Configuré dans M2922_TeamManager via TeamConfig[].
    /// </summary>
    public interface ITeamMember
    {
        /// <summary>Index de l'équipe actuelle (-1 = pas d'équipe)</summary>
        int TeamIndex { get; }
        
        /// <summary>
        /// Changer d'équipe
        /// </summary>
        void SetTeam(int teamIndex);
        
        /// <summary>
        /// Vérifier si une entité est alliée
        /// </summary>
        bool IsFriendly(int otherTeamIndex);
        
        /// <summary>
        /// Vérifier si une entité est ennemie
        /// </summary>
        bool IsEnemy(int otherTeamIndex);
    }
}
