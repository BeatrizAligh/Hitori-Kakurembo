using UnityEngine;

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Controla la visibilidad visual y fisica del jugador habilitando o deshabilitando sus renderers y colliders.
    /// </summary>
    public class PlayerVisibilityHandler : MonoBehaviour
    {
        /// <summary>
        /// Renderers afectados cuando cambia el estado de visibilidad.
        /// </summary>
        [SerializeField] private Renderer[] trackedRenderers;

        /// <summary>
        /// Colliders afectados cuando cambia el estado de visibilidad.
        /// </summary>
        [SerializeField] private Collider[] trackedColliders;

        /// <summary>
        /// Obtiene un valor que indica si el jugador se considera visible en este momento.
        /// </summary>
        public bool IsVisible { get; private set; } = true;

        /// <summary>
        /// Inicializa automaticamente las referencias de renderers y colliders cuando no fueron asignadas desde el inspector.
        /// </summary>
        private void Awake()
        {
            if (trackedRenderers == null || trackedRenderers.Length == 0)
            {
                trackedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (trackedColliders == null || trackedColliders.Length == 0)
            {
                trackedColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        /// <summary>
        /// Aplica el estado de visibilidad solicitado a todos los renderers y colliders rastreados.
        /// </summary>
        /// <param name="visible">
        /// Estado de visibilidad que debe aplicarse.
        /// </param>
        public void SetVisible(bool visible)
        {
            IsVisible = visible;

            foreach (Renderer rendererComponent in trackedRenderers)
            {
                if (rendererComponent != null)
                {
                    rendererComponent.enabled = visible;
                }
            }

            foreach (Collider colliderComponent in trackedColliders)
            {
                if (colliderComponent != null)
                {
                    colliderComponent.enabled = visible;
                }
            }
        }
    }
}
