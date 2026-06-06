using UnityEngine;

namespace HitoriKakurembo.Items
{
    /// <summary>
    /// Enumera los tipos de items base disponibles para el prototipo.
    /// </summary>
    public enum ItemType
    {
        /// <summary>
        /// Indica la ausencia de un tipo de item valido.
        /// </summary>
        None = 0,

        /// <summary>
        /// Item relacionado con captura de evidencia visual.
        /// </summary>
        Camera = 1,

        /// <summary>
        /// Item relacionado con iluminacion o pasos rituales.
        /// </summary>
        Candle = 2,

        /// <summary>
        /// Item relacionado con deteccion de huellas o rastros.
        /// </summary>
        Flour = 3,

        /// <summary>
        /// Item relacionado con deteccion ambiental o de temperatura.
        /// </summary>
        Thermometer = 4,

        /// <summary>
        /// Item relacionado con proteccion o limpieza ritual.
        /// </summary>
        SaltWater = 5
    }

    /// <summary>
    /// Define la funcionalidad comun de cualquier item utilizable por el jugador.
    /// </summary>
    public abstract class ItemBase : MonoBehaviour
    {
        /// <summary>
        /// Nombre visible del item para interfaces, depuracion o trazas.
        /// </summary>
        [SerializeField] private string displayName = "Item";

        /// <summary>
        /// Indica si el item debe consumirse o agotarse al usarse.
        /// </summary>
        [SerializeField] private bool consumable = true;

        /// <summary>
        /// Obtiene el tipo concreto del item implementado por la subclase.
        /// </summary>
        public abstract ItemType Type { get; }

        /// <summary>
        /// Obtiene el nombre final visible del item.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Type.ToString() : displayName;

        /// <summary>
        /// Obtiene un valor que indica si el item se considera consumible.
        /// </summary>
        public bool Consumable => consumable;

        /// <summary>
        /// Ejecuta el comportamiento basico de uso del item.
        /// </summary>
        public virtual void Use()
        {
            Debug.Log($"{DisplayName} used.");
        }
    }
}
