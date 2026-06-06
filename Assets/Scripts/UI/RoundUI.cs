using HitoriKakurembo.Rounds;
using UnityEngine;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Conserva el estado visible de la ronda actual para el HUD o panel de partida.
    /// </summary>
    public class RoundUI : MonoBehaviour
    {
        /// <summary>
        /// Estado de ronda que la interfaz esta mostrando actualmente.
        /// </summary>
        [SerializeField] private RoundState currentState = RoundState.WaitingForPlayers;

        /// <summary>
        /// Numero de ronda que la interfaz esta mostrando actualmente.
        /// </summary>
        [SerializeField] private int currentRoundNumber;

        /// <summary>
        /// Obtiene el estado de ronda actual almacenado por la interfaz.
        /// </summary>
        public RoundState CurrentState => currentState;

        /// <summary>
        /// Obtiene el numero de ronda actual almacenado por la interfaz.
        /// </summary>
        public int CurrentRoundNumber => currentRoundNumber;

        /// <summary>
        /// Actualiza el estado y numero de ronda que debe representar la interfaz.
        /// </summary>
        /// <param name="roundState">
        /// Estado de ronda que debe almacenarse.
        /// </param>
        /// <param name="roundNumber">
        /// Numero de ronda que debe almacenarse.
        /// </param>
        public void SetRoundState(RoundState roundState, int roundNumber)
        {
            currentState = roundState;
            currentRoundNumber = Mathf.Max(0, roundNumber);
        }
    }
}
