using UnityEngine;

namespace HitoriKakurembo.Traps
{
    /// <summary>
    /// Enumera los tipos base de trampas que el muneco puede utilizar.
    /// </summary>
    public enum TrapType
    {
        /// <summary>
        /// Indica la ausencia de un tipo de trampa valido.
        /// </summary>
        None = 0,

        /// <summary>
        /// Trampa orientada a audio o engaño mediante voces.
        /// </summary>
        Voice = 1,

        /// <summary>
        /// Trampa orientada a sobresalto o presion inmediata.
        /// </summary>
        Screamer = 2,

        /// <summary>
        /// Trampa orientada a reposicionamiento o desvio.
        /// </summary>
        Teleport = 3,

        /// <summary>
        /// Trampa orientada a cambios de estado o transformacion.
        /// </summary>
        DollTransformation = 4
    }

    /// <summary>
    /// Define la funcionalidad comun de cualquier trampa desplegable por el muneco.
    /// </summary>
    public abstract class TrapBase : MonoBehaviour
    {
        /// <summary>
        /// Nombre visible de la trampa para interfaces, trazas o depuracion.
        /// </summary>
        [SerializeField] private string displayName = "Trap";

        /// <summary>
        /// Indica si la trampa se encuentra lista para activarse.
        /// </summary>
        [SerializeField] private bool isArmed = true;

        /// <summary>
        /// Obtiene el tipo concreto de trampa implementado por la subclase.
        /// </summary>
        public abstract TrapType Type { get; }

        /// <summary>
        /// Obtiene el nombre visible final de la trampa.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Type.ToString() : displayName;

        /// <summary>
        /// Obtiene un valor que indica si la trampa esta armada.
        /// </summary>
        public bool IsArmed => isArmed;

        /// <summary>
        /// Marca la trampa como armada.
        /// </summary>
        public virtual void Arm()
        {
            isArmed = true;
        }

        /// <summary>
        /// Marca la trampa como desarmada.
        /// </summary>
        public virtual void Disarm()
        {
            isArmed = false;
        }

        /// <summary>
        /// Ejecuta la activacion base de la trampa sobre el objeto instigador recibido.
        /// </summary>
        /// <param name="instigator">
        /// Objeto que provoco la activacion de la trampa.
        /// </param>
        public virtual void TriggerTrap(GameObject instigator)
        {
            if (!isArmed)
            {
                return;
            }

            Debug.Log($"{DisplayName} triggered by {(instigator != null ? instigator.name : "Unknown")}.");
        }
    }
}
