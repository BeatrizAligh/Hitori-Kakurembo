using HitoriKakurembo.PlayerSystem.Core;

namespace HitoriKakurembo.PlayerSystem.Interfaces
{
    /// <summary>
    /// Contrato que debe implementar cualquier objeto del mundo que pueda ser usado por un jugador.
    /// La interfaz no conoce items, rituales, sellos ni rondas; solo define la comunicacion minima con el Player System.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Devuelve el texto que podria mostrarse al jugador cuando mira este objeto.
        /// </summary>
        /// <returns>
        /// Mensaje descriptivo de la accion disponible.
        /// </returns>
        string GetInteractionText();

        /// <summary>
        /// Evalua si el jugador recibido puede interactuar con este objeto en el estado actual.
        /// </summary>
        /// <param name="player">
        /// Raiz del jugador que intenta interactuar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la interaccion esta permitida.
        /// </returns>
        bool CanInteract(PlayerRoot player);

        /// <summary>
        /// Ejecuta la interaccion solicitada por el jugador.
        /// </summary>
        /// <param name="player">
        /// Raiz del jugador que confirma la interaccion.
        /// </param>
        void Interact(PlayerRoot player);
    }
}
