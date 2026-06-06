using UnityEngine;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Conserva el contenido y estado visible del prompt contextual de interaccion.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        /// <summary>
        /// Mensaje de prompt que la interfaz deberia mostrar actualmente.
        /// </summary>
        [SerializeField] private string currentPrompt = string.Empty;

        /// <summary>
        /// Indica si el prompt se encuentra visible para el jugador.
        /// </summary>
        [SerializeField] private bool isVisible;

        /// <summary>
        /// Obtiene el texto actual del prompt.
        /// </summary>
        public string CurrentPrompt => currentPrompt;

        /// <summary>
        /// Obtiene un valor que indica si el prompt se encuentra visible.
        /// </summary>
        public bool IsVisible => isVisible;

        /// <summary>
        /// Muestra un nuevo mensaje de prompt y actualiza su estado visible.
        /// </summary>
        /// <param name="promptMessage">
        /// Texto que debe mostrarse al jugador.
        /// </param>
        public void ShowPrompt(string promptMessage)
        {
            currentPrompt = promptMessage ?? string.Empty;
            isVisible = !string.IsNullOrWhiteSpace(currentPrompt);
        }

        /// <summary>
        /// Limpia el mensaje actual y oculta el prompt.
        /// </summary>
        public void HidePrompt()
        {
            currentPrompt = string.Empty;
            isVisible = false;
        }
    }
}
