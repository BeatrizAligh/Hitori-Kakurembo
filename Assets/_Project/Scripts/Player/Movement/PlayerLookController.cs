using HitoriKakurembo.PlayerSystem.Core;
using HitoriKakurembo.PlayerSystem.Interfaces;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HitoriKakurembo.PlayerSystem.Movement
{
    /// <summary>
    /// Controla la mirada local del jugador y la camara asociada al owner.
    /// No implementa logica de gameplay; solo rota el cuerpo horizontalmente y limita la mirada vertical.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLookController : NetworkBehaviour, IPlayerControllable
    {
        /// <summary>
        /// Componente que entrega el input local centralizado.
        /// </summary>
        [SerializeField] private PlayerInputHandler inputHandler;

        /// <summary>
        /// Camara que debe quedar activa solo para el owner.
        /// </summary>
        [SerializeField] private Camera playerCamera;

        /// <summary>
        /// Transform que recibe el pitch vertical. Si no se asigna, se usa la camara.
        /// </summary>
        [SerializeField] private Transform pitchPivot;

        /// <summary>
        /// Sensibilidad horizontal en grados por unidad de input.
        /// </summary>
        [SerializeField] private float horizontalSensitivity = 0.12f;

        /// <summary>
        /// Sensibilidad vertical en grados por unidad de input.
        /// </summary>
        [SerializeField] private float verticalSensitivity = 0.12f;

        /// <summary>
        /// Limite vertical positivo y negativo de la mirada.
        /// </summary>
        [SerializeField] private float verticalLookLimit = 80f;

        /// <summary>
        /// Indica si este componente debe bloquear el cursor al tener control local.
        /// </summary>
        [SerializeField] private bool lockCursorForOwner = true;

        /// <summary>
        /// Indica si el control de mirada esta habilitado.
        /// </summary>
        private bool controlEnabled = true;

        /// <summary>
        /// Pitch acumulado de la camara.
        /// </summary>
        private float pitch;

        /// <summary>
        /// Transform que representa la direccion visual usada por movimiento.
        /// </summary>
        public Transform ViewTransform => playerCamera != null ? playerCamera.transform : transform;

        /// <summary>
        /// Indica si la mirada esta habilitada actualmente.
        /// </summary>
        public bool IsControlEnabled => controlEnabled;

        /// <summary>
        /// Cachea referencias locales al crearse el componente.
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// Configura la camara cuando Netcode spawnea el objeto.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            ConfigureCameraForOwnership();
        }

        /// <summary>
        /// Configura la camara tambien en escenas de prueba donde el objeto no esta spawneado.
        /// </summary>
        private void Start()
        {
            ConfigureCameraForOwnership();
        }

        /// <summary>
        /// Procesa mirada local solo para el owner o para pruebas sin spawn de red.
        /// </summary>
        private void Update()
        {
            if (!controlEnabled || !CanProcessOwnerInput())
            {
                return;
            }

            HandleCursorLock();
            ApplyLook(inputHandler != null ? inputHandler.LookInput : Vector2.zero);
        }

        /// <summary>
        /// Habilita el control de mirada.
        /// </summary>
        public void EnableControl()
        {
            controlEnabled = true;
        }

        /// <summary>
        /// Deshabilita el control de mirada.
        /// </summary>
        public void DisableControl()
        {
            controlEnabled = false;
        }

        /// <summary>
        /// Habilita la mirada con un nombre explicito para sistemas externos.
        /// </summary>
        public void EnableLook()
        {
            EnableControl();
        }

        /// <summary>
        /// Deshabilita la mirada con un nombre explicito para sistemas externos.
        /// </summary>
        public void DisableLook()
        {
            DisableControl();
        }

        /// <summary>
        /// Cachea referencias del mismo prefab sin usar busquedas globales.
        /// </summary>
        public void CacheReferences()
        {
            inputHandler = inputHandler != null ? inputHandler : GetComponent<PlayerInputHandler>();
            playerCamera = playerCamera != null ? playerCamera : GetComponentInChildren<Camera>(true);
            pitchPivot = pitchPivot != null ? pitchPivot : (playerCamera != null ? playerCamera.transform : null);
        }

        /// <summary>
        /// Determina si este componente debe responder al input local.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> para owner, o para prueba local sin NetworkObject spawneado.
        /// </returns>
        private bool CanProcessOwnerInput()
        {
            NetworkObject networkObject = NetworkObject;
            return networkObject == null || !networkObject.IsSpawned || IsOwner;
        }

        /// <summary>
        /// Activa la camara solo en owner para evitar multiples camaras/audio listeners en clientes remotos.
        /// </summary>
        private void ConfigureCameraForOwnership()
        {
            CacheReferences();

            bool shouldEnableCamera = CanProcessOwnerInput();

            if (playerCamera != null)
            {
                playerCamera.enabled = shouldEnableCamera;
            }

            AudioListener audioListener = playerCamera != null ? playerCamera.GetComponent<AudioListener>() : null;

            if (audioListener != null)
            {
                audioListener.enabled = shouldEnableCamera;
            }
        }

        /// <summary>
        /// Aplica yaw al cuerpo del jugador y pitch al pivote de camara.
        /// </summary>
        /// <param name="lookInput">
        /// Delta de mirada del frame actual.
        /// </param>
        private void ApplyLook(Vector2 lookInput)
        {
            if (lookInput.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float yawDelta = lookInput.x * horizontalSensitivity;
            float pitchDelta = lookInput.y * verticalSensitivity;
            transform.Rotate(Vector3.up, yawDelta, Space.World);

            if (pitchPivot == null)
            {
                return;
            }

            pitch = Mathf.Clamp(pitch - pitchDelta, -verticalLookLimit, verticalLookLimit);
            pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// Bloquea el cursor mientras el owner controla la camara y permite liberarlo con Escape durante pruebas.
        /// </summary>
        private void HandleCursorLock()
        {
            if (!lockCursorForOwner)
            {
                return;
            }

            if (WasEscapePressedThisFrame())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Detecta si Escape fue presionado este frame para liberar el cursor en Play Mode.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando Escape fue presionado localmente.
        /// </returns>
        private static bool WasEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
