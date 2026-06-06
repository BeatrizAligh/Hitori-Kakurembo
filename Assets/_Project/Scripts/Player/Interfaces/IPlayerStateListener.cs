using HitoriKakurembo.PlayerSystem.Data;

namespace HitoriKakurembo.PlayerSystem.Interfaces
{
    /// <summary>
    /// Contrato para componentes que necesitan reaccionar cuando cambia el estado de vida del jugador.
    /// Se usa para mantener sistemas desacoplados entre si, por ejemplo movimiento, audio o visuales.
    /// </summary>
    public interface IPlayerStateListener
    {
        /// <summary>
        /// Notifica que el estado de vida o participacion del jugador cambio.
        /// </summary>
        /// <param name="newState">
        /// Nuevo estado aplicado al jugador.
        /// </param>
        void OnPlayerStateChanged(PlayerLifeStateType newState);
    }
}
