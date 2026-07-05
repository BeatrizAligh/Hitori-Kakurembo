using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestMultiplayer.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TestMultiplayer.Networking
{
    public class TestMultiplayerSessionManager : MonoBehaviour
    {
        private const string RuntimeObjectName = "TestMultiplayer_NetworkRuntime";
        private const string DefaultLobbySceneName = "TestMultiplayerLobby";
        private const string DefaultGameSceneName = "TestMultiplayerGame";

        [Header("Scenes")]
        [SerializeField] private string lobbySceneName = DefaultLobbySceneName;
        [SerializeField] private string gameSceneName = DefaultGameSceneName;

        [Header("Network")]
        [SerializeField] private NetworkObject playerBrainPrefab;
        [SerializeField] private NetworkObject[] registeredNetworkPrefabs = Array.Empty<NetworkObject>();
        [SerializeField] private int maximumPlayers = 6;
        [SerializeField] private int minimumPlayersToStart = 1;
        [SerializeField] private string relayConnectionType = "udp";

        private readonly Dictionary<ulong, CharacterProfile> approvedProfiles = new Dictionary<ulong, CharacterProfile>();
        private readonly Dictionary<ulong, bool> readyStates = new Dictionary<ulong, bool>();
        private readonly List<TestMultiplayerPlayerBrain> brains = new List<TestMultiplayerPlayerBrain>();

        private NetworkManager networkManager;
        private UnityTransport unityTransport;
        private bool callbacksRegistered;
        private bool networkPrefabsRegistered;
        private bool servicesInitialized;
        private bool matchStarted;

        public static TestMultiplayerSessionManager Instance { get; private set; }

        public string JoinCode { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = "Listo.";
        public string ErrorMessage { get; private set; } = string.Empty;
        public bool IsBusy { get; private set; }
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsClient => networkManager != null && networkManager.IsClient;
        public bool IsInSession => networkManager != null && networkManager.IsListening;
        public IReadOnlyList<TestMultiplayerPlayerBrain> Brains => brains;

        public event Action StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureNetworkRuntime();
        }

        private void OnEnable()
        {
            RegisterCallbacks();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void Update()
        {
            if (!callbacksRegistered)
            {
                RegisterCallbacks();
            }
        }

        public async Task<bool> CreateLobbyAsync(CharacterProfile localProfile)
        {
            if (!BeginOperation("Creando lobby..."))
            {
                return false;
            }

            try
            {
                EnsureNetworkRuntime();
                ShutdownCurrentSession();

                if (!await InitializeServicesAsync())
                {
                    return false;
                }

                CharacterProfile safeProfile = CharacterProfileStore.Sanitize(localProfile).Clone();
                PrepareConnectionPayload(safeProfile);

                int maxConnections = Mathf.Max(0, maximumPlayers - 1);
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                unityTransport.SetRelayServerData(new RelayServerData(allocation, relayConnectionType));

                if (!networkManager.StartHost())
                {
                    SetError("No se pudo iniciar el host.");
                    return false;
                }

                approvedProfiles[networkManager.LocalClientId] = safeProfile;
                readyStates[networkManager.LocalClientId] = false;
                SetStatus($"Lobby creado. Codigo: {JoinCode}");
                LoadSceneForSession(lobbySceneName);
                return true;
            }
            catch (Exception exception)
            {
                SetError($"No se pudo crear el lobby: {exception.Message}");
                return false;
            }
            finally
            {
                EndOperation();
            }
        }

        public async Task<bool> JoinLobbyAsync(CharacterProfile localProfile, string joinCode)
        {
            if (!BeginOperation("Uniendose al lobby..."))
            {
                return false;
            }

            try
            {
                EnsureNetworkRuntime();
                ShutdownCurrentSession();

                string safeJoinCode = string.IsNullOrWhiteSpace(joinCode) ? string.Empty : joinCode.Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(safeJoinCode))
                {
                    SetError("Ingresa un codigo de lobby.");
                    return false;
                }

                if (!await InitializeServicesAsync())
                {
                    return false;
                }

                PrepareConnectionPayload(CharacterProfileStore.Sanitize(localProfile));
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(safeJoinCode);
                JoinCode = safeJoinCode;
                unityTransport.SetRelayServerData(new RelayServerData(allocation, relayConnectionType));

                if (!networkManager.StartClient())
                {
                    SetError("No se pudo iniciar el cliente.");
                    return false;
                }

                SetStatus("Conectando con el host...");
                return true;
            }
            catch (Exception exception)
            {
                SetError($"No se pudo unir al lobby: {exception.Message}");
                return false;
            }
            finally
            {
                EndOperation();
            }
        }

        public void LeaveSession()
        {
            ShutdownCurrentSession();
            SetStatus("Sesion cerrada.");
        }

        public void StartGame()
        {
            if (!CanStartGame(out string reason))
            {
                SetError(reason);
                return;
            }

            matchStarted = true;
            SetStatus("Cargando partida...");
            LoadSceneForSession(gameSceneName);
        }

        public bool CanStartGame(out string reason)
        {
            if (!IsHost)
            {
                reason = "Solo el host puede iniciar la partida.";
                return false;
            }

            if (matchStarted)
            {
                reason = "La partida ya esta arrancando.";
                return false;
            }

            if (brains.Count < Mathf.Max(1, minimumPlayersToStart))
            {
                reason = $"Se necesitan al menos {minimumPlayersToStart} jugadores.";
                return false;
            }

            if (brains.Any(brain => brain != null && !brain.IsReady))
            {
                reason = "Todos los jugadores deben marcar listo.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public CharacterProfile GetApprovedProfile(ulong clientId)
        {
            if (approvedProfiles.TryGetValue(clientId, out CharacterProfile profile))
            {
                return profile.Clone();
            }

            return new CharacterProfile
            {
                PlayerName = $"Player {clientId}",
                Appearance = CharacterAppearanceData.Default
            };
        }

        public bool GetReadyState(ulong clientId)
        {
            return readyStates.TryGetValue(clientId, out bool isReady) && isReady;
        }

        public void SetReadyStateOnServer(ulong clientId, bool isReady)
        {
            if (!IsServer)
            {
                return;
            }

            readyStates[clientId] = isReady;
            NotifyStateChanged();
        }

        public void RegisterBrain(TestMultiplayerPlayerBrain brain)
        {
            if (brain == null)
            {
                return;
            }

            brains.RemoveAll(existing => existing == null || existing.OwnerClientId == brain.OwnerClientId);
            brains.Add(brain);
            brains.Sort((left, right) => left.OwnerClientId.CompareTo(right.OwnerClientId));
            NotifyStateChanged();
        }

        public void UnregisterBrain(TestMultiplayerPlayerBrain brain)
        {
            if (brain == null)
            {
                return;
            }

            brains.RemoveAll(existing => existing == null || existing == brain || existing.OwnerClientId == brain.OwnerClientId);
            NotifyStateChanged();
        }

        private void EnsureNetworkRuntime()
        {
            networkManager = NetworkManager.Singleton;

            if (networkManager == null)
            {
                GameObject runtimeObject = new GameObject(RuntimeObjectName);
                DontDestroyOnLoad(runtimeObject);
                networkManager = runtimeObject.AddComponent<NetworkManager>();
                unityTransport = runtimeObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig = new NetworkConfig();
            }

            unityTransport = networkManager.GetComponent<UnityTransport>();

            if (unityTransport == null)
            {
                unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
            }

            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.Prefabs ??= new NetworkPrefabs();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.PlayerPrefab = playerBrainPrefab != null ? playerBrainPrefab.gameObject : null;
            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
            RegisterNetworkPrefabs();
            RegisterCallbacks();
        }

        private void RegisterNetworkPrefabs()
        {
            if (networkManager == null || networkManager.IsListening)
            {
                return;
            }

            if (networkPrefabsRegistered)
            {
                return;
            }

            foreach (NetworkObject networkPrefab in registeredNetworkPrefabs)
            {
                if (networkPrefab != null && networkPrefab != playerBrainPrefab)
                {
                    networkManager.AddNetworkPrefab(networkPrefab.gameObject);
                }
            }

            networkPrefabsRegistered = true;
        }

        private async Task<bool> InitializeServicesAsync()
        {
            if (servicesInitialized)
            {
                return true;
            }

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesInitialized = true;
            return true;
        }

        private void PrepareConnectionPayload(CharacterProfile profile)
        {
            string json = JsonUtility.ToJson(CharacterProfileStore.Sanitize(profile));
            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(json);
        }

        private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (networkManager == null || playerBrainPrefab == null)
            {
                Reject(response, "El lobby no tiene prefab de PlayerBrain configurado.");
                return;
            }

            if (networkManager.ConnectedClientsIds.Count >= maximumPlayers)
            {
                Reject(response, "El lobby esta lleno.");
                return;
            }

            CharacterProfile profile = DecodeProfile(request.Payload, request.ClientNetworkId);
            approvedProfiles[request.ClientNetworkId] = profile;
            readyStates[request.ClientNetworkId] = false;

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = null;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }

        private static CharacterProfile DecodeProfile(byte[] payload, ulong clientId)
        {
            if (payload == null || payload.Length == 0)
            {
                return new CharacterProfile { PlayerName = $"Player {clientId}" };
            }

            try
            {
                string json = Encoding.UTF8.GetString(payload);
                CharacterProfile profile = JsonUtility.FromJson<CharacterProfile>(json);
                return CharacterProfileStore.Sanitize(profile).Clone();
            }
            catch
            {
                return new CharacterProfile { PlayerName = $"Player {clientId}" };
            }
        }

        private void RegisterCallbacks()
        {
            if (callbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!callbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            callbacksRegistered = false;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            approvedProfiles.Remove(clientId);
            readyStates.Remove(clientId);
            brains.RemoveAll(brain => brain == null || brain.OwnerClientId == clientId);
            NotifyStateChanged();
        }

        private void LoadSceneForSession(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (networkManager != null && networkManager.IsListening && networkManager.IsServer)
            {
                networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return;
            }

            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private void ShutdownCurrentSession()
        {
            approvedProfiles.Clear();
            readyStates.Clear();
            brains.Clear();
            JoinCode = string.Empty;
            matchStarted = false;

            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private bool BeginOperation(string status)
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            SetStatus(status);
            return true;
        }

        private void EndOperation()
        {
            IsBusy = false;
            NotifyStateChanged();
        }

        private void SetStatus(string message)
        {
            StatusMessage = message;
            ErrorMessage = string.Empty;
            NotifyStateChanged();
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            StatusMessage = message;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason;
        }
    }
}
