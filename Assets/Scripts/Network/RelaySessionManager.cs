using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HitoriKakurembo.Core;
using HitoriKakurembo.Rounds;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitoriKakurembo.Network
{
    /// <summary>
    /// Orquesta el flujo de sesion del prototipo: autenticacion anonima, creacion o union por codigo Relay, lobby y arranque de la escena de juego.
    /// </summary>
    public class RelaySessionManager : MonoBehaviour
    {
        /// <summary>
        /// Nombre canonico de la escena de menu principal.
        /// </summary>
        private const string MainMenuSceneName = "MainMenu";

        /// <summary>
        /// Nombre canonico de la escena de lobby.
        /// </summary>
        private const string LobbySceneName = "LobbyScene";

        /// <summary>
        /// Nombre canonico de la escena principal de juego.
        /// </summary>
        private const string GameSceneName = "GameScene";

        /// <summary>
        /// Nombre del objeto persistente que contiene NetworkManager, Unity Transport y managers de red del prototipo.
        /// </summary>
        private const string NetworkRuntimeObjectName = "HK_NetworkRuntime";

        /// <summary>
        /// Mensaje NGO usado por el servidor para publicar una fotografia simple de jugadores visibles en el lobby.
        /// </summary>
        private const string LobbySnapshotMessageName = "HK_LobbySnapshot";

        /// <summary>
        /// Mensaje NGO usado por un cliente para pedir al servidor una fotografia actualizada del lobby.
        /// </summary>
        private const string LobbySnapshotRequestMessageName = "HK_LobbySnapshotRequest";

        /// <summary>
        /// Mensaje NGO usado por un cliente para pedir al servidor cambiar su estado ready dentro del lobby.
        /// </summary>
        private const string LobbyReadyRequestMessageName = "HK_LobbyReadyRequest";

        /// <summary>
        /// Capacidad maxima del paquete de lobby; alcanza para los 6 jugadores previstos y sus nombres visibles.
        /// </summary>
        private const int LobbySnapshotWriterCapacity = 1024;

        /// <summary>
        /// Intervalo de reintento usado por clientes que aun no recibieron una fotografia completa del lobby.
        /// </summary>
        private const float LobbySnapshotRequestRetryInterval = 1f;

        /// <summary>
        /// Nombre por defecto usado cuando un jugador no ingresa un alias.
        /// </summary>
        private const string DefaultPlayerName = "Jugador";

        /// <summary>
        /// Cantidad maxima de jugadores que se permite en la sesion Relay del prototipo.
        /// </summary>
        [SerializeField] private int maximumPlayers = 6;

        /// <summary>
        /// Cantidad minima de jugadores requerida para habilitar el boton de inicio de partida en el lobby.
        /// </summary>
        [SerializeField] private int minimumPlayersToStart = 2;

        /// <summary>
        /// Tipo de conexion usado al construir el <see cref="RelayServerData"/> de Unity Transport.
        /// </summary>
        [SerializeField] private string relayConnectionType = "udp";

        /// <summary>
        /// Referencia cacheada al <see cref="NetworkManager"/> persistente de la sesion.
        /// </summary>
        private NetworkManager networkManager;

        /// <summary>
        /// Referencia cacheada al transporte Unity Transport usado por NGO.
        /// </summary>
        private UnityTransport unityTransport;

        /// <summary>
        /// Referencia cacheada al bootstrap de red que expone los arranques Host, Client y Server.
        /// </summary>
        private NetworkBootstrap networkBootstrap;

        /// <summary>
        /// Referencia cacheada al manager que registra jugadores de red conectados.
        /// </summary>
        private NetworkGameManager networkGameManager;

        /// <summary>
        /// Referencia cacheada al manager liviano de conexiones activas.
        /// </summary>
        private PlayerConnectionManager playerConnectionManager;

        /// <summary>
        /// Plantilla runtime usada como prefab base del jugador de red.
        /// </summary>
        private GameObject runtimePlayerPrefab;

        /// <summary>
        /// Handler que instancia jugadores activos a partir de la plantilla runtime registrada en NGO.
        /// </summary>
        private RuntimeNetworkPlayerPrefabHandler runtimePlayerPrefabHandler;

        /// <summary>
        /// Diccionario de nombres aprobados por client id para inicializar correctamente cada <see cref="NetworkPlayer"/>.
        /// </summary>
        private readonly Dictionary<ulong, string> approvedPlayerNames = new Dictionary<ulong, string>();

        /// <summary>
        /// Cache local de jugadores visibles en lobby recibida desde el servidor para que clientes y host muestren la misma lista.
        /// </summary>
        private readonly Dictionary<ulong, LobbyPlayerSnapshot> visibleLobbyPlayers = new Dictionary<ulong, LobbyPlayerSnapshot>();

        /// <summary>
        /// Estado ready autoritativo por client id; permite validar el lobby aunque un NetworkPlayer tarde en registrarse.
        /// </summary>
        private readonly Dictionary<ulong, bool> lobbyReadyStates = new Dictionary<ulong, bool>();

        /// <summary>
        /// Indica si los servicios de Unity ya fueron inicializados satisfactoriamente.
        /// </summary>
        private bool servicesInitialized;

        /// <summary>
        /// Indica si el host ya cerro el lobby e inicio la transicion hacia la partida.
        /// </summary>
        private bool matchInProgress;

        /// <summary>
        /// Indica si este manager ya se suscribio a los callbacks del <see cref="NetworkManager"/> actual.
        /// </summary>
        private bool callbacksRegistered;

        /// <summary>
        /// Indica si ya se registraron los mensajes personalizados usados para sincronizar la lista de lobby.
        /// </summary>
        private bool lobbyMessageHandlersRegistered;

        /// <summary>
        /// Instancia concreta de mensajeria NGO sobre la que se registraron los handlers de lobby.
        /// </summary>
        private CustomMessagingManager registeredLobbyMessagingManager;

        /// <summary>
        /// Proximo tiempo local en el que el cliente puede volver a pedir la fotografia de lobby al servidor.
        /// </summary>
        private float nextLobbySnapshotRequestTime;

        /// <summary>
        /// Evita reaccionar al callback de desconexion local cuando el cierre fue solicitado intencionalmente por la propia aplicacion.
        /// </summary>
        private bool suppressLocalDisconnectHandling;

        /// <summary>
        /// Codigo de union Relay de la sesion actual cuando esta instancia actua como host.
        /// </summary>
        public string JoinCode { get; private set; } = string.Empty;

        /// <summary>
        /// Ultimo nombre local confirmado para el cliente actual.
        /// </summary>
        public string LocalPlayerName { get; private set; } = DefaultPlayerName;

        /// <summary>
        /// Ultimo mensaje informativo de estado producido por el flujo de sesion.
        /// </summary>
        public string LastStatusMessage { get; private set; } = "Listo para crear o unirse a una partida.";

        /// <summary>
        /// Ultimo mensaje de error producido por el flujo de sesion.
        /// </summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>
        /// Indica si existe una operacion asincronica activa que aun no termina.
        /// </summary>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Indica si esta instancia local actua actualmente como host.
        /// </summary>
        public bool IsHost => networkManager != null && networkManager.IsHost;

        /// <summary>
        /// Indica si esta instancia local actua actualmente como cliente.
        /// </summary>
        public bool IsClient => networkManager != null && networkManager.IsClient;

        /// <summary>
        /// Indica si esta instancia local actua actualmente como servidor.
        /// </summary>
        public bool IsServer => networkManager != null && networkManager.IsServer;

        /// <summary>
        /// Indica si la sesion NGO ya se encuentra escuchando o conectada.
        /// </summary>
        public bool IsInSession => networkManager != null && networkManager.IsListening;

        /// <summary>
        /// Evento emitido cuando cambia algun aspecto visible del flujo de sesion y la interfaz debe refrescarse.
        /// </summary>
        public event Action SessionStateChanged;

        /// <summary>
        /// Representa una entrada autoritativa del lobby preparada por el servidor para UI y validaciones de inicio.
        /// </summary>
        private readonly struct LobbyPlayerSnapshot
        {
            /// <summary>
            /// Client id propietario del jugador dentro de Netcode.
            /// </summary>
            public readonly ulong ClientId;

            /// <summary>
            /// Indice cero-basado asignado por el servidor para ordenar el lobby.
            /// </summary>
            public readonly int PlayerIndex;

            /// <summary>
            /// Nombre visible aprobado por el servidor para este jugador.
            /// </summary>
            public readonly string PlayerName;

            /// <summary>
            /// Indica si el jugador ya confirmo que esta listo.
            /// </summary>
            public readonly bool IsReady;

            /// <summary>
            /// Indica si el jugador sigue vivo en la ronda actual.
            /// </summary>
            public readonly bool IsAlive;

            /// <summary>
            /// Puntos obtenidos por este jugador durante la ronda actual.
            /// </summary>
            public readonly int CurrentRoundScore;

            /// <summary>
            /// Crea una entrada inmutable de lobby con datos ya validados por la autoridad.
            /// </summary>
            /// <param name="clientId">
            /// Client id propietario del jugador.
            /// </param>
            /// <param name="playerIndex">
            /// Indice asignado dentro del lobby.
            /// </param>
            /// <param name="playerName">
            /// Nombre visible aprobado.
            /// </param>
            /// <param name="isReady">
            /// Estado ready actual.
            /// </param>
            /// <param name="isAlive">
            /// Estado de vida actual.
            /// </param>
            /// <param name="currentRoundScore">
            /// Puntos obtenidos durante la ronda actual.
            /// </param>
            public LobbyPlayerSnapshot(ulong clientId, int playerIndex, string playerName, bool isReady, bool isAlive, int currentRoundScore)
            {
                ClientId = clientId;
                PlayerIndex = playerIndex;
                PlayerName = playerName;
                IsReady = isReady;
                IsAlive = isAlive;
                CurrentRoundScore = currentRoundScore;
            }
        }

        /// <summary>
        /// Registra el manager como servicio y asegura que la infraestructura de red persistente exista desde el arranque.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<RelaySessionManager>(this);
            EnsureNetworkInfrastructure();
        }

        /// <summary>
        /// Se suscribe a eventos de escena y de red para mantener sincronizado el flujo.
        /// </summary>
        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RefreshNetworkCallbacksSubscription();
        }

        /// <summary>
        /// Cancela las suscripciones activas cuando el manager se desactiva.
        /// </summary>
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeFromNetworkCallbacks();
        }

        /// <summary>
        /// Reintenta suscribirse a callbacks cuando la infraestructura fue creada despues de la activacion inicial del componente.
        /// </summary>
        private void Update()
        {
            if (!callbacksRegistered)
            {
                RefreshNetworkCallbacksSubscription();
            }

            RegisterLobbyMessageHandlers();

            if (ShouldRetryLobbySnapshotRequest())
            {
                nextLobbySnapshotRequestTime = Time.unscaledTime + LobbySnapshotRequestRetryInterval;
                SendLobbySnapshotRequestToServer();
            }
        }

        /// <summary>
        /// Crea una sesion Relay como host, genera el codigo de union y carga el lobby sincronizado.
        /// </summary>
        /// <param name="requestedPlayerName">
        /// Alias que desea usar el jugador local.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la sesion pudo crearse correctamente; en caso contrario, <see langword="false"/>.
        /// </returns>
        public async Task<bool> CreateRelaySessionAsync(string requestedPlayerName)
        {
            if (!TryBeginOperation("Creando partida..."))
            {
                return false;
            }

            try
            {
                EnsureNetworkInfrastructure();
                ShutdownNetworkSession();
                SetLocalPlayerName(requestedPlayerName);

                if (!await InitializeServicesIfNeededAsync())
                {
                    return false;
                }

                PrepareConnectionPayload(LocalPlayerName);
                approvedPlayerNames.Clear();
                lobbyReadyStates.Clear();
                matchInProgress = false;

                int maxConnections = Mathf.Max(1, GetMaximumPlayerCount() - 1);
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                ConfigureRelayHostTransport(allocation);
                networkBootstrap.UseRelayTransport(true);

                if (!networkBootstrap.StartHost())
                {
                    SetError("No se pudo iniciar la sesion como host.");
                    return false;
                }

                approvedPlayerNames[networkManager.LocalClientId] = LocalPlayerName;
                lobbyReadyStates[networkManager.LocalClientId] = false;
                SetStatus($"Partida creada. Comparte el codigo {JoinCode} con el resto del grupo.");
                ResolveSceneLoader()?.LoadLobby();
                return true;
            }
            catch (Exception exception)
            {
                SetError($"No fue posible crear la partida Relay. {exception.Message}");
                return false;
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Se une a una sesion Relay existente usando el codigo compartido por el host.
        /// </summary>
        /// <param name="requestedPlayerName">
        /// Alias que desea usar el jugador local.
        /// </param>
        /// <param name="joinCode">
        /// Codigo Relay generado previamente por el host.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el cliente pudo iniciar la conexion; en caso contrario, <see langword="false"/>.
        /// </returns>
        public async Task<bool> JoinRelaySessionAsync(string requestedPlayerName, string joinCode)
        {
            if (!TryBeginOperation("Uniendose a la partida..."))
            {
                return false;
            }

            try
            {
                EnsureNetworkInfrastructure();
                ShutdownNetworkSession();
                SetLocalPlayerName(requestedPlayerName);

                string normalizedJoinCode = NormalizeJoinCode(joinCode);

                if (string.IsNullOrWhiteSpace(normalizedJoinCode))
                {
                    SetError("Debes ingresar un codigo de partida valido para unirte.");
                    return false;
                }

                if (!await InitializeServicesIfNeededAsync())
                {
                    return false;
                }

                PrepareConnectionPayload(LocalPlayerName);
                JoinCode = normalizedJoinCode;

                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(normalizedJoinCode);
                ConfigureRelayClientTransport(joinAllocation);
                networkBootstrap.UseRelayTransport(true);

                if (!networkBootstrap.StartClient())
                {
                    SetError("No se pudo iniciar el cliente Relay.");
                    return false;
                }

                SetStatus("Conectando con el lobby del host...");
                return true;
            }
            catch (Exception exception)
            {
                SetError($"No fue posible unirse a la partida Relay. {exception.Message}");
                return false;
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Inicia la partida desde el lobby y ordena al host cargar la escena principal de juego para todos los clientes.
        /// </summary>
        public void StartGameFromLobby()
        {
            if (!CanStartMatch())
            {
                SetError(GetMatchStartBlockReason());
                return;
            }

            matchInProgress = true;
            SetStatus("Iniciando partida...");
            SendLobbySnapshotToAllClients();
            ResolveSceneLoader()?.LoadGame();
        }

        /// <summary>
        /// Cierra la sesion actual y regresa al menu principal local.
        /// </summary>
        public void LeaveSessionAndReturnToMenu()
        {
            ShutdownNetworkSession();
            SetStatus("Sesion cerrada. Has vuelto al menu principal.");
            ResolveSceneLoader()?.LoadMainMenu();
        }

        /// <summary>
        /// Solicita cambiar el estado ready del jugador local desde la UI del lobby.
        /// </summary>
        /// <param name="isReady">
        /// Nuevo estado ready que el jugador local desea aplicar.
        /// </param>
        public void SetLocalReadyState(bool isReady)
        {
            if (!IsClient || SceneManager.GetActiveScene().name != LobbySceneName)
            {
                SetError("Solo puedes cambiar tu estado ready dentro del lobby.");
                return;
            }

            NetworkPlayer localPlayer = GetLocalPlayer();

            if (localPlayer == null)
            {
                SendReadyStateRequestToServer(isReady);
            }
            else
            {
                localPlayer.SubmitReadyState(isReady);
            }

            SetStatus(isReady ? "Marcaste tu estado como listo." : "Marcaste tu estado como no listo.");
        }

        /// <summary>
        /// Alterna el estado ready del jugador local usando el valor sincronizado actual como referencia.
        /// </summary>
        public void ToggleLocalReadyState()
        {
            SetLocalReadyState(!IsLocalPlayerReady());
        }

        /// <summary>
        /// Indica si el jugador local ya marco ready en el lobby.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el jugador local esta listo; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool IsLocalPlayerReady()
        {
            if (networkManager != null
                && visibleLobbyPlayers.TryGetValue(networkManager.LocalClientId, out LobbyPlayerSnapshot snapshot))
            {
                return snapshot.IsReady;
            }

            NetworkPlayer localPlayer = GetLocalPlayer();

            if (localPlayer != null)
            {
                return localPlayer.IsReady;
            }

            return false;
        }

        /// <summary>
        /// Determina si la UI local puede permitir al jugador alternar su estado ready.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el jugador esta conectado, en lobby y la partida aun no comenzo.
        /// </returns>
        public bool CanToggleLocalReady()
        {
            return IsClient
                && !IsBusy
                && !matchInProgress
                && SceneManager.GetActiveScene().name == LobbySceneName
                && IsInSession;
        }

        /// <summary>
        /// Valida en servidor si se aceptan cambios de ready en este momento.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el lobby sigue abierto; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool CanChangeReadyStateOnServer()
        {
            return IsServer
                && !matchInProgress
                && SceneManager.GetActiveScene().name == LobbySceneName;
        }

        /// <summary>
        /// Consulta el estado ready autoritativo que el servidor tiene guardado para un cliente.
        /// </summary>
        /// <param name="clientId">
        /// Client id cuyo estado ready se desea consultar.
        /// </param>
        /// <param name="fallbackValue">
        /// Valor devuelto cuando todavia no existe una entrada autoritativa.
        /// </param>
        /// <returns>
        /// Estado ready guardado para el cliente, o el fallback indicado.
        /// </returns>
        public bool GetLobbyReadyStateForClient(ulong clientId, bool fallbackValue)
        {
            return GetReadyStateForClient(clientId, fallbackValue);
        }

        /// <summary>
        /// Notifica que un dato sincronizado de jugador cambio y que el servidor debe publicar un nuevo snapshot de lobby.
        /// </summary>
        public void NotifyLobbyPlayerStateChanged()
        {
            if (!IsServer)
            {
                return;
            }

            StartCoroutine(SendLobbySnapshotNextFrame());
        }

        /// <summary>
        /// Devuelve el nombre aprobado para el client id indicado.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente cuyo nombre se desea consultar.
        /// </param>
        /// <returns>
        /// Nombre aprobado para el cliente, o un alias por defecto si aun no existe uno registrado.
        /// </returns>
        public string GetApprovedPlayerName(ulong clientId)
        {
            return approvedPlayerNames.TryGetValue(clientId, out string playerName)
                ? playerName
                : NormalizePlayerName(string.Empty, clientId);
        }

        /// <summary>
        /// Obtiene la cantidad de jugadores conectados que esta sesion reconoce actualmente.
        /// </summary>
        /// <returns>
        /// Numero de jugadores conectados.
        /// </returns>
        public int GetConnectedPlayerCount()
        {
            if (networkManager != null && networkManager.IsListening && networkManager.IsServer)
            {
                return networkManager.ConnectedClientsIds.Count;
            }

            if (visibleLobbyPlayers.Count > 0)
            {
                return visibleLobbyPlayers.Count;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();
            playerConnectionManager = playerConnectionManager ?? FindAnyObjectByType<PlayerConnectionManager>();

            if (networkGameManager != null && networkGameManager.ConnectedClientCount > 0)
            {
                return networkGameManager.ConnectedClientCount;
            }

            return playerConnectionManager != null ? playerConnectionManager.ConnectedCount : 0;
        }

        /// <summary>
        /// Construye la lista visible de jugadores para el lobby o la pantalla de juego.
        /// </summary>
        /// <returns>
        /// Lista nueva con los nombres visibles de los jugadores conectados.
        /// </returns>
        public IReadOnlyList<string> GetLobbyPlayerDisplayNames()
        {
            if (visibleLobbyPlayers.Count > 0)
            {
                return visibleLobbyPlayers
                    .OrderBy(entry => entry.Value.PlayerIndex)
                    .ThenBy(entry => entry.Key)
                    .Select(entry => FormatLobbyPlayerSnapshot(entry.Value))
                    .ToList();
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager != null)
            {
                List<string> playerNames = networkGameManager.GetPlayersSnapshot()
                    .Where(player => player != null)
                    .OrderBy(player => player.PlayerIndex)
                    .ThenBy(player => player.PlayerId)
                    .Select(player => player.IsDoll
                        ? $"{player.PlayerName} [Muneco]"
                        : $"{player.PlayerName} [{(player.IsReady ? "Listo" : "Esperando")}]")
                    .ToList();

                if (playerNames.Count > 0)
                {
                    return playerNames;
                }
            }

            return approvedPlayerNames
                .OrderBy(entry => entry.Key)
                .Select(entry => entry.Value)
                .ToList();
        }

        /// <summary>
        /// Resuelve el objeto de jugador perteneciente al cliente local.
        /// </summary>
        /// <returns>
        /// <see cref="NetworkPlayer"/> local, o <see langword="null"/> si aun no existe.
        /// </returns>
        public NetworkPlayer GetLocalPlayer()
        {
            if (networkManager == null)
            {
                return null;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();
            return networkGameManager != null ? networkGameManager.GetPlayer(networkManager.LocalClientId) : null;
        }

        /// <summary>
        /// Determina si el host tiene autorizacion y jugadores suficientes para iniciar la partida desde el lobby.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando la partida puede comenzar; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool CanStartMatch()
        {
            return !IsBusy
                && IsHost
                && !matchInProgress
                && SceneManager.GetActiveScene().name == LobbySceneName
                && GetConnectedPlayerCount() >= GetMinimumPlayerCount()
                && GetConnectedPlayerCount() <= GetMaximumPlayerCount()
                && AreAllConnectedPlayersReady();
        }

        /// <summary>
        /// Crea o reutiliza la infraestructura persistente de red que NGO necesita para operar en cualquier escena.
        /// </summary>
        private void EnsureNetworkInfrastructure()
        {
            networkManager = ResolveNetworkManager();

            if (networkManager == null)
            {
                GameObject networkRoot = new GameObject(NetworkRuntimeObjectName);
                DontDestroyOnLoad(networkRoot);
                networkRoot.SetActive(false);

                unityTransport = networkRoot.AddComponent<UnityTransport>();
                networkManager = networkRoot.AddComponent<NetworkManager>();
                EnsureNetworkConfig();
                networkBootstrap = networkRoot.AddComponent<NetworkBootstrap>();
                networkGameManager = networkRoot.AddComponent<NetworkGameManager>();
                playerConnectionManager = networkRoot.AddComponent<PlayerConnectionManager>();

                ConfigureNetworkManager();
                EnsurePlayerPrefabConfigured();
                networkRoot.SetActive(true);
                return;
            }

            DontDestroyOnLoad(networkManager.gameObject);
            EnsureNetworkConfig();
            unityTransport = networkManager.GetComponent<UnityTransport>() ?? networkManager.gameObject.AddComponent<UnityTransport>();
            networkBootstrap = networkManager.GetComponent<NetworkBootstrap>() ?? networkManager.gameObject.AddComponent<NetworkBootstrap>();
            networkGameManager = networkManager.GetComponent<NetworkGameManager>() ?? networkManager.gameObject.AddComponent<NetworkGameManager>();
            playerConnectionManager = networkManager.GetComponent<PlayerConnectionManager>() ?? networkManager.gameObject.AddComponent<PlayerConnectionManager>();

            ConfigureNetworkManager();
            EnsurePlayerPrefabConfigured();
        }

        /// <summary>
        /// Aplica la configuracion de NGO necesaria para esta fase del prototipo.
        /// </summary>
        private void ConfigureNetworkManager()
        {
            if (networkManager == null)
            {
                return;
            }

            unityTransport = unityTransport != null
                ? unityTransport
                : networkManager.GetComponent<UnityTransport>() ?? networkManager.gameObject.AddComponent<UnityTransport>();

            EnsureNetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.ForceSamePrefabs = false;
            networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = true;
            networkManager.NetworkConfig.ClientConnectionBufferTimeout = 20;
            networkManager.ConnectionApprovalCallback = null;
            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
        }

        /// <summary>
        /// Registra el prefab runtime del jugador para que NGO pueda crear una instancia por cliente conectado.
        /// </summary>
        private void EnsurePlayerPrefabConfigured()
        {
            if (networkManager == null)
            {
                return;
            }

            EnsureNetworkConfig();

            if (runtimePlayerPrefab == null)
            {
                runtimePlayerPrefab = RuntimeNetworkPlayerFactory.CreatePlayerPrefab();
                DontDestroyOnLoad(runtimePlayerPrefab);
            }

            networkManager.NetworkConfig.PlayerPrefab = runtimePlayerPrefab;

            if (!networkManager.NetworkConfig.Prefabs.Contains(runtimePlayerPrefab))
            {
                networkManager.AddNetworkPrefab(runtimePlayerPrefab);
            }

            RegisterRuntimePlayerPrefabHandler();
        }

        /// <summary>
        /// Registra un instanciador custom para evitar que las copias del prefab runtime nazcan desactivadas.
        /// </summary>
        private void RegisterRuntimePlayerPrefabHandler()
        {
            if (networkManager == null || networkManager.PrefabHandler == null || runtimePlayerPrefab == null)
            {
                return;
            }

            runtimePlayerPrefabHandler = new RuntimeNetworkPlayerPrefabHandler(runtimePlayerPrefab);
            networkManager.PrefabHandler.RemoveHandler(runtimePlayerPrefab);
            networkManager.PrefabHandler.AddHandler(runtimePlayerPrefab, runtimePlayerPrefabHandler);
        }

        /// <summary>
        /// Inicializa Unity Services y autentica al jugador local de manera anonima cuando aun no existe sesion autenticada.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando los servicios quedaron listos; en caso contrario, <see langword="false"/>.
        /// </returns>
        private async Task<bool> InitializeServicesIfNeededAsync()
        {
            try
            {
                if (!servicesInitialized)
                {
                    await UnityServices.InitializeAsync();
                    servicesInitialized = true;
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                return true;
            }
            catch (Exception exception)
            {
                SetError($"No fue posible inicializar Unity Services. {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prepara el payload de conexion que el cliente enviara al host durante la aprobacion.
        /// </summary>
        /// <param name="playerName">
        /// Nombre local que debe serializarse dentro del payload.
        /// </param>
        private void PrepareConnectionPayload(string playerName)
        {
            if (networkManager == null)
            {
                return;
            }

            EnsureNetworkConfig();
            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(NormalizePlayerName(playerName));
        }

        /// <summary>
        /// Traduce una asignacion Relay de host a la configuracion concreta que Unity Transport necesita para abrir la sesion.
        /// </summary>
        /// <param name="allocation">
        /// Asignacion Relay creada para el host actual.
        /// </param>
        private void ConfigureRelayHostTransport(Allocation allocation)
        {
            if (unityTransport == null)
            {
                throw new InvalidOperationException("Unity Transport no esta disponible para configurar la sesion Relay del host.");
            }

            RelayServerEndpoint endpoint = SelectRelayEndpoint(allocation.ServerEndpoints);
            unityTransport.SetHostRelayData(
                endpoint.Host,
                (ushort)endpoint.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                endpoint.Secure);
        }

        /// <summary>
        /// Traduce una asignacion Relay de cliente a la configuracion concreta que Unity Transport necesita para unirse al host.
        /// </summary>
        /// <param name="allocation">
        /// Asignacion Relay resuelta a partir del codigo de union.
        /// </param>
        private void ConfigureRelayClientTransport(JoinAllocation allocation)
        {
            if (unityTransport == null)
            {
                throw new InvalidOperationException("Unity Transport no esta disponible para configurar la sesion Relay del cliente.");
            }

            RelayServerEndpoint endpoint = SelectRelayEndpoint(allocation.ServerEndpoints);
            unityTransport.SetClientRelayData(
                endpoint.Host,
                (ushort)endpoint.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData,
                endpoint.Secure);
        }

        /// <summary>
        /// Maneja la aprobacion de un nuevo cliente y decide el nombre y posicion inicial del jugador que NGO va a spawnear.
        /// </summary>
        /// <param name="request">
        /// Payload de conexion enviado por el cliente.
        /// </param>
        /// <param name="response">
        /// Respuesta que el servidor debe completar con la aprobacion final.
        /// </param>
        private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (matchInProgress || SceneManager.GetActiveScene().name == GameSceneName)
            {
                RejectConnection(response, "La partida ya comenzo. No se permiten nuevas conexiones.");
                return;
            }

            if (networkManager != null && networkManager.ConnectedClientsIds.Count >= GetMaximumPlayerCount())
            {
                RejectConnection(response, "La sala esta llena.");
                return;
            }

            string approvedName = NormalizePlayerName(DecodePlayerName(request.Payload), request.ClientNetworkId);
            approvedPlayerNames[request.ClientNetworkId] = approvedName;
            lobbyReadyStates[request.ClientNetworkId] = false;

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;
            response.Reason = string.Empty;
            response.Position = GetPlayerSpawnPosition(request.ClientNetworkId);
            response.Rotation = Quaternion.identity;
        }

        /// <summary>
        /// Se suscribe a callbacks del network manager actual para mantener el estado del flujo siempre actualizado.
        /// </summary>
        private void RefreshNetworkCallbacksSubscription()
        {
            if (callbacksRegistered)
            {
                return;
            }

            EnsureNetworkInfrastructure();

            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            callbacksRegistered = true;
            RegisterLobbyMessageHandlers();
        }

        /// <summary>
        /// Elimina las suscripciones activas al network manager.
        /// </summary>
        private void UnsubscribeFromNetworkCallbacks()
        {
            if (!callbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            callbacksRegistered = false;
            UnregisterLobbyMessageHandlers();
        }

        /// <summary>
        /// Refresca la interfaz cuando NGO confirma una nueva conexion.
        /// </summary>
        /// <param name="clientId">
        /// Cliente que termino de conectarse.
        /// </param>
        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager != null && clientId == networkManager.LocalClientId)
            {
                SetStatus(IsHost
                    ? $"Lobby activo. Comparte el codigo {JoinCode}."
                    : "Conexion completada. Esperando sincronizacion del host...");

                if (IsClient && !IsServer)
                {
                    SendLobbySnapshotRequestToServer();
                }
            }

            if (IsServer)
            {
                StartCoroutine(SendLobbySnapshotNextFrame());
            }

            NotifySessionStateChanged();
        }

        /// <summary>
        /// Reacciona a desconexiones remotas y locales, devolviendo al menu cuando el cliente actual pierde la sesion.
        /// </summary>
        /// <param name="clientId">
        /// Cliente que NGO reporto como desconectado.
        /// </param>
        private void HandleClientDisconnected(ulong clientId)
        {
            if (networkManager == null || clientId != networkManager.LocalClientId)
            {
                if (IsServer)
                {
                    approvedPlayerNames.Remove(clientId);
                    lobbyReadyStates.Remove(clientId);
                    visibleLobbyPlayers.Remove(clientId);
                    StartCoroutine(SendLobbySnapshotNextFrame());
                }

                NotifySessionStateChanged();
                return;
            }

            if (suppressLocalDisconnectHandling)
            {
                NotifySessionStateChanged();
                return;
            }

            string disconnectReason = string.IsNullOrWhiteSpace(networkManager.DisconnectReason)
                ? "La sesion de red se cerro."
                : networkManager.DisconnectReason;

            ResetSessionState();
            SetError(disconnectReason);

            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                ResolveSceneLoader()?.LoadMainMenu();
            }
        }

        /// <summary>
        /// Reacciona a cargas de escena para completar el paso de lobby o arrancar la primera ronda del prototipo.
        /// </summary>
        /// <param name="scene">
        /// Escena recien cargada.
        /// </param>
        /// <param name="loadSceneMode">
        /// Modo de carga utilizado.
        /// </param>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (!scene.IsValid())
            {
                return;
            }

            if (scene.name == GameSceneName && IsServer)
            {
                matchInProgress = true;
                StartCoroutine(BeginGameSceneFlowNextFrame());
                return;
            }

            if (scene.name == LobbySceneName && IsHost)
            {
                matchInProgress = false;
                SetStatus($"Esperando jugadores en el lobby. Codigo: {JoinCode}");
                SendLobbySnapshotToAllClients();
                return;
            }

            if (scene.name == LobbySceneName && IsClient && !IsServer)
            {
                SendLobbySnapshotRequestToServer();
                return;
            }

            if (scene.name == MainMenuSceneName && !IsInSession)
            {
                ResetSessionState();
            }
        }

        /// <summary>
        /// Espera a que la escena de juego termine de estabilizarse y luego posiciona a los jugadores e inicia la primera ronda.
        /// </summary>
        /// <returns>
        /// Rutina utilizada para diferir la inicializacion una vez completada la carga de escena.
        /// </returns>
        private IEnumerator BeginGameSceneFlowNextFrame()
        {
            yield return null;
            yield return null;

            PositionPlayersForGameScene();

            RoundManager roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();

            if (roundManager != null)
            {
                roundManager.StartNextRound();
            }

            SetStatus("Partida iniciada. La escena de juego ya esta sincronizada para todos los clientes.");
        }

        /// <summary>
        /// Reubica a los jugadores conectados alrededor del centro del mapa de prueba al entrar en la escena de juego.
        /// </summary>
        private void PositionPlayersForGameScene()
        {
            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager == null)
            {
                return;
            }

            List<NetworkPlayer> players = networkGameManager.GetPlayersSnapshot()
                .Where(player => player != null)
                .OrderBy(player => player.PlayerId)
                .ToList();

            if (players.Count == 0)
            {
                return;
            }

            float radius = Mathf.Max(2.5f, players.Count);

            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                float angle = (Mathf.PI * 2f * playerIndex) / players.Count;
                Vector3 spawnPosition = new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
                players[playerIndex].TeleportToGameSpawnOnServer(spawnPosition, Quaternion.identity);
            }
        }

        /// <summary>
        /// Cierra la sesion NGO actual sin cambiar de escena de inmediato.
        /// </summary>
        private void ShutdownNetworkSession()
        {
            EnsureNetworkInfrastructure();

            if (networkManager == null || networkBootstrap == null)
            {
                ResetSessionState();
                return;
            }

            suppressLocalDisconnectHandling = true;
            UnregisterLobbyMessageHandlers();
            networkBootstrap.Shutdown();
            networkBootstrap.UseRelayTransport(false);
            EnsureNetworkConfig();
            networkManager.NetworkConfig.ConnectionData = Array.Empty<byte>();
            StartCoroutine(ClearLocalDisconnectSuppressionNextFrame());
            ResetSessionState();
        }

        /// <summary>
        /// Libera el bloqueo temporal usado para ignorar la desconexion local esperada durante un cierre voluntario.
        /// </summary>
        /// <returns>
        /// Rutina de un frame usada para levantar la supresion despues de completar el shutdown.
        /// </returns>
        private IEnumerator ClearLocalDisconnectSuppressionNextFrame()
        {
            yield return null;
            suppressLocalDisconnectHandling = false;
        }

        /// <summary>
        /// Espera un frame para dar tiempo a que NGO cree o destruya objetos de jugador antes de publicar la lista de lobby.
        /// </summary>
        /// <returns>
        /// Rutina usada por el servidor para diferir la publicacion de la fotografia del lobby.
        /// </returns>
        private IEnumerator SendLobbySnapshotNextFrame()
        {
            yield return null;
            SendLobbySnapshotToAllClients();
        }

        /// <summary>
        /// Guarda el nombre local usando una normalizacion consistente para UI, payload y fallback de lobby.
        /// </summary>
        /// <param name="requestedPlayerName">
        /// Nombre solicitado por el usuario.
        /// </param>
        private void SetLocalPlayerName(string requestedPlayerName)
        {
            LocalPlayerName = NormalizePlayerName(requestedPlayerName);
            NotifySessionStateChanged();
        }

        /// <summary>
        /// Determina la posicion de spawn provisional que se asigna al jugador durante la aprobacion de conexion.
        /// </summary>
        /// <param name="clientId">
        /// Client id para el cual se calcula la posicion.
        /// </param>
        /// <returns>
        /// Posicion base de spawn para el nuevo jugador.
        /// </returns>
        private static Vector3 GetPlayerSpawnPosition(ulong clientId)
        {
            float offset = clientId * 1.5f;
            return new Vector3(offset, 1f, 0f);
        }

        /// <summary>
        /// Convierte el payload de conexion recibido en el nombre legible del jugador.
        /// </summary>
        /// <param name="payload">
        /// Bytes enviados por el cliente durante la conexion.
        /// </param>
        /// <returns>
        /// Nombre decodificado, o una cadena vacia cuando el payload no existe.
        /// </returns>
        private static string DecodePlayerName(byte[] payload)
        {
            return payload != null && payload.Length > 0
                ? Encoding.UTF8.GetString(payload)
                : string.Empty;
        }

        /// <summary>
        /// Selecciona el endpoint Relay que mejor coincide con el tipo de conexion configurado para el prototipo.
        /// </summary>
        /// <param name="serverEndpoints">
        /// Coleccion de endpoints devuelta por el servicio Relay.
        /// </param>
        /// <returns>
        /// Endpoint que debe usarse para configurar Unity Transport.
        /// </returns>
        private RelayServerEndpoint SelectRelayEndpoint(IReadOnlyList<RelayServerEndpoint> serverEndpoints)
        {
            RelayServerEndpoint selectedEndpoint = serverEndpoints?
                .FirstOrDefault(endpoint => endpoint != null && endpoint.ConnectionType == relayConnectionType);

            selectedEndpoint ??= serverEndpoints?.FirstOrDefault(endpoint => endpoint != null);

            if (selectedEndpoint == null)
            {
                throw new InvalidOperationException("Relay no devolvio un endpoint utilizable para el tipo de conexion solicitado.");
            }

            return selectedEndpoint;
        }

        /// <summary>
        /// Normaliza y recorta el nombre visible del jugador para mantener una longitud segura en UI y payload.
        /// </summary>
        /// <param name="requestedPlayerName">
        /// Nombre solicitado por el usuario.
        /// </param>
        /// <param name="fallbackClientId">
        /// Client id usado como respaldo cuando se necesita diferenciar automaticamente un alias vacio.
        /// </param>
        /// <returns>
        /// Nombre normalizado listo para usarse dentro del prototipo.
        /// </returns>
        private static string NormalizePlayerName(string requestedPlayerName, ulong fallbackClientId = 0)
        {
            string normalizedName = string.IsNullOrWhiteSpace(requestedPlayerName)
                ? DefaultPlayerName
                : requestedPlayerName.Trim();

            if (normalizedName.Length > 24)
            {
                normalizedName = normalizedName.Substring(0, 24);
            }

            if (normalizedName == DefaultPlayerName && fallbackClientId > 0)
            {
                normalizedName = $"{DefaultPlayerName} {fallbackClientId}";
            }

            return normalizedName;
        }

        /// <summary>
        /// Limpia el codigo Relay ingresado o generado para asegurar una comparacion consistente.
        /// </summary>
        /// <param name="joinCode">
        /// Codigo que se desea normalizar.
        /// </param>
        /// <returns>
        /// Codigo limpio en mayusculas.
        /// </returns>
        private static string NormalizeJoinCode(string joinCode)
        {
            return string.IsNullOrWhiteSpace(joinCode)
                ? string.Empty
                : joinCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Establece el estado del manager como ocupado y publica el mensaje inicial de la operacion.
        /// </summary>
        /// <param name="statusMessage">
        /// Mensaje que debe mostrarse mientras la operacion queda en curso.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la operacion puede comenzar; en caso contrario, <see langword="false"/>.
        /// </returns>
        private bool TryBeginOperation(string statusMessage)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            SetStatus(statusMessage);
            return true;
        }

        /// <summary>
        /// Marca el final de una operacion asincronica y notifica a la interfaz que el estado volvio a quedar disponible.
        /// </summary>
        private void EndOperation()
        {
            IsBusy = false;
            NotifySessionStateChanged();
        }

        /// <summary>
        /// Registra un mensaje informativo y limpia el ultimo error visible.
        /// </summary>
        /// <param name="message">
        /// Mensaje que debe quedar expuesto como estado actual.
        /// </param>
        private void SetStatus(string message)
        {
            LastStatusMessage = message;
            LastErrorMessage = string.Empty;
            NotifySessionStateChanged();
        }

        /// <summary>
        /// Registra un error visible para UI y consola.
        /// </summary>
        /// <param name="message">
        /// Texto de error que debe conservarse.
        /// </param>
        private void SetError(string message)
        {
            LastErrorMessage = message;
            Debug.LogWarning(message);
            NotifySessionStateChanged();
        }

        /// <summary>
        /// Limpia el estado volatile de la sesion sin destruir la infraestructura persistente de red.
        /// </summary>
        private void ResetSessionState()
        {
            JoinCode = string.Empty;
            approvedPlayerNames.Clear();
            visibleLobbyPlayers.Clear();
            lobbyReadyStates.Clear();
            matchInProgress = false;
            nextLobbySnapshotRequestTime = 0f;
            NotifySessionStateChanged();
        }

        /// <summary>
        /// Resuelve el cargador de escenas registrado para coordinar las transiciones entre menu, lobby y juego.
        /// </summary>
        /// <returns>
        /// Referencia al <see cref="SceneLoader"/> activo, o <see langword="null"/> si aun no existe.
        /// </returns>
        private static SceneLoader ResolveSceneLoader()
        {
            return ServiceLocator.Resolve<SceneLoader>() ?? FindAnyObjectByType<SceneLoader>();
        }

        /// <summary>
        /// Registra los mensajes personalizados que permiten sincronizar la lista simple de lobby entre servidor y clientes.
        /// </summary>
        private void RegisterLobbyMessageHandlers()
        {
            CustomMessagingManager messagingManager = networkManager != null ? networkManager.CustomMessagingManager : null;

            if (messagingManager == null)
            {
                return;
            }

            if (lobbyMessageHandlersRegistered && registeredLobbyMessagingManager == messagingManager)
            {
                return;
            }

            if (lobbyMessageHandlersRegistered)
            {
                UnregisterLobbyMessageHandlers();
            }

            messagingManager.RegisterNamedMessageHandler(LobbySnapshotMessageName, HandleLobbySnapshotMessage);
            messagingManager.RegisterNamedMessageHandler(LobbySnapshotRequestMessageName, HandleLobbySnapshotRequestMessage);
            messagingManager.RegisterNamedMessageHandler(LobbyReadyRequestMessageName, HandleLobbyReadyRequestMessage);
            registeredLobbyMessagingManager = messagingManager;
            lobbyMessageHandlersRegistered = true;
        }

        /// <summary>
        /// Elimina los handlers de mensajes personalizados antes de cambiar o destruir la infraestructura de red.
        /// </summary>
        private void UnregisterLobbyMessageHandlers()
        {
            CustomMessagingManager messagingManager = registeredLobbyMessagingManager != null
                ? registeredLobbyMessagingManager
                : networkManager != null
                    ? networkManager.CustomMessagingManager
                    : null;

            if (!lobbyMessageHandlersRegistered && messagingManager == null)
            {
                return;
            }

            if (messagingManager != null)
            {
                messagingManager.UnregisterNamedMessageHandler(LobbySnapshotMessageName);
                messagingManager.UnregisterNamedMessageHandler(LobbySnapshotRequestMessageName);
                messagingManager.UnregisterNamedMessageHandler(LobbyReadyRequestMessageName);
            }

            registeredLobbyMessagingManager = null;
            lobbyMessageHandlersRegistered = false;
        }

        /// <summary>
        /// Procesa una fotografia de lobby enviada por el servidor y actualiza la cache visible de esta instancia local.
        /// </summary>
        /// <param name="senderClientId">
        /// Client id que envio el mensaje; en condiciones normales debe ser el servidor.
        /// </param>
        /// <param name="messagePayload">
        /// Buffer con pares serializados de client id y nombre visible.
        /// </param>
        private void HandleLobbySnapshotMessage(ulong senderClientId, FastBufferReader messagePayload)
        {
            visibleLobbyPlayers.Clear();
            messagePayload.ReadValueSafe(out int playerCount);

            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                messagePayload.ReadValueSafe(out ulong clientId);
                messagePayload.ReadValueSafe(out int lobbyIndex);
                messagePayload.ReadValueSafe(out FixedString64Bytes playerName);
                messagePayload.ReadValueSafe(out bool isReady);
                messagePayload.ReadValueSafe(out bool isAlive);
                messagePayload.ReadValueSafe(out int currentRoundScore);
                visibleLobbyPlayers[clientId] = new LobbyPlayerSnapshot(
                    clientId,
                    lobbyIndex,
                    playerName.ToString(),
                    isReady,
                    isAlive,
                    currentRoundScore);
            }

            NotifySessionStateChanged();
        }

        /// <summary>
        /// Atiende una solicitud de actualizacion enviada por un cliente que acaba de entrar o cargar el lobby.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que solicita la fotografia actual del lobby.
        /// </param>
        /// <param name="messagePayload">
        /// Payload vacio reservado para futuras extensiones.
        /// </param>
        private void HandleLobbySnapshotRequestMessage(ulong senderClientId, FastBufferReader messagePayload)
        {
            if (!IsServer)
            {
                return;
            }

            SendLobbySnapshotToClient(senderClientId);
        }

        /// <summary>
        /// Atiende una solicitud de ready enviada por un cliente cuando su <see cref="NetworkPlayer"/> local aun no esta resuelto.
        /// </summary>
        /// <param name="senderClientId">
        /// Client id del jugador que solicita cambiar su estado ready.
        /// </param>
        /// <param name="messagePayload">
        /// Buffer que contiene el valor ready solicitado.
        /// </param>
        private void HandleLobbyReadyRequestMessage(ulong senderClientId, FastBufferReader messagePayload)
        {
            if (!CanChangeReadyStateOnServer())
            {
                return;
            }

            messagePayload.ReadValueSafe(out bool requestedReadyState);
            SetLobbyReadyStateOnServer(senderClientId, requestedReadyState);
        }

        /// <summary>
        /// Solicita al servidor la lista actual de jugadores visibles cuando esta instancia actua como cliente.
        /// </summary>
        private void SendLobbySnapshotRequestToServer()
        {
            RegisterLobbyMessageHandlers();

            if (networkManager == null || !networkManager.IsListening || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(sizeof(byte), Allocator.Temp))
            {
                networkManager.CustomMessagingManager.SendNamedMessage(
                    LobbySnapshotRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Envia al servidor una solicitud ready cuando el cliente no puede usar directamente el RPC del objeto jugador.
        /// </summary>
        /// <param name="isReady">
        /// Estado ready que el jugador local desea aplicar.
        /// </param>
        private void SendReadyStateRequestToServer(bool isReady)
        {
            RegisterLobbyMessageHandlers();

            if (networkManager == null || !networkManager.IsListening || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            if (IsServer)
            {
                SetLobbyReadyStateOnServer(networkManager.LocalClientId, isReady);
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(sizeof(byte), Allocator.Temp))
            {
                writer.WriteValueSafe(isReady);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    LobbyReadyRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Guarda en servidor el estado ready autoritativo y lo replica al NetworkPlayer cuando ya existe.
        /// </summary>
        /// <param name="clientId">
        /// Cliente cuyo estado ready debe actualizarse.
        /// </param>
        /// <param name="isReady">
        /// Nuevo estado ready validado por el servidor.
        /// </param>
        public void SetLobbyReadyStateOnServer(ulong clientId, bool isReady)
        {
            if (!CanChangeReadyStateOnServer())
            {
                return;
            }

            lobbyReadyStates[clientId] = isReady;
            SyncNetworkPlayerReadyState(clientId, isReady);
            NotifyLobbyPlayerStateChanged();
        }

        /// <summary>
        /// Copia el estado ready autoritativo al NetworkPlayer asociado cuando el objeto ya esta registrado.
        /// </summary>
        /// <param name="clientId">
        /// Cliente propietario del NetworkPlayer.
        /// </param>
        /// <param name="isReady">
        /// Estado ready que debe reflejar la NetworkVariable del jugador.
        /// </param>
        private void SyncNetworkPlayerReadyState(ulong clientId, bool isReady)
        {
            if (!IsServer)
            {
                return;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();
            NetworkPlayer player = networkGameManager != null ? networkGameManager.GetPlayer(clientId) : null;

            if (player == null)
            {
                StartCoroutine(ApplyReadyStateWhenPlayerExists(clientId, isReady));
                return;
            }

            if (player.IsReady != isReady)
            {
                player.SetReadyState(isReady);
            }
        }

        /// <summary>
        /// Reintenta aplicar ready cuando la solicitud llego antes de que el servidor registrara el <see cref="NetworkPlayer"/>.
        /// </summary>
        /// <param name="clientId">
        /// Cliente cuyo jugador debe recibir el estado ready.
        /// </param>
        /// <param name="isReady">
        /// Estado ready solicitado.
        /// </param>
        /// <returns>
        /// Rutina que espera unos frames mientras el spawn de red termina de registrarse.
        /// </returns>
        private IEnumerator ApplyReadyStateWhenPlayerExists(ulong clientId, bool isReady)
        {
            const int maxAttempts = 10;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                yield return null;

                if (!CanChangeReadyStateOnServer())
                {
                    yield break;
                }

                networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();
                NetworkPlayer player = networkGameManager != null ? networkGameManager.GetPlayer(clientId) : null;

                if (player == null)
                {
                    continue;
                }

                SyncNetworkPlayerReadyState(clientId, isReady);
                yield break;
            }
        }

        /// <summary>
        /// Publica la lista actual de lobby a todos los clientes conectados desde la autoridad del servidor.
        /// </summary>
        private void SendLobbySnapshotToAllClients()
        {
            if (!IsServer || networkManager == null || !networkManager.IsListening || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            Dictionary<ulong, LobbyPlayerSnapshot> snapshot = BuildLobbySnapshot();
            ApplyLocalLobbySnapshot(snapshot);

            using (FastBufferWriter writer = CreateLobbySnapshotWriter(snapshot))
            {
                networkManager.CustomMessagingManager.SendNamedMessageToAll(
                    LobbySnapshotMessageName,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Envia la lista actual de lobby a un cliente especifico que acaba de solicitarla.
        /// </summary>
        /// <param name="clientId">
        /// Cliente destino de la fotografia del lobby.
        /// </param>
        private void SendLobbySnapshotToClient(ulong clientId)
        {
            if (!IsServer || networkManager == null || !networkManager.IsListening || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            Dictionary<ulong, LobbyPlayerSnapshot> snapshot = BuildLobbySnapshot();
            ApplyLocalLobbySnapshot(snapshot);

            using (FastBufferWriter writer = CreateLobbySnapshotWriter(snapshot))
            {
                networkManager.CustomMessagingManager.SendNamedMessage(
                    LobbySnapshotMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Decide si el cliente debe volver a pedir la lista de lobby porque aun no recibio una fotografia completa.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando conviene reintentar la solicitud al servidor; en caso contrario, <see langword="false"/>.
        /// </returns>
        private bool ShouldRetryLobbySnapshotRequest()
        {
            return IsClient
                && !IsServer
                && IsInSession
                && SceneManager.GetActiveScene().name == LobbySceneName
                && visibleLobbyPlayers.Count <= 1
                && Time.unscaledTime >= nextLobbySnapshotRequestTime;
        }

        /// <summary>
        /// Obtiene los client ids que Netcode reconoce actualmente como conectados en la instancia local.
        /// </summary>
        /// <returns>
        /// Conjunto nuevo con los clientes conectados; en servidor representa la fuente autoritativa del lobby.
        /// </returns>
        private HashSet<ulong> GetAuthoritativeConnectedClientIds()
        {
            HashSet<ulong> connectedClientIds = new HashSet<ulong>();

            if (networkManager == null)
            {
                return connectedClientIds;
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                connectedClientIds.Add(clientId);
            }

            return connectedClientIds;
        }

        /// <summary>
        /// Construye una fotografia ordenada de jugadores usando primero los <see cref="NetworkPlayer"/> y luego los nombres aprobados.
        /// </summary>
        /// <returns>
        /// Diccionario nuevo con client ids y nombres visibles listos para serializar.
        /// </returns>
        private Dictionary<ulong, LobbyPlayerSnapshot> BuildLobbySnapshot()
        {
            Dictionary<ulong, LobbyPlayerSnapshot> snapshot = new Dictionary<ulong, LobbyPlayerSnapshot>();
            HashSet<ulong> connectedClientIds = GetAuthoritativeConnectedClientIds();
            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager != null)
            {
                foreach (NetworkPlayer player in networkGameManager.GetPlayersSnapshot().Where(player => player != null))
                {
                    if (connectedClientIds.Count > 0 && !connectedClientIds.Contains(player.OwnerClientId))
                    {
                        continue;
                    }

                    string playerName = string.IsNullOrWhiteSpace(player.PlayerName)
                        ? NormalizePlayerName(string.Empty, player.OwnerClientId)
                        : player.PlayerName;
                    snapshot[player.OwnerClientId] = new LobbyPlayerSnapshot(
                        player.OwnerClientId,
                        player.PlayerIndex,
                        playerName,
                        GetReadyStateForClient(player.OwnerClientId, player.IsReady),
                        player.IsAlive,
                        player.CurrentRoundScore);
                }
            }

            int fallbackIndex = 0;

            foreach (ulong clientId in connectedClientIds)
            {
                if (snapshot.ContainsKey(clientId))
                {
                    fallbackIndex++;
                    continue;
                }

                string playerName = approvedPlayerNames.TryGetValue(clientId, out string approvedName)
                    ? approvedName
                    : NormalizePlayerName(string.Empty, clientId);
                snapshot[clientId] = new LobbyPlayerSnapshot(
                    clientId,
                    fallbackIndex,
                    playerName,
                    GetReadyStateForClient(clientId, false),
                    true,
                    0);
                fallbackIndex++;
            }

            foreach (ulong approvedClientId in approvedPlayerNames.Keys.ToList())
            {
                if (!connectedClientIds.Contains(approvedClientId))
                {
                    approvedPlayerNames.Remove(approvedClientId);
                    lobbyReadyStates.Remove(approvedClientId);
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Reemplaza la cache visible local con una fotografia ya validada por el servidor.
        /// </summary>
        /// <param name="snapshot">
        /// Datos de lobby que deben quedar visibles para esta instancia.
        /// </param>
        private void ApplyLocalLobbySnapshot(Dictionary<ulong, LobbyPlayerSnapshot> snapshot)
        {
            visibleLobbyPlayers.Clear();

            foreach (KeyValuePair<ulong, LobbyPlayerSnapshot> entry in snapshot
                .OrderBy(entry => entry.Value.PlayerIndex)
                .ThenBy(entry => entry.Key))
            {
                visibleLobbyPlayers[entry.Key] = entry.Value;
            }

            NotifySessionStateChanged();
        }

        /// <summary>
        /// Serializa una fotografia de lobby en un buffer temporal listo para enviarse por Custom Messaging.
        /// </summary>
        /// <param name="snapshot">
        /// Datos de lobby que deben empaquetarse.
        /// </param>
        /// <returns>
        /// Writer temporal que el llamador debe liberar al terminar el envio.
        /// </returns>
        private static FastBufferWriter CreateLobbySnapshotWriter(Dictionary<ulong, LobbyPlayerSnapshot> snapshot)
        {
            FastBufferWriter writer = new FastBufferWriter(LobbySnapshotWriterCapacity, Allocator.Temp);
            writer.WriteValueSafe(snapshot.Count);

            foreach (KeyValuePair<ulong, LobbyPlayerSnapshot> entry in snapshot
                .OrderBy(entry => entry.Value.PlayerIndex)
                .ThenBy(entry => entry.Key))
            {
                LobbyPlayerSnapshot playerSnapshot = entry.Value;
                writer.WriteValueSafe(playerSnapshot.ClientId);
                writer.WriteValueSafe(playerSnapshot.PlayerIndex);
                writer.WriteValueSafe(new FixedString64Bytes(playerSnapshot.PlayerName));
                writer.WriteValueSafe(playerSnapshot.IsReady);
                writer.WriteValueSafe(playerSnapshot.IsAlive);
                writer.WriteValueSafe(playerSnapshot.CurrentRoundScore);
            }

            return writer;
        }

        /// <summary>
        /// Construye el texto visible de una entrada de lobby con nombre y estado ready.
        /// </summary>
        /// <param name="snapshot">
        /// Entrada de jugador generada por el servidor.
        /// </param>
        /// <returns>
        /// Texto listo para mostrarse en la lista del lobby.
        /// </returns>
        private static string FormatLobbyPlayerSnapshot(LobbyPlayerSnapshot snapshot)
        {
            string readyText = snapshot.IsReady ? "Listo" : "Esperando";
            return $"{snapshot.PlayerName} [{readyText}]";
        }

        /// <summary>
        /// Obtiene el maximo de jugadores permitido, limitado al rango soportado por el prototipo.
        /// </summary>
        /// <returns>
        /// Maximo de jugadores entre el minimo requerido y 6.
        /// </returns>
        private int GetMaximumPlayerCount()
        {
            return Mathf.Clamp(maximumPlayers, GetMinimumPlayerCount(), 6);
        }

        /// <summary>
        /// Obtiene el minimo de jugadores necesario para iniciar partida, siempre dentro del rango del prototipo.
        /// </summary>
        /// <returns>
        /// Minimo de jugadores entre 2 y el maximo configurado.
        /// </returns>
        private int GetMinimumPlayerCount()
        {
            return Mathf.Clamp(minimumPlayersToStart, 2, Mathf.Max(2, maximumPlayers));
        }

        /// <summary>
        /// Determina si todos los jugadores conectados tienen su estado ready confirmado en servidor.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando todos los jugadores conectados estan listos.
        /// </returns>
        private bool AreAllConnectedPlayersReady()
        {
            HashSet<ulong> connectedClientIds = GetAuthoritativeConnectedClientIds();

            if (connectedClientIds.Count < GetMinimumPlayerCount())
            {
                return false;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager == null)
            {
                return false;
            }

            foreach (ulong clientId in connectedClientIds)
            {
                if (lobbyReadyStates.TryGetValue(clientId, out bool isReadyFromLobby))
                {
                    if (!isReadyFromLobby)
                    {
                        return false;
                    }

                    continue;
                }

                NetworkPlayer player = networkGameManager.GetPlayer(clientId);

                if (player == null || !player.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Devuelve el estado ready autoritativo de un client id, con fallback al valor sincronizado del NetworkPlayer.
        /// </summary>
        /// <param name="clientId">
        /// Cliente cuyo estado ready se desea resolver.
        /// </param>
        /// <param name="fallbackValue">
        /// Valor usado cuando el servidor aun no tiene una entrada explicita para ese cliente.
        /// </param>
        /// <returns>
        /// Estado ready que debe usarse para UI y validaciones.
        /// </returns>
        private bool GetReadyStateForClient(ulong clientId, bool fallbackValue)
        {
            return lobbyReadyStates.TryGetValue(clientId, out bool readyState)
                ? readyState
                : fallbackValue;
        }

        /// <summary>
        /// Genera un mensaje claro para explicar por que el host aun no puede iniciar la partida.
        /// </summary>
        /// <returns>
        /// Texto listo para mostrarse en la UI y consola.
        /// </returns>
        private string GetMatchStartBlockReason()
        {
            if (!IsHost)
            {
                return "Solo el host puede iniciar la partida.";
            }

            if (matchInProgress)
            {
                return "La partida ya esta en proceso de inicio.";
            }

            if (SceneManager.GetActiveScene().name != LobbySceneName)
            {
                return "La partida solo puede iniciarse desde el lobby.";
            }

            int connectedPlayers = GetConnectedPlayerCount();

            if (connectedPlayers < GetMinimumPlayerCount())
            {
                return $"Se necesitan al menos {GetMinimumPlayerCount()} jugadores para iniciar.";
            }

            if (connectedPlayers > GetMaximumPlayerCount())
            {
                return $"La sala supera el maximo de {GetMaximumPlayerCount()} jugadores.";
            }

            if (!AreAllConnectedPlayersReady())
            {
                return "Todos los jugadores deben marcar Listo antes de iniciar.";
            }

            return "Aun no es posible iniciar la partida.";
        }

        /// <summary>
        /// Rechaza una conexion entrante durante Connection Approval y conserva una razon visible para el cliente.
        /// </summary>
        /// <param name="response">
        /// Respuesta de aprobacion que debe completarse.
        /// </param>
        /// <param name="reason">
        /// Motivo enviado al cliente rechazado.
        /// </param>
        private static void RejectConnection(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
        }

        /// <summary>
        /// Calcula cuantos jugadores visibles estan marcados como listos en el snapshot local.
        /// </summary>
        /// <returns>
        /// Cantidad de jugadores listos visibles para esta instancia.
        /// </returns>
        public int GetReadyPlayerCount()
        {
            if (visibleLobbyPlayers.Count > 0)
            {
                return visibleLobbyPlayers.Values.Count(player => player.IsReady);
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();
            return networkGameManager != null
                ? networkGameManager.GetPlayersSnapshot().Count(player => player != null && player.IsReady)
                : 0;
        }

        /// <summary>
        /// Construye un resumen compacto del estado ready del lobby para la UI runtime.
        /// </summary>
        /// <returns>
        /// Texto con progreso de ready y rango permitido de jugadores.
        /// </returns>
        public string GetLobbyReadinessSummary()
        {
            return $"Listos: {GetReadyPlayerCount()}/{GetConnectedPlayerCount()} | Minimo: {GetMinimumPlayerCount()} | Maximo: {GetMaximumPlayerCount()}";
        }

        /// <summary>
        /// Busca el <see cref="NetworkManager"/> que debe gobernar la sesion y advierte si existen duplicados en escena.
        /// </summary>
        /// <returns>
        /// Instancia elegida como autoridad local de Netcode, o <see langword="null"/> si aun no existe ninguna.
        /// </returns>
        private NetworkManager ResolveNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                return NetworkManager.Singleton;
            }

            NetworkManager[] managers = FindObjectsByType<NetworkManager>();

            if (managers == null || managers.Length == 0)
            {
                return null;
            }

            if (managers.Length > 1)
            {
                Debug.LogWarning($"Se encontraron {managers.Length} NetworkManager activos. Se usara el primero disponible para evitar duplicar infraestructura.");
            }

            return managers.FirstOrDefault(manager => manager != null && manager.gameObject.name == NetworkRuntimeObjectName)
                ?? managers.FirstOrDefault(manager => manager != null);
        }

        /// <summary>
        /// Garantiza que el <see cref="NetworkManager"/> disponga de una instancia valida de <see cref="NetworkConfig"/>
        /// incluso cuando fue creado por codigo mediante <see cref="GameObject.AddComponent{T}"/>.
        /// </summary>
        private void EnsureNetworkConfig()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.Prefabs ??= new NetworkPrefabs();
            networkManager.NetworkConfig.ConnectionData ??= Array.Empty<byte>();
        }

        /// <summary>
        /// Notifica a los observadores que el estado visible de la sesion cambio y debe refrescarse.
        /// </summary>
        private void NotifySessionStateChanged()
        {
            SessionStateChanged?.Invoke();
        }
    }
}
