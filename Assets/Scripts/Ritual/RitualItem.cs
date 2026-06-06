using UnityEngine;

namespace HitoriKakurembo.Ritual
{
    /// <summary>
    /// Representa un objeto o elemento de la escena que forma parte del progreso del ritual.
    /// </summary>
    public class RitualItem : MonoBehaviour
    {
        /// <summary>
        /// Identificador logico del item ritual para integraciones futuras con datos, UI o guardado.
        /// </summary>
        [SerializeField] private string itemId = "ritual-item";

        /// <summary>
        /// Indica si el item ya fue recogido, completado o procesado por la logica del ritual.
        /// </summary>
        [SerializeField] private bool isCollected;

        /// <summary>
        /// Obtiene el identificador logico del item ritual.
        /// </summary>
        public string ItemId => itemId;

        /// <summary>
        /// Obtiene un valor que indica si el item ya se considera completado.
        /// </summary>
        public bool IsCollected => isCollected;

        /// <summary>
        /// Actualiza el estado de recoleccion del item ritual.
        /// </summary>
        /// <param name="value">
        /// Nuevo estado que debe aplicar la logica del ritual.
        /// </param>
        public void SetCollected(bool value)
        {
            isCollected = value;
        }
    }
}
