using UnityEngine;

namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Representa un sello ritual individual y mantiene su estado visual local segun la autoridad del servidor.
    /// </summary>
    public class RitualSeal : MonoBehaviour
    {
        /// <summary>
        /// Color usado cuando el sello todavia puede activarse.
        /// </summary>
        private static readonly Color InactiveColor = new Color(0.92f, 0.76f, 0.32f, 1f);

        /// <summary>
        /// Color usado cuando el sello ya fue activado por un superviviente.
        /// </summary>
        private static readonly Color ActivatedColor = new Color(0.34f, 1f, 0.72f, 1f);

        /// <summary>
        /// Indice logico del sello dentro del conjunto fijo esperado por el manager.
        /// </summary>
        [SerializeField] private int sealIndex;

        /// <summary>
        /// Indica si el sello ya fue activado en la ronda actual.
        /// </summary>
        [SerializeField] private bool isActivated;

        /// <summary>
        /// Client id del jugador que activo el sello; se usa para UI/debug y auditoria de prototipo.
        /// </summary>
        [SerializeField] private ulong activatedByClientId = ulong.MaxValue;

        /// <summary>
        /// Renderer cacheado para actualizar el color del placeholder sin buscarlo cada frame.
        /// </summary>
        private Renderer cachedRenderer;

        /// <summary>
        /// Obtiene el indice logico del sello.
        /// </summary>
        public int SealIndex => sealIndex;

        /// <summary>
        /// Obtiene un valor que indica si el sello ya fue activado.
        /// </summary>
        public bool IsActivated => isActivated;

        /// <summary>
        /// Obtiene el client id del jugador que activo este sello.
        /// </summary>
        public ulong ActivatedByClientId => activatedByClientId;

        /// <summary>
        /// Cachea referencias visuales y aplica el color inicial correcto.
        /// </summary>
        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            ApplyVisualState();
        }

        /// <summary>
        /// Actualiza el indice logico del sello aplicando un limite inferior de cero.
        /// </summary>
        /// <param name="index">
        /// Nuevo indice del sello.
        /// </param>
        public void SetSealIndex(int index)
        {
            sealIndex = Mathf.Max(0, index);
        }

        /// <summary>
        /// Marca el sello como activado por un jugador concreto.
        /// </summary>
        /// <param name="activatorClientId">
        /// Client id del superviviente que completo la activacion.
        /// </param>
        public void ActivateSeal(ulong activatorClientId)
        {
            SetActivationState(true, activatorClientId);
        }

        /// <summary>
        /// Marca el sello como activado sin registrar activador especifico.
        /// </summary>
        public void ActivateSeal()
        {
            ActivateSeal(ulong.MaxValue);
        }

        /// <summary>
        /// Restaura el sello al estado no activado.
        /// </summary>
        public void ResetSeal()
        {
            SetActivationState(false, ulong.MaxValue);
        }

        /// <summary>
        /// Aplica un estado recibido desde el servidor sin ejecutar validaciones de gameplay locales.
        /// </summary>
        /// <param name="activated">
        /// Estado autoritativo del sello.
        /// </param>
        /// <param name="activatorClientId">
        /// Jugador que activo el sello, o <see cref="ulong.MaxValue"/> si no existe.
        /// </param>
        public void ApplyNetworkState(bool activated, ulong activatorClientId)
        {
            SetActivationState(activated, activatorClientId);
        }

        /// <summary>
        /// Actualiza campos internos y refresca la representacion visual.
        /// </summary>
        /// <param name="activated">
        /// Nuevo estado activado.
        /// </param>
        /// <param name="activatorClientId">
        /// Client id asociado a la activacion.
        /// </param>
        private void SetActivationState(bool activated, ulong activatorClientId)
        {
            isActivated = activated;
            activatedByClientId = activated ? activatorClientId : ulong.MaxValue;
            ApplyVisualState();
        }

        /// <summary>
        /// Cambia el color del placeholder para que los jugadores vean claramente que el sello ya fue completado.
        /// </summary>
        private void ApplyVisualState()
        {
            cachedRenderer = cachedRenderer != null ? cachedRenderer : GetComponent<Renderer>();

            if (cachedRenderer != null)
            {
                cachedRenderer.material.color = isActivated ? ActivatedColor : InactiveColor;
            }
        }
    }
}
