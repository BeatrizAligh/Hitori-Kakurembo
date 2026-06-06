using UnityEngine;

namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Expone un punto de activacion para un sello ritual concreto.
    /// </summary>
    public class SealActivationZone : MonoBehaviour
    {
        /// <summary>
        /// Referencia al sello que debe activarse cuando esta zona sea usada por la logica de juego.
        /// </summary>
        [SerializeField] private RitualSeal targetSeal = null;

        /// <summary>
        /// Obtiene el sello controlado por esta zona de activacion.
        /// </summary>
        public RitualSeal TargetSeal => targetSeal;

        /// <summary>
        /// Asigna el sello que esta zona debe representar dentro de la escena.
        /// </summary>
        /// <param name="seal">
        /// Sello ritual asociado a esta zona interactiva.
        /// </param>
        public void SetTargetSeal(RitualSeal seal)
        {
            targetSeal = seal;
        }

        /// <summary>
        /// Intenta activar el sello asociado a la zona.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando existe un sello asignado y fue activado; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool TryActivateSeal()
        {
            if (targetSeal == null)
            {
                return false;
            }

            targetSeal.ActivateSeal();
            return true;
        }
    }
}
