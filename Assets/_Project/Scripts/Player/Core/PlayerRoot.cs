using UnityEngine;
using HitoriKakurembo.PlayerSystem.Interaction;
using HitoriKakurembo.PlayerSystem.Inventory;
using HitoriKakurembo.PlayerSystem.Movement;
using HitoriKakurembo.PlayerSystem.Network;
using HitoriKakurembo.PlayerSystem.Roles;
using HitoriKakurembo.PlayerSystem.State;

namespace HitoriKakurembo.PlayerSystem.Core
{
    /// <summary>
    /// Punto de entrada principal del Player System.
    /// Esta clase actua como fachada central del prefab de jugador: no ejecuta gameplay directamente,
    /// solo concentra referencias a los modulos especializados para que otros scripts puedan acceder a ellos sin buscarlos manualmente.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRoot : MonoBehaviour
    {
        /// <summary>
        /// Componente responsable de leer input local y exponerlo al resto del Player System.
        /// </summary>
        [SerializeField] private PlayerInputHandler inputHandler;

        /// <summary>
        /// Datos de red sincronizados del jugador. Otros sistemas deberian leer identidad y estados online desde aqui.
        /// </summary>
        [SerializeField] private PlayerNetworkData networkData;

        /// <summary>
        /// Controlador responsable del desplazamiento local del jugador owner.
        /// </summary>
        [SerializeField] private PlayerMovementController movementController;

        /// <summary>
        /// Controlador responsable de la mirada y camara local del jugador.
        /// </summary>
        [SerializeField] private PlayerLookController lookController;

        /// <summary>
        /// Controlador responsable de detectar y ejecutar interacciones del jugador.
        /// </summary>
        [SerializeField] private PlayerInteractionController interactionController;

        /// <summary>
        /// Inventario local basico del jugador.
        /// </summary>
        [SerializeField] private PlayerInventory inventory;

        /// <summary>
        /// Manejador de rol asignado al jugador.
        /// </summary>
        [SerializeField] private PlayerRoleHandler roleHandler;

        /// <summary>
        /// Estado de vida y participacion del jugador.
        /// </summary>
        [SerializeField] private PlayerLifeState lifeState;

        /// <summary>
        /// Controlador de visibilidad local/remota del jugador.
        /// </summary>
        [SerializeField] private PlayerVisibilityController visibilityController;

        /// <summary>
        /// Obtiene el lector de input local del jugador.
        /// </summary>
        public PlayerInputHandler InputHandler => inputHandler;

        /// <summary>
        /// Obtiene los datos de red sincronizados del jugador.
        /// </summary>
        public PlayerNetworkData NetworkData => networkData;

        /// <summary>
        /// Obtiene el controlador de movimiento del jugador.
        /// </summary>
        public PlayerMovementController MovementController => movementController;

        /// <summary>
        /// Obtiene el controlador de mirada y camara del jugador.
        /// </summary>
        public PlayerLookController LookController => lookController;

        /// <summary>
        /// Obtiene el controlador de interaccion del jugador.
        /// </summary>
        public PlayerInteractionController InteractionController => interactionController;

        /// <summary>
        /// Obtiene el inventario basico del jugador.
        /// </summary>
        public PlayerInventory Inventory => inventory;

        /// <summary>
        /// Obtiene el manejador de rol del jugador.
        /// </summary>
        public PlayerRoleHandler RoleHandler => roleHandler;

        /// <summary>
        /// Obtiene el estado de vida del jugador.
        /// </summary>
        public PlayerLifeState LifeState => lifeState;

        /// <summary>
        /// Obtiene el controlador de visibilidad del jugador.
        /// </summary>
        public PlayerVisibilityController VisibilityController => visibilityController;

        /// <summary>
        /// Cachea referencias automaticamente al iniciar la instancia en escena.
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// Cachea referencias automaticamente cuando el componente se agrega o se reinicia desde el editor.
        /// </summary>
        private void Reset()
        {
            CacheReferences();
        }

        /// <summary>
        /// Revalida referencias en editor cuando se modifica el prefab o sus componentes.
        /// </summary>
        private void OnValidate()
        {
            CacheReferences();
        }

        /// <summary>
        /// Busca en el mismo GameObject los componentes del Player System y actualiza las referencias cacheadas.
        /// No usa busquedas globales para mantener bajo acoplamiento y permitir que el prefab sea portable.
        /// </summary>
        public void CacheReferences()
        {
            inputHandler = inputHandler != null ? inputHandler : GetComponent<PlayerInputHandler>();
            networkData = networkData != null ? networkData : GetComponent<PlayerNetworkData>();
            movementController = movementController != null ? movementController : GetComponent<PlayerMovementController>();
            lookController = lookController != null ? lookController : GetComponent<PlayerLookController>();
            interactionController = interactionController != null ? interactionController : GetComponent<PlayerInteractionController>();
            inventory = inventory != null ? inventory : GetComponent<PlayerInventory>();
            roleHandler = roleHandler != null ? roleHandler : GetComponent<PlayerRoleHandler>();
            lifeState = lifeState != null ? lifeState : GetComponent<PlayerLifeState>();
            visibilityController = visibilityController != null ? visibilityController : GetComponent<PlayerVisibilityController>();
        }

        /// <summary>
        /// Indica si el GameObject contiene todos los modulos principales requeridos por el Player System.
        /// Es util para validaciones de prefab y pruebas tempranas en escenas vacias.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando todas las referencias principales fueron resueltas.
        /// </returns>
        public bool HasRequiredReferences()
        {
            return inputHandler != null
                && networkData != null
                && movementController != null
                && lookController != null
                && interactionController != null
                && inventory != null
                && roleHandler != null
                && lifeState != null
                && visibilityController != null;
        }
    }
}
