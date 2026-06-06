using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Crea y controla una camara Cinemachine en primera persona exclusiva del jugador propietario local.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerFirstPersonCamera : NetworkBehaviour
    {
        /// <summary>
        /// Nombre canonico de la escena donde se habilita la camara de gameplay.
        /// </summary>
        private const string GameSceneName = "GameScene";

        /// <summary>
        /// Nombre usado para identificar el pivote local de cabeza dentro del jugador.
        /// </summary>
        private const string HeadPivotName = "FirstPersonHeadPivot";

        /// <summary>
        /// Nombre usado para identificar la camara virtual Cinemachine local del jugador.
        /// </summary>
        private const string VirtualCameraName = "FirstPersonCinemachineCamera";

        /// <summary>
        /// Altura local del punto de vista respecto al centro de la capsula.
        /// </summary>
        [SerializeField] private float eyeHeight = 0.65f;

        /// <summary>
        /// Pequeno offset frontal para evitar que la camara quede exactamente en el centro geometrico de la capsula.
        /// </summary>
        [SerializeField] private float eyeForwardOffset = 0.05f;

        /// <summary>
        /// Sensibilidad angular del mouse en grados por unidad de delta.
        /// </summary>
        [SerializeField] private float mouseSensitivity = 0.12f;

        /// <summary>
        /// Angulo maximo hacia arriba y hacia abajo para evitar giros verticales imposibles.
        /// </summary>
        [SerializeField] private float maxPitchAngle = 80f;

        /// <summary>
        /// Campo de vision de la camara first-person.
        /// </summary>
        [SerializeField] private float fieldOfView = 75f;

        /// <summary>
        /// Prioridad alta para que esta camara gane sobre la camara global de prototipo.
        /// </summary>
        [SerializeField] private int cameraPriority = 100;

        /// <summary>
        /// Indica si el cursor debe bloquearse automaticamente al entrar en la escena de juego.
        /// </summary>
        [SerializeField] private bool lockCursorInGame = true;

        /// <summary>
        /// Indica si se ocultan los renderers del jugador local para que la camara no vea el interior de la capsula.
        /// </summary>
        [SerializeField] private bool hideLocalBody = true;

        /// <summary>
        /// Referencia al controlador de movimiento para activar rotacion externa mientras la camara esta viva.
        /// </summary>
        private PlayerController playerController;

        /// <summary>
        /// Pivote local que representa la cabeza del jugador y recibe el pitch vertical.
        /// </summary>
        private Transform headPivot;

        /// <summary>
        /// Camara virtual Cinemachine que conduce la camara real mediante CinemachineBrain.
        /// </summary>
        private CinemachineCamera firstPersonCamera;

        /// <summary>
        /// Componente de posicionamiento Cinemachine que mantiene la camara pegada al pivote de cabeza.
        /// </summary>
        private CinemachineHardLockToTarget hardLockToTarget;

        /// <summary>
        /// Renderers locales ocultados mientras la vista first-person esta activa.
        /// </summary>
        private Renderer[] localRenderers;

        /// <summary>
        /// Rotacion vertical acumulada de la mirada.
        /// </summary>
        private float pitch;

        /// <summary>
        /// Indica si el rig local de camara ya fue creado para esta instancia propietaria.
        /// </summary>
        private bool cameraRigCreated;

        /// <summary>
        /// Inicializa referencias locales del jugador.
        /// </summary>
        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }

        /// <summary>
        /// Destruye el rig local y restaura visuales cuando el objeto de red se despawnea.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            DestroyCameraRig();
        }

        /// <summary>
        /// Crea o destruye la camara segun la escena activa y procesa la mirada del owner local.
        /// </summary>
        private void Update()
        {
            if (!IsOwner)
            {
                DestroyCameraRig();
                return;
            }

            bool shouldUseFirstPersonCamera = SceneManager.GetActiveScene().name == GameSceneName;

            if (!shouldUseFirstPersonCamera)
            {
                DestroyCameraRig();
                return;
            }

            EnsureCameraRig();
            HandleCursorLock();
            HandleLookInput();
            SynchronizeVirtualCameraTransform();
        }

        /// <summary>
        /// Garantiza que existan el pivote de cabeza, la camara virtual y el CinemachineBrain de la camara real.
        /// </summary>
        private void EnsureCameraRig()
        {
            if (cameraRigCreated)
            {
                return;
            }

            playerController = playerController != null ? playerController : GetComponent<PlayerController>();
            playerController?.SetExternalRotationEnabled(true);

            EnsureHeadPivot();
            EnsureMainCameraBrain();
            CreateVirtualCamera();
            SetLocalBodyVisibility(false);
            cameraRigCreated = true;
        }

        /// <summary>
        /// Crea el pivote local de cabeza como hijo del jugador si aun no existe.
        /// </summary>
        private void EnsureHeadPivot()
        {
            if (headPivot != null)
            {
                return;
            }

            GameObject headPivotObject = new GameObject(HeadPivotName);
            headPivotObject.transform.SetParent(transform, false);
            headPivotObject.transform.localPosition = new Vector3(0f, eyeHeight, eyeForwardOffset);
            headPivotObject.transform.localRotation = Quaternion.identity;
            headPivot = headPivotObject.transform;
        }

        /// <summary>
        /// Asegura que la camara principal tenga CinemachineBrain para recibir la salida de la camara virtual.
        /// </summary>
        private static void EnsureMainCameraBrain()
        {
            Camera mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();

            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("PrototypeMainCamera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";

                if (FindAnyObjectByType<AudioListener>() == null)
                {
                    cameraObject.AddComponent<AudioListener>();
                }
            }

            if (mainCamera.GetComponent<CinemachineBrain>() == null)
            {
                mainCamera.gameObject.AddComponent<CinemachineBrain>();
            }
        }

        /// <summary>
        /// Crea la camara virtual Cinemachine local y la configura para seguir exactamente el pivote de cabeza.
        /// </summary>
        private void CreateVirtualCamera()
        {
            GameObject cameraObject = new GameObject($"{VirtualCameraName}_{OwnerClientId}");
            cameraObject.transform.SetPositionAndRotation(headPivot.position, headPivot.rotation);

            firstPersonCamera = cameraObject.AddComponent<CinemachineCamera>();
            firstPersonCamera.Priority = cameraPriority;
            firstPersonCamera.Target.TrackingTarget = headPivot;
            firstPersonCamera.Lens.FieldOfView = fieldOfView;

            hardLockToTarget = cameraObject.AddComponent<CinemachineHardLockToTarget>();
            hardLockToTarget.Damping = 0f;
        }

        /// <summary>
        /// Lee el mouse local y aplica yaw al cuerpo del jugador y pitch al pivote de cabeza.
        /// </summary>
        private void HandleLookInput()
        {
            Vector2 lookDelta = ReadLookDelta();

            if (lookDelta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float yawDelta = lookDelta.x * mouseSensitivity;
            float pitchDelta = lookDelta.y * mouseSensitivity;
            transform.Rotate(Vector3.up, yawDelta, Space.World);
            pitch = Mathf.Clamp(pitch - pitchDelta, -maxPitchAngle, maxPitchAngle);
            headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// Mantiene el GameObject de la camara virtual alineado con el pivote para que Cinemachine entregue la orientacion first-person.
        /// </summary>
        private void SynchronizeVirtualCameraTransform()
        {
            if (firstPersonCamera == null || headPivot == null)
            {
                return;
            }

            firstPersonCamera.transform.SetPositionAndRotation(headPivot.position, headPivot.rotation);
            firstPersonCamera.ForceCameraPosition(headPivot.position, headPivot.rotation);
        }

        /// <summary>
        /// Gestiona el bloqueo del cursor durante gameplay y permite liberarlo con Escape.
        /// </summary>
        private void HandleCursorLock()
        {
            if (!lockCursorInGame)
            {
                return;
            }

            if (WasEscapePressedThisFrame())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked && WasPrimaryButtonPressedThisFrame())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Destruye la camara virtual local y restaura el estado visual/control del jugador.
        /// </summary>
        private void DestroyCameraRig()
        {
            if (!cameraRigCreated && firstPersonCamera == null && headPivot == null)
            {
                return;
            }

            playerController = playerController != null ? playerController : GetComponent<PlayerController>();
            playerController?.SetExternalRotationEnabled(false);
            SetLocalBodyVisibility(true);

            if (firstPersonCamera != null)
            {
                Destroy(firstPersonCamera.gameObject);
            }

            if (headPivot != null)
            {
                Destroy(headPivot.gameObject);
            }

            firstPersonCamera = null;
            hardLockToTarget = null;
            headPivot = null;
            cameraRigCreated = false;
            pitch = 0f;

            if (lockCursorInGame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Oculta o muestra los renderers locales del jugador sin afectar como otros clientes ven esta capsula.
        /// </summary>
        /// <param name="isVisible">
        /// <see langword="true"/> para mostrar el cuerpo local; <see langword="false"/> para ocultarlo en primera persona.
        /// </param>
        private void SetLocalBodyVisibility(bool isVisible)
        {
            if (!hideLocalBody)
            {
                return;
            }

            localRenderers ??= GetComponentsInChildren<Renderer>(true);

            foreach (Renderer rendererComponent in localRenderers)
            {
                if (rendererComponent != null)
                {
                    rendererComponent.enabled = isVisible;
                }
            }
        }

        /// <summary>
        /// Lee el delta local de mouse usando Input System o el Input Manager clasico.
        /// </summary>
        /// <returns>
        /// Delta de mirada en unidades de input local.
        /// </returns>
        private static Vector2 ReadLookDelta()
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
        /// Detecta si Escape fue presionado este frame para liberar el cursor durante pruebas.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando Escape se presiono localmente.
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

        /// <summary>
        /// Detecta si el boton principal del mouse fue presionado para volver a bloquear el cursor.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el click principal se presiono localmente.
        /// </returns>
        private static bool WasPrimaryButtonPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }
}
