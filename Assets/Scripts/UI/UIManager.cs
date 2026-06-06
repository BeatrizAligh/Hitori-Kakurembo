using System.Collections.Generic;
using HitoriKakurembo.Core;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Utilities;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Actua como fachada central para actualizar los distintos paneles de interfaz del prototipo.
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        /// <summary>
        /// Referencia al panel de lobby.
        /// </summary>
        [UnityEngine.SerializeField] private LobbyUI lobbyUI = null;

        /// <summary>
        /// Referencia al panel de ronda o HUD principal.
        /// </summary>
        [UnityEngine.SerializeField] private RoundUI roundUI = null;

        /// <summary>
        /// Referencia al panel de scoreboard.
        /// </summary>
        [UnityEngine.SerializeField] private ScoreboardUI scoreboardUI = null;

        /// <summary>
        /// Referencia al panel de prompts de interaccion.
        /// </summary>
        [UnityEngine.SerializeField] private InteractionPromptUI interactionPromptUI = null;

        /// <summary>
        /// Inicializa el singleton y registra el manager en el localizador de servicios.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register<UIManager>(this);
        }

        /// <summary>
        /// Actualiza el conteo de jugadores mostrado en el panel de lobby.
        /// </summary>
        /// <param name="playerCount">
        /// Numero de jugadores conectados que debe mostrarse.
        /// </param>
        public void ShowLobbyPlayerCount(int playerCount)
        {
            lobbyUI?.RefreshPlayerCount(playerCount);
        }

        /// <summary>
        /// Actualiza si el lobby puede iniciar la partida.
        /// </summary>
        /// <param name="canStart">
        /// Indica si el boton o estado de inicio debe habilitarse.
        /// </param>
        public void SetLobbyStartAvailability(bool canStart)
        {
            lobbyUI?.SetCanStartMatch(canStart);
        }

        /// <summary>
        /// Actualiza el panel de ronda con el estado y numero actuales.
        /// </summary>
        /// <param name="roundState">
        /// Estado de ronda que debe mostrarse.
        /// </param>
        /// <param name="roundNumber">
        /// Numero de ronda que debe mostrarse.
        /// </param>
        public void UpdateRoundState(RoundState roundState, int roundNumber)
        {
            roundUI?.SetRoundState(roundState, roundNumber);
        }

        /// <summary>
        /// Actualiza el panel de scoreboard con la lista de puntuaciones suministrada.
        /// </summary>
        /// <param name="scores">
        /// Coleccion de puntuaciones preparada por la logica de partida.
        /// </param>
        public void ShowScores(IReadOnlyList<PlayerScoreData> scores)
        {
            scoreboardUI?.SetScores(scores);
        }

        /// <summary>
        /// Muestra un mensaje de interaccion contextual.
        /// </summary>
        /// <param name="message">
        /// Texto que debe mostrarse al jugador.
        /// </param>
        public void ShowInteractionPrompt(string message)
        {
            interactionPromptUI?.ShowPrompt(message);
        }

        /// <summary>
        /// Oculta el mensaje de interaccion contextual.
        /// </summary>
        public void HideInteractionPrompt()
        {
            interactionPromptUI?.HidePrompt();
        }
    }
}
