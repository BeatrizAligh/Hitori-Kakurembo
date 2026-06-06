using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HitoriKakurembo.PlayerSystem.Core
{
    /// <summary>
    /// Centraliza la lectura de input local del jugador.
    /// Este componente no mueve ni interactua por si mismo; solo expone valores normalizados para movimiento, camara e interaccion.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputHandler : NetworkBehaviour
    {
        /// <summary>
        /// Indica si se debe leer input aunque el objeto no este spawneado por Netcode, util para pruebas en escena vacia.
        /// </summary>
        [SerializeField] private bool allowInputWhenNotSpawned = true;

        /// <summary>
        /// Sensibilidad base aplicada al delta de mirada antes de entregarlo a otros componentes.
        /// </summary>
        [SerializeField] private Vector2 lookSensitivity = Vector2.one;

        /// <summary>
        /// Entrada de movimiento normalizada. X representa izquierda/derecha y Y adelante/atras.
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>
        /// Entrada de mirada del frame actual.
        /// </summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>
        /// Indica si el jugador presiono interactuar durante este frame.
        /// </summary>
        public bool InteractPressed { get; private set; }

        /// <summary>
        /// Indica si el jugador mantiene presionado correr.
        /// </summary>
        public bool RunHeld { get; private set; }

        /// <summary>
        /// Indica si el jugador presiono saltar durante este frame.
        /// </summary>
        public bool JumpPressed { get; private set; }

        /// <summary>
        /// Limpia y lee input cada frame solo para el owner, o para pruebas locales sin spawn de red.
        /// </summary>
        private void Update()
        {
            ResetFrameInput();

            if (!CanReadLocalInput())
            {
                return;
            }

            MoveInput = ReadMoveInput();
            LookInput = Vector2.Scale(ReadLookInput(), lookSensitivity);
            InteractPressed = ReadInteractPressed();
            RunHeld = ReadRunHeld();
            JumpPressed = ReadJumpPressed();
        }

        /// <summary>
        /// Define si este componente debe leer input en el estado actual de red.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el objeto pertenece al cliente local o cuando se permite test sin spawn.
        /// </returns>
        private bool CanReadLocalInput()
        {
            NetworkObject networkObject = NetworkObject;
            return networkObject == null || !networkObject.IsSpawned
                ? allowInputWhenNotSpawned
                : IsOwner;
        }

        /// <summary>
        /// Reinicia valores de input de frame para evitar que un no-owner conserve valores anteriores.
        /// </summary>
        private void ResetFrameInput()
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            InteractPressed = false;
            RunHeld = false;
            JumpPressed = false;
        }

        /// <summary>
        /// Lee input de movimiento compatible con Input System y con el Input Manager clasico.
        /// </summary>
        /// <returns>
        /// Vector de movimiento normalizado.
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
            Vector2 legacyInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            return legacyInput.sqrMagnitude > 1f ? legacyInput.normalized : legacyInput;
#else
            return Vector2.zero;
#endif
        }

        /// <summary>
        /// Lee el delta de mirada del mouse.
        /// </summary>
        /// <returns>
        /// Delta de mirada del frame actual.
        /// </returns>
        private static Vector2 ReadLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        /// <summary>
        /// Lee si el jugador presiono la accion de interaccion.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se presiono la tecla de interaccion.
        /// </returns>
        private static bool ReadInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        /// <summary>
        /// Lee si el jugador mantiene correr.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando correr esta presionado.
        /// </returns>
        private static bool ReadRunHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }

        /// <summary>
        /// Lee si el jugador presiono salto.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se presiono salto durante este frame.
        /// </returns>
        private static bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
