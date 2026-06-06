using HitoriKakurembo.PlayerSystem.Core;
using HitoriKakurembo.PlayerSystem.Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.PlayerSystem.Movement
{
    /// <summary>
    /// Controla el movimiento 3D basico del jugador local.
    /// Solo procesa input para el owner de Netcode, o para pruebas locales donde el objeto aun no esta spawneado en red.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMovementController : NetworkBehaviour, IPlayerControllable
    {
        /// <summary>
        /// Componente que entrega el input local centralizado.
        /// </summary>
        [SerializeField] private PlayerInputHandler inputHandler;

        /// <summary>
        /// Referencia visual usada para orientar el movimiento respecto a la camara.
        /// </summary>
        [SerializeField] private PlayerLookController lookController;

        /// <summary>
        /// CharacterController que resuelve colisiones y suelo.
        /// </summary>
        [SerializeField] private CharacterController characterController;

        /// <summary>
        /// Velocidad base de caminata.
        /// </summary>
        [SerializeField] private float defaultMoveSpeed = 4f;

        /// <summary>
        /// Multiplicador aplicado al correr.
        /// </summary>
        [SerializeField] private float runSpeedMultiplier = 1.6f;

        /// <summary>
        /// Altura aproximada del salto.
        /// </summary>
        [SerializeField] private float jumpHeight = 1.2f;

        /// <summary>
        /// Gravedad aplicada al movimiento vertical.
        /// </summary>
        [SerializeField] private float gravity = -18f;

        /// <summary>
        /// Velocidad vertical maxima negativa para evitar acumulacion extrema.
        /// </summary>
        [SerializeField] private float terminalVelocity = -40f;

        /// <summary>
        /// Indica si el control de movimiento esta habilitado para este componente.
        /// </summary>
        private bool controlEnabled = true;

        /// <summary>
        /// Velocidad actual configurable por sistemas externos.
        /// </summary>
        private float currentMoveSpeed;

        /// <summary>
        /// Velocidad vertical acumulada por salto y gravedad.
        /// </summary>
        private float verticalVelocity;

        /// <summary>
        /// Obtiene si el movimiento esta actualmente habilitado.
        /// </summary>
        public bool IsControlEnabled => controlEnabled;

        /// <summary>
        /// Obtiene la velocidad horizontal base actual.
        /// </summary>
        public float CurrentMoveSpeed => currentMoveSpeed;

        /// <summary>
        /// Cachea referencias y prepara la velocidad inicial.
        /// </summary>
        private void Awake()
        {
            CacheReferences();
            currentMoveSpeed = defaultMoveSpeed;
        }

        /// <summary>
        /// Procesa desplazamiento local solo para el owner.
        /// </summary>
        private void Update()
        {
            if (!controlEnabled || !CanProcessOwnerInput())
            {
                return;
            }

            HandleMovement();
        }

        /// <summary>
        /// Habilita el control de movimiento.
        /// </summary>
        public void EnableControl()
        {
            EnableMovement();
        }

        /// <summary>
        /// Deshabilita el control de movimiento sin destruir ni desactivar el componente.
        /// </summary>
        public void DisableControl()
        {
            DisableMovement();
        }

        /// <summary>
        /// Habilita el movimiento del jugador.
        /// </summary>
        public void EnableMovement()
        {
            controlEnabled = true;
        }

        /// <summary>
        /// Deshabilita el movimiento del jugador.
        /// </summary>
        public void DisableMovement()
        {
            controlEnabled = false;
        }

        /// <summary>
        /// Define una nueva velocidad base de movimiento.
        /// </summary>
        /// <param name="speed">
        /// Nueva velocidad horizontal. Se limita a valores positivos.
        /// </param>
        public void SetMovementSpeed(float speed)
        {
            currentMoveSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// Restaura la velocidad base configurada en el prefab.
        /// </summary>
        public void ResetMovementSpeed()
        {
            currentMoveSpeed = defaultMoveSpeed;
        }

        /// <summary>
        /// Cachea referencias del mismo prefab sin usar busquedas globales.
        /// </summary>
        public void CacheReferences()
        {
            inputHandler = inputHandler != null ? inputHandler : GetComponent<PlayerInputHandler>();
            lookController = lookController != null ? lookController : GetComponent<PlayerLookController>();
            characterController = characterController != null ? characterController : GetComponent<CharacterController>();
        }

        /// <summary>
        /// Determina si este componente puede procesar input local.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> para owner, o para pruebas locales sin NetworkObject spawneado.
        /// </returns>
        private bool CanProcessOwnerInput()
        {
            NetworkObject networkObject = NetworkObject;
            return networkObject == null || !networkObject.IsSpawned || IsOwner;
        }

        /// <summary>
        /// Calcula movimiento horizontal, salto y gravedad para este frame.
        /// </summary>
        private void HandleMovement()
        {
            CacheReferences();

            Vector2 moveInput = inputHandler != null ? inputHandler.MoveInput : Vector2.zero;
            bool runHeld = inputHandler != null && inputHandler.RunHeld;
            bool jumpPressed = inputHandler != null && inputHandler.JumpPressed;
            Vector3 movementDirection = BuildCameraRelativeMovement(moveInput);
            float speed = runHeld ? currentMoveSpeed * runSpeedMultiplier : currentMoveSpeed;

            ApplyGravityAndJump(jumpPressed);

            Vector3 frameMovement = (movementDirection * speed) + (Vector3.up * verticalVelocity);
            Move(frameMovement * Time.deltaTime);
        }

        /// <summary>
        /// Convierte input 2D en direccion mundial relativa a la vista horizontal del jugador.
        /// </summary>
        /// <param name="moveInput">
        /// Input de movimiento normalizado.
        /// </param>
        /// <returns>
        /// Direccion mundial horizontal.
        /// </returns>
        private Vector3 BuildCameraRelativeMovement(Vector2 moveInput)
        {
            Transform referenceTransform = lookController != null ? lookController.ViewTransform : transform;
            Vector3 forward = referenceTransform.forward;
            Vector3 right = referenceTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 movement = (right * moveInput.x) + (forward * moveInput.y);
            return movement.sqrMagnitude > 1f ? movement.normalized : movement;
        }

        /// <summary>
        /// Aplica gravedad continua y salto cuando el jugador esta en suelo.
        /// </summary>
        /// <param name="jumpPressed">
        /// Indica si se solicito salto durante este frame.
        /// </param>
        private void ApplyGravityAndJump(bool jumpPressed)
        {
            bool isGrounded = characterController != null ? characterController.isGrounded : transform.position.y <= 0.01f;

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (isGrounded && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity = Mathf.Max(terminalVelocity, verticalVelocity + (gravity * Time.deltaTime));
        }

        /// <summary>
        /// Ejecuta el desplazamiento usando CharacterController si existe; si no, aplica transform como fallback de prueba.
        /// </summary>
        /// <param name="delta">
        /// Desplazamiento mundial del frame.
        /// </param>
        private void Move(Vector3 delta)
        {
            if (characterController != null)
            {
                characterController.Move(delta);
                return;
            }

            transform.position += delta;
        }
    }
}
