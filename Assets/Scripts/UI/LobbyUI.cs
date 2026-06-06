using UnityEngine;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Conserva el estado visible del lobby para que el resto de la interfaz lo pueda presentar.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        /// <summary>
        /// Ultimo conteo de jugadores conectado mostrado por el lobby.
        /// </summary>
        [SerializeField] private int connectedPlayers;

        /// <summary>
        /// Indica si el lobby permite iniciar una partida.
        /// </summary>
        [SerializeField] private bool canStartMatch;

        /// <summary>
        /// Obtiene el ultimo conteo de jugadores registrado.
        /// </summary>
        public int ConnectedPlayers => connectedPlayers;

        /// <summary>
        /// Obtiene un valor que indica si el lobby se encuentra listo para iniciar.
        /// </summary>
        public bool CanStartMatch => canStartMatch;

        /// <summary>
        /// Actualiza el conteo de jugadores visible del lobby.
        /// </summary>
        /// <param name="playerCount">
        /// Conteo de jugadores que debe mostrarse.
        /// </param>
        public void RefreshPlayerCount(int playerCount)
        {
            connectedPlayers = Mathf.Max(0, playerCount);
        }

        /// <summary>
        /// Actualiza si el lobby puede iniciar una partida.
        /// </summary>
        /// <param name="value">
        /// Estado que debe aplicarse al lobby.
        /// </param>
        public void SetCanStartMatch(bool value)
        {
            canStartMatch = value;
        }
    }
}
