using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Interface commune à tous les game modes.
    /// Lue par M2922_Manager et M2922_RoundManager pour piloter la partie.
    /// 
    /// RÈGLE : Un GameMode ne modifie jamais directement les HP ou le score.
    ///         Il publie des événements sur l'EventBus.
    ///         Les autres systèmes (HealthController, ScoringSystem) réagissent.
    /// 
    /// Implémenté par : M2922_GameMode (base), M2922_GameMode_TDM,
    ///                  M2922_GameMode_FFA, M2922_GameMode_Domination,
    ///                  M2922_GameMode_SearchDestroy, M2922_GameMode_Horde
    /// </summary>
    public interface IGameMode
    {
        /// <summary>Nom du mode de jeu affiché en HUD</summary>
        string ModeName { get; }

        /// <summary>État actuel de la partie</summary>
        GameState CurrentState { get; }

        /// <summary>Temps restant en secondes (-1 si pas de timer)</summary>
        float TimeLeft { get; }

        /// <summary>Score limite pour gagner (-1 si pas de score limite)</summary>
        int ScoreLimit { get; }

        /// <summary>La partie est-elle en cours ?</summary>
        bool IsInProgress { get; }

        /// <summary>
        /// Démarre la partie (passe à l'état InProgress).
        /// Publie OnGameStarted sur l'EventBus.
        /// </summary>
        void StartGame();

        /// <summary>
        /// Termine la partie immédiatement.
        /// Publie OnGameFinished sur l'EventBus.
        /// </summary>
        void EndGame();

        /// <summary>
        /// Remet la partie à zéro (scores, timers, état des zones).
        /// Utilisé entre deux manches par RoundManager.
        /// Publie OnGameReset sur l'EventBus.
        /// </summary>
        void ResetGame();

        /// <summary>
        /// Retourne l'index de l'équipe gagnante (-1 si pas de vainqueur ou FFA).
        /// </summary>
        int GetWinnerTeamIndex();

        /// <summary>
        /// Retourne le PlayerId du joueur vainqueur en mode FFA (-1 si mode équipe).
        /// </summary>
        int GetWinnerPlayerId();
    }

    /// <summary>
    /// États d'une partie.
    /// </summary>
    public enum GameState
    {
        Waiting     = 0,   // En attente de joueurs
        Preparing   = 1,   // Compte à rebours avant le début
        InProgress  = 2,   // Partie en cours
        Finished    = 3,   // Partie terminée, affichage des résultats
    }
}
