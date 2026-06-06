using HitoriKakurembo.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Implementa un controlador de movimiento basico para el propietario local del jugador durante el prototipo.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        /// <summary>
        /// Nombre de la escena donde el jugador puede moverse durante el prototipo.
        /// </summary>
        private const string GameSceneName = "GameScene";

        /// <summary>
        /// Velocidad lineal aplicada al desplazamiento del jugador.
        /// </summary>
        [SerializeField] private float moveSpeed = 4f;

        /// <summary>
        /// Intensidad de la gravedad aplicada al controlador para mantener la capsula apoyada sobre el suelo.
        /// </summary>
        [SerializeField] private float gravity = -18f;

        /// <summary>
        /// Velocidad utilizada para interpolar la rotacion del personaje hacia la direccion de movimiento.
        /// </summary>
        [SerializeField] private float rotationSpeed = 12f;

        /// <summary>
        /// Controla si el jugador puede procesar movimiento en el estado actual.
        /// </summary>
        [SerializeField] private bool canMove = true;

        /// <summary>
        /// Indica si otro sistema, como la camara en primera persona, controla la rotacion horizontal del jugador.
        /// </summary>
        private bool useExternalViewRotation;

        /// <summary>
        /// Controlador de colisiones usado para mover la capsula sin atravesar el suelo ni otros colliders basicos.
        /// </summary>
        private CharacterController characterController;

        /// <summary>
        /// Referencia al estado de red del jugador para respetar si el servidor lo marco vivo o fuera de ronda.
        /// </summary>
        private NetworkPlayer networkPlayer;

        /// <summary>
        /// Velocidad vertical acumulada por gravedad.
        /// </summary>
        private float verticalVelocity;

        /// <summary>
        /// Obtiene un valor que indica si el movimiento del jugador esta habilitado.
        /// </summary>
        public bool CanMove => canMove;

        /// <summary>
        /// Cachea el CharacterController para que el movimiento funcione tanto en prefabs runtime como en prefabs creados en editor.
        /// </summary>
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            networkPlayer = GetComponent<NetworkPlayer>();
        }

        /// <summary>
        /// Procesa el movimiento local del jugador solo cuando el objeto pertenece al cliente actual.
        /// </summary>
        private void Update()
        {
            if (!IsOwner || !canMove || !CanProcessGameplayMovement() || !CanProcessAliveMovement())
            {
                return;
            }

            HandleMovement();
        }

        /// <summary>
        /// Habilita o deshabilita el movimiento del jugador.
        /// </summary>
        /// <param name="value">
        /// Valor que define si el jugador podra moverse.
        /// </param>
        public void SetMovementEnabled(bool value)
        {
            canMove = value;
        }

        /// <summary>
        /// Define si el controlador debe respetar una rotacion aplicada por otro componente local.
        /// </summary>
        /// <param name="value">
        /// <see langword="true"/> cuando la camara o un sistema externo controla el yaw del jugador.
        /// </param>
        public void SetExternalRotationEnabled(bool value)
        {
            useExternalViewRotation = value;
        }

        /// <summary>
        /// Reubica la capsula de forma segura y reinicia la velocidad vertical acumulada.
        /// </summary>
        /// <param name="position">
        /// Posicion mundial donde debe quedar el jugador.
        /// </param>
        /// <param name="rotation">
        /// Rotacion mundial que debe aplicar el jugador.
        /// </param>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            verticalVelocity = 0f;

            if (characterController == null)
            {
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = wasEnabled;
        }

        /// <summary>
        /// Lee el input de movimiento, traslada al personaje y orienta su rotacion hacia la direccion resultante.
        /// </summary>
        private void HandleMovement()
        {
            Vector2 moveInput = ReadMoveInput();
            Vector3 horizontalMovement = useExternalViewRotation
                ? (transform.right * moveInput.x) + (transform.forward * moveInput.y)
                : new Vector3(moveInput.x, 0f, moveInput.y);
            horizontalMovement.y = 0f;

            if (horizontalMovement.sqrMagnitude > 1f)
            {
                horizontalMovement.Normalize();
            }

            ApplyGravity();
            Vector3 frameMovement = (horizontalMovement * moveSpeed) + (Vector3.up * verticalVelocity);

            if (characterController != null)
            {
                characterController.Move(frameMovement * Time.deltaTime);
            }
            else
            {
                transform.position += frameMovement * Time.deltaTime;
            }

            if (useExternalViewRotation || horizontalMovement.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(horizontalMovement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Aplica gravedad vertical de forma controlada para mantener estable el movimiento sobre superficies planas.
        /// </summary>
        private void ApplyGravity()
        {
            if (characterController != null && characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                return;
            }

            verticalVelocity += gravity * Time.deltaTime;
        }

        /// <summary>
        /// Evita que el objeto de jugador procese gravedad o input mientras esta en menu o lobby.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando la escena activa permite movimiento de gameplay.
        /// </returns>
        private static bool CanProcessGameplayMovement()
        {
            return SceneManager.GetActiveScene().name == GameSceneName;
        }

        /// <summary>
        /// Evita que un jugador marcado como fuera por el servidor continue desplazandose como participante activo.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando no existe estado de red o el jugador sigue vivo.
        /// </returns>
        private bool CanProcessAliveMovement()
        {
            networkPlayer = networkPlayer != null ? networkPlayer : GetComponent<NetworkPlayer>();
            return networkPlayer == null || networkPlayer.IsAlive;
        }

        /// <summary>
        /// Lee entrada de teclado compatible con Input System o con el Input Manager clasico de Unity.
        /// </summary>
        /// <returns>
        /// Vector 2D normalizado donde X representa izquierda/derecha y Y adelante/atras.
        /// </returns>
        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                Vector2 input = Vector2.zero;
                input.x += keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
                input.x -= keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
                input.y += keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
                input.y -= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
                return input.sqrMagnitude > 1f ? input.normalized : input;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
            return Vector2.zero;
#endif
        }
    }
}
