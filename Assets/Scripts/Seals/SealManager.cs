using System;
using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Core;
using HitoriKakurembo.Network;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Spawning;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Orquesta los sellos rituales activos de la partida.
    /// El servidor decide que sellos se spawnean, valida solicitudes de jugadores y publica snapshots para que la UI de todos los clientes vea el mismo progreso.
    /// </summary>
    public class SealManager : MonoBehaviour
    {
        /// <summary>
        /// Cantidad base de sellos del prototipo ritual.
        /// Se conserva como constante para compatibilidad con scripts existentes.
        /// </summary>
        public const int RequiredSealCount = 6;

        /// <summary>
        /// Mensaje NGO usado por clientes para pedir al servidor la activacion de un sello cercano.
        /// </summary>
        private const string SealActivationRequestMessageName = "HK_SealActivationRequest";

        /// <summary>
        /// Mensaje NGO usado por el servidor para publicar el estado de los sellos.
        /// </summary>
        private const string SealSnapshotMessageName = "HK_SealSnapshot";

        /// <summary>
        /// Mensaje NGO usado por clientes que acaban de cargar la escena para pedir un snapshot de sellos.
        /// </summary>
        private const string SealSnapshotRequestMessageName = "HK_SealSnapshotRequest";

        /// <summary>
        /// Capacidad maxima del buffer de snapshot de sellos.
        /// </summary>
        private const int SealSnapshotWriterCapacity = 512;

        /// <summary>
        /// Intervalo con el que un cliente reintenta pedir snapshot si aun no lo recibio.
        /// </summary>
        private const float SealSnapshotRequestRetryInterval = 1f;

        /// <summary>
        /// Cantidad de sellos que deben estar activos para completar el objetivo de ronda.
        /// </summary>
        [SerializeField] private int requiredSealCount = RequiredSealCount;

        /// <summary>
        /// Definiciones disponibles para generar sellos al iniciar una ronda.
        /// </summary>
        [SerializeField] private List<SealDefinition> availableSealDefinitions = new List<SealDefinition>();

        /// <summary>
        /// Si esta activo, el servidor intentara generar sellos automaticamente cuando la sesion de red este lista.
        /// </summary>
        [SerializeField] private bool spawnOnStart;

        /// <summary>
        /// Permite repetir tipos de sello cuando hay menos definiciones que sellos requeridos.
        /// </summary>
        [SerializeField] private bool allowDuplicateSealTypes = true;

        /// <summary>
        /// Distancia global minima entre sellos spawneados por el sistema.
        /// </summary>
        [SerializeField] private float minDistanceBetweenSeals = 2f;

        /// <summary>
        /// Slots de sellos registrados por indice logico.
        /// </summary>
        [SerializeField] private RitualSeal[] ritualSeals = new RitualSeal[RequiredSealCount];

        /// <summary>
        /// Distancia maxima entre jugador y sello aceptada por el servidor para validar una activacion.
        /// </summary>
        [SerializeField] private float activationRange = 3f;

        /// <summary>
        /// Estado autoritativo cacheado por indice de sello para snapshots legacy y UI.
        /// </summary>
        private bool[] activatedStates = new bool[RequiredSealCount];

        /// <summary>
        /// Client id del jugador que activo cada sello, usado para UI y depuracion.
        /// </summary>
        private ulong[] activatedByClientIds = new ulong[RequiredSealCount];

        /// <summary>
        /// Registro plano usado para desuscribir eventos sin recorrer objetos destruidos.
        /// </summary>
        private readonly List<RitualSeal> registeredSeals = new List<RitualSeal>();

        /// <summary>
        /// Instancia de mensajeria NGO sobre la que se registraron los handlers.
        /// </summary>
        private CustomMessagingManager registeredMessagingManager;

        /// <summary>
        /// Indica si los mensajes personalizados de sellos ya fueron registrados.
        /// </summary>
        private bool sealMessageHandlersRegistered;

        /// <summary>
        /// Indica si un cliente ya recibio el primer snapshot de sellos.
        /// </summary>
        private bool hasReceivedSealSnapshot;

        /// <summary>
        /// Proximo tiempo local en el que el cliente puede volver a pedir snapshot de sellos.
        /// </summary>
        private float nextSealSnapshotRequestTime;

        /// <summary>
        /// Evita ejecutar dos veces la notificacion de objetivo completado.
        /// </summary>
        private bool allSealsActivatedNotified;

        /// <summary>
        /// Evita repetir el spawn automatico cuando spawnOnStart esta activo.
        /// </summary>
        private bool automaticSpawnCompleted;

        /// <summary>
        /// Evento local usado por UI o sistemas futuros para reaccionar a cambios de progreso.
        /// </summary>
        public event Action<int, int> OnSealProgressChanged;

        /// <summary>
        /// Evento local disparado cuando todos los sellos requeridos estan activos.
        /// </summary>
        public event Action OnAllSealsActivated;

        public RitualSeal[] RitualSeals => ritualSeals;
        public float ActivationRange => activationRange;
        public int RequiredSealTotal => GetRequiredSealCount();
        public IReadOnlyList<SealDefinition> AvailableSealDefinitions => availableSealDefinitions;

        /// <summary>
        /// Registra este manager en el localizador de servicios e inicializa caches autoritativos.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<SealManager>(this);
            EnsureSealCapacity();
            InitializeActivatorCache();
        }

        /// <summary>
        /// Registra handlers de red mientras el manager esta activo.
        /// </summary>
        private void OnEnable()
        {
            RegisterSealMessageHandlers();
        }

        /// <summary>
        /// Cancela handlers y eventos para evitar duplicados al cambiar de escena.
        /// </summary>
        private void OnDisable()
        {
            UnregisterSealMessageHandlers();

            foreach (RitualSeal seal in registeredSeals)
            {
                if (seal != null)
                {
                    seal.StateChanged -= HandleSealStateChanged;
                }
            }

            registeredSeals.Clear();
        }

        /// <summary>
        /// Mantiene conectada la mensajeria NGO, solicita snapshot inicial en clientes y ejecuta spawn automatico si se configuro.
        /// </summary>
        private void Update()
        {
            RegisterSealMessageHandlers();

            if (spawnOnStart && !automaticSpawnCompleted && IsServerActive())
            {
                automaticSpawnCompleted = true;
                SpawnSealsForRound();
            }

            if (ShouldRequestSealSnapshot())
            {
                nextSealSnapshotRequestTime = Time.unscaledTime + SealSnapshotRequestRetryInterval;
                SendSealSnapshotRequestToServer();
            }
        }

        /// <summary>
        /// Solicita al SpawnManager la generacion server-side de los sellos requeridos para la ronda actual.
        /// </summary>
        public void SpawnSealsForRound()
        {
            if (!IsServerActive())
            {
                Debug.LogWarning("SpawnSealsForRound solo puede ejecutarse desde el servidor activo.");
                return;
            }

            EnsureSealCapacity();
            ClearSpawnedSeals();
            ResetLocalSealCaches();

            SpawnManager spawnManager = ServiceLocator.Resolve<SpawnManager>() ?? FindAnyObjectByType<SpawnManager>();

            if (spawnManager == null)
            {
                Debug.LogWarning("No existe SpawnManager en escena. Agrega un SpawnManager antes de generar sellos.");
                return;
            }

            if (availableSealDefinitions == null || availableSealDefinitions.Count == 0)
            {
                Debug.LogWarning("SealManager no tiene SealDefinitions disponibles. Crea assets SealDefinition y asigna la lista en el inspector.");
                return;
            }

            HashSet<SealDefinition> usedDefinitions = new HashSet<SealDefinition>();
            int requiredCount = GetRequiredSealCount();
            int spawnedCount = 0;

            for (int sealIndex = 0; sealIndex < requiredCount; sealIndex++)
            {
                SealDefinition definition = SelectDefinitionForSlot(sealIndex, usedDefinitions);

                if (definition == null)
                {
                    Debug.LogWarning("No hay suficientes SealDefinitions para completar todos los sellos requeridos.");
                    break;
                }

                if (spawnManager.TrySpawnSeal(definition, sealIndex, minDistanceBetweenSeals, out RitualSeal seal))
                {
                    RegisterSeal(seal);
                    usedDefinitions.Add(definition);
                    spawnedCount++;
                    continue;
                }

                Debug.LogWarning($"No se pudo generar una pose valida para el sello '{definition.DisplayName}'.");
            }

            SendSealSnapshotToAllClients();
            PublishSealProgress();

            if (spawnedCount < requiredCount)
            {
                Debug.LogWarning($"SealManager genero {spawnedCount}/{requiredCount} sellos. Revisa SpawnAreas, capas y prefabs de red.");
            }
        }

        /// <summary>
        /// Destruye sellos generados por sistema y limpia sus slots sin afectar sellos manuales de escena.
        /// </summary>
        public void ClearSpawnedSeals()
        {
            EnsureSealCapacity();

            SpawnManager spawnManager = ServiceLocator.Resolve<SpawnManager>() ?? FindAnyObjectByType<SpawnManager>();

            if (IsServerActive())
            {
                spawnManager?.ClearSpawnedSeals();
            }

            for (int index = 0; index < ritualSeals.Length; index++)
            {
                RitualSeal seal = ritualSeals[index];

                if (seal == null)
                {
                    ritualSeals[index] = null;
                    activatedStates[index] = false;
                    activatedByClientIds[index] = ulong.MaxValue;
                    continue;
                }

                if (!seal.IsSpawnedBySystem)
                {
                    continue;
                }

                UnregisterSeal(seal);
                ritualSeals[index] = null;
                activatedStates[index] = false;
                activatedByClientIds[index] = ulong.MaxValue;
            }

            allSealsActivatedNotified = false;
        }

        /// <summary>
        /// Registra un sello en el slot definido por su indice logico.
        /// </summary>
        public bool RegisterSeal(RitualSeal seal)
        {
            if (seal == null)
            {
                return false;
            }

            EnsureSealCapacity();
            int index = Mathf.Clamp(seal.SealIndex, 0, GetRequiredSealCount() - 1);
            seal.SetSealIndex(index);

            if (ritualSeals[index] != null && ritualSeals[index] != seal)
            {
                ritualSeals[index].StateChanged -= HandleSealStateChanged;
                registeredSeals.Remove(ritualSeals[index]);
            }

            ritualSeals[index] = seal;
            seal.StateChanged -= HandleSealStateChanged;
            seal.StateChanged += HandleSealStateChanged;

            if (!registeredSeals.Contains(seal))
            {
                registeredSeals.Add(seal);
            }

            if (seal.IsActivated)
            {
                activatedStates[index] = true;
                activatedByClientIds[index] = seal.ActivatingPlayerClientId;
            }
            else if (activatedStates[index])
            {
                seal.ApplyNetworkState(true, activatedByClientIds[index]);
            }
            else
            {
                seal.ApplyNetworkState(false, ulong.MaxValue);
            }

            PublishSealProgress();
            return true;
        }

        /// <summary>
        /// Elimina un sello del registro si pertenece a este manager.
        /// </summary>
        public void UnregisterSeal(RitualSeal seal)
        {
            if (seal == null)
            {
                return;
            }

            seal.StateChanged -= HandleSealStateChanged;
            registeredSeals.Remove(seal);

            for (int index = 0; index < ritualSeals.Length; index++)
            {
                if (ritualSeals[index] != seal)
                {
                    continue;
                }

                ritualSeals[index] = null;
                activatedStates[index] = false;
                activatedByClientIds[index] = ulong.MaxValue;
            }

            PublishSealProgress();
        }

        /// <summary>
        /// Obtiene el sello registrado en el indice solicitado.
        /// </summary>
        public RitualSeal GetSeal(int index)
        {
            EnsureSealCapacity();

            if (index < 0 || index >= ritualSeals.Length)
            {
                return null;
            }

            return ritualSeals[index];
        }

        /// <summary>
        /// Solicita activar un sello desde el jugador local; el servidor conserva la validacion definitiva.
        /// </summary>
        public void RequestActivateSealFromLocalPlayer(int sealIndex)
        {
            if (sealIndex < 0 || sealIndex >= GetRequiredSealCount())
            {
                return;
            }

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || !networkManager.IsListening || !networkManager.IsClient)
            {
                return;
            }

            if (networkManager.IsServer)
            {
                TryActivateSealOnServer(networkManager.LocalClientId, sealIndex);
                return;
            }

            if (networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(sealIndex);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    SealActivationRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Cuenta cuantos sellos del conjunto actual se encuentran activados.
        /// </summary>
        public int GetActiveSealCount()
        {
            EnsureSealCapacity();
            int requiredCount = GetRequiredSealCount();
            int activeCount = 0;

            for (int index = 0; index < requiredCount; index++)
            {
                if (activatedStates[index])
                {
                    activeCount++;
                }
            }

            return activeCount;
        }

        /// <summary>
        /// Alias de compatibilidad usado por UI existente.
        /// </summary>
        public int GetActivatedSealCount()
        {
            return GetActiveSealCount();
        }

        /// <summary>
        /// Construye un texto compacto con el progreso global de sellos.
        /// </summary>
        public string GetSealProgressSummary()
        {
            return $"Sellos activados: {GetActiveSealCount()}/{GetRequiredSealCount()}";
        }

        /// <summary>
        /// Construye una lista legible de estados de sellos para debug visual de la fase.
        /// </summary>
        public string GetSealStatusList()
        {
            EnsureSealCapacity();
            int requiredCount = GetRequiredSealCount();
            List<string> entries = new List<string>(requiredCount);

            for (int index = 0; index < requiredCount; index++)
            {
                RitualSeal seal = ritualSeals[index];
                string stateLabel = seal != null ? seal.CurrentState.ToString() : "Sin sello";
                entries.Add($"Sello {index + 1}: {stateLabel}");
            }

            return string.Join("\n", entries);
        }

        /// <summary>
        /// Determina si la totalidad de sellos requeridos ya se encuentra activa.
        /// </summary>
        public bool AreAllRequiredSealsActive()
        {
            return GetActiveSealCount() >= GetRequiredSealCount();
        }

        /// <summary>
        /// Alias de compatibilidad usado por sistemas ya existentes.
        /// </summary>
        public bool AreAllSealsActive()
        {
            return AreAllRequiredSealsActive();
        }

        /// <summary>
        /// Restablece todos los sellos conocidos al estado no activado y publica el snapshot desde servidor.
        /// </summary>
        public void ResetAllSeals()
        {
            EnsureSealCapacity();
            ResetLocalSealCaches();

            for (int index = 0; index < GetRequiredSealCount(); index++)
            {
                ritualSeals[index]?.ResetSeal();
            }

            if (IsServerActive())
            {
                SendSealSnapshotToAllClients();
            }

            PublishSealProgress();
        }

        /// <summary>
        /// Actualiza caches cuando un RitualSeal cambia de estado por NetworkVariable o flujo legacy.
        /// </summary>
        private void HandleSealStateChanged(RitualSeal seal, SealState newState)
        {
            if (seal == null)
            {
                return;
            }

            EnsureSealCapacity();
            int index = Mathf.Clamp(seal.SealIndex, 0, GetRequiredSealCount() - 1);
            bool isActive = newState == SealState.Active;
            activatedStates[index] = isActive;
            activatedByClientIds[index] = isActive ? seal.ActivatingPlayerClientId : ulong.MaxValue;

            if (!AreAllRequiredSealsActive())
            {
                allSealsActivatedNotified = false;
            }

            if (IsServerActive())
            {
                SendSealSnapshotToAllClients();
                NotifyAllSealsActivatedIfNeeded();
            }

            PublishSealProgress();
        }

        /// <summary>
        /// Selecciona una definicion para un slot respetando la configuracion de duplicados.
        /// </summary>
        private SealDefinition SelectDefinitionForSlot(int slotIndex, HashSet<SealDefinition> usedDefinitions)
        {
            if (availableSealDefinitions == null || availableSealDefinitions.Count == 0)
            {
                return null;
            }

            List<SealDefinition> validDefinitions = availableSealDefinitions.Where(definition => definition != null).ToList();

            if (validDefinitions.Count == 0)
            {
                return null;
            }

            if (allowDuplicateSealTypes)
            {
                return validDefinitions[slotIndex % validDefinitions.Count];
            }

            foreach (SealDefinition definition in validDefinitions)
            {
                if (!usedDefinitions.Contains(definition))
                {
                    return definition;
                }
            }

            return null;
        }

        /// <summary>
        /// Inicializa caches locales al estado base.
        /// </summary>
        private void ResetLocalSealCaches()
        {
            InitializeActivatorCache();
            allSealsActivatedNotified = false;

            for (int index = 0; index < activatedStates.Length; index++)
            {
                activatedStates[index] = false;
                activatedByClientIds[index] = ulong.MaxValue;
            }
        }

        /// <summary>
        /// Asegura que los arreglos internos coincidan con la cantidad requerida configurada.
        /// </summary>
        private void EnsureSealCapacity()
        {
            int requiredCount = GetRequiredSealCount();

            if (ritualSeals == null || ritualSeals.Length != requiredCount)
            {
                Array.Resize(ref ritualSeals, requiredCount);
            }

            if (activatedStates == null || activatedStates.Length != requiredCount)
            {
                Array.Resize(ref activatedStates, requiredCount);
            }

            if (activatedByClientIds == null || activatedByClientIds.Length != requiredCount)
            {
                int oldLength = activatedByClientIds != null ? activatedByClientIds.Length : 0;
                Array.Resize(ref activatedByClientIds, requiredCount);

                for (int index = oldLength; index < activatedByClientIds.Length; index++)
                {
                    activatedByClientIds[index] = ulong.MaxValue;
                }
            }
        }

        /// <summary>
        /// Devuelve una cantidad de sellos segura y nunca menor a uno.
        /// </summary>
        private int GetRequiredSealCount()
        {
            return Mathf.Max(1, requiredSealCount);
        }

        /// <summary>
        /// Inicializa los activadores con un valor centinela conocido.
        /// </summary>
        private void InitializeActivatorCache()
        {
            if (activatedByClientIds == null)
            {
                return;
            }

            for (int index = 0; index < activatedByClientIds.Length; index++)
            {
                if (activatedByClientIds[index] == 0 && !activatedStates[index])
                {
                    activatedByClientIds[index] = ulong.MaxValue;
                }
            }
        }

        /// <summary>
        /// Registra los mensajes personalizados usados por el sistema de sellos.
        /// </summary>
        private void RegisterSealMessageHandlers()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            CustomMessagingManager messagingManager = networkManager.CustomMessagingManager;

            if (sealMessageHandlersRegistered && registeredMessagingManager == messagingManager)
            {
                return;
            }

            UnregisterSealMessageHandlers();
            messagingManager.RegisterNamedMessageHandler(SealActivationRequestMessageName, HandleSealActivationRequestMessage);
            messagingManager.RegisterNamedMessageHandler(SealSnapshotMessageName, HandleSealSnapshotMessage);
            messagingManager.RegisterNamedMessageHandler(SealSnapshotRequestMessageName, HandleSealSnapshotRequestMessage);
            registeredMessagingManager = messagingManager;
            sealMessageHandlersRegistered = true;
        }

        /// <summary>
        /// Cancela los handlers de mensajes personalizados de sellos.
        /// </summary>
        private void UnregisterSealMessageHandlers()
        {
            if (!sealMessageHandlersRegistered || registeredMessagingManager == null)
            {
                return;
            }

            registeredMessagingManager.UnregisterNamedMessageHandler(SealActivationRequestMessageName);
            registeredMessagingManager.UnregisterNamedMessageHandler(SealSnapshotMessageName);
            registeredMessagingManager.UnregisterNamedMessageHandler(SealSnapshotRequestMessageName);
            registeredMessagingManager = null;
            sealMessageHandlersRegistered = false;
        }

        /// <summary>
        /// Procesa una peticion de activacion enviada por un cliente.
        /// </summary>
        private void HandleSealActivationRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerActive())
            {
                return;
            }

            reader.ReadValueSafe(out int sealIndex);
            TryActivateSealOnServer(senderClientId, sealIndex);
        }

        /// <summary>
        /// Valida una activacion de sello en servidor y publica el resultado si fue aceptada.
        /// </summary>
        private bool TryActivateSealOnServer(ulong clientId, int sealIndex)
        {
            EnsureSealCapacity();

            if (sealIndex < 0 || sealIndex >= GetRequiredSealCount() || activatedStates[sealIndex])
            {
                return false;
            }

            RitualSeal seal = GetSeal(sealIndex);

            if (seal == null)
            {
                return false;
            }

            NetworkGameManager networkGameManager = ServiceLocator.Resolve<NetworkGameManager>() ?? FindAnyObjectByType<NetworkGameManager>();
            NetworkPlayer player = networkGameManager != null ? networkGameManager.GetPlayer(clientId) : null;

            if (!CanPlayerActivateSeal(player, seal))
            {
                return false;
            }

            if (seal.IsSpawned && seal.IsServer)
            {
                seal.StartActivationServer(clientId);
                return true;
            }

            seal.ActivateSeal(clientId);
            return true;
        }

        /// <summary>
        /// Valida si un jugador puede completar un sello en el estado actual de la ronda.
        /// </summary>
        private bool CanPlayerActivateSeal(NetworkPlayer player, RitualSeal seal)
        {
            if (player == null || seal == null || !player.IsSurvivor || player.IsDoll || !player.IsAlive)
            {
                return false;
            }

            RoundManager roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();

            if (roundManager != null && roundManager.CurrentState != RoundState.Playing)
            {
                return false;
            }

            float distance = Vector3.Distance(player.transform.position, seal.transform.position);
            return distance <= activationRange;
        }

        /// <summary>
        /// Publica evento local y deja a la UI saber que el progreso cambio.
        /// </summary>
        private void PublishSealProgress()
        {
            OnSealProgressChanged?.Invoke(GetActiveSealCount(), GetRequiredSealCount());
        }

        /// <summary>
        /// Notifica una sola vez cuando todos los sellos requeridos quedan activos.
        /// </summary>
        private void NotifyAllSealsActivatedIfNeeded()
        {
            if (allSealsActivatedNotified || !AreAllRequiredSealsActive())
            {
                return;
            }

            allSealsActivatedNotified = true;
            OnAllSealsActivated?.Invoke();
            RoundManager roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();
            roundManager?.NotifyAllSealsActivatedOnServer();
        }

        /// <summary>
        /// Publica el estado actual de sellos a todos los clientes.
        /// </summary>
        private void SendSealSnapshotToAllClients()
        {
            if (!CanSendSealMessages())
            {
                return;
            }

            using (FastBufferWriter writer = CreateSealSnapshotWriter())
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(
                    SealSnapshotMessageName,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Publica el estado actual de sellos a un cliente especifico.
        /// </summary>
        private void SendSealSnapshotToClient(ulong clientId)
        {
            if (!CanSendSealMessages())
            {
                return;
            }

            using (FastBufferWriter writer = CreateSealSnapshotWriter())
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    SealSnapshotMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Construye un snapshot serializado con los estados de sellos.
        /// </summary>
        private FastBufferWriter CreateSealSnapshotWriter()
        {
            EnsureSealCapacity();
            int requiredCount = GetRequiredSealCount();
            FastBufferWriter writer = new FastBufferWriter(SealSnapshotWriterCapacity, Allocator.Temp);
            writer.WriteValueSafe(requiredCount);

            for (int index = 0; index < requiredCount; index++)
            {
                writer.WriteValueSafe(index);
                writer.WriteValueSafe(activatedStates[index]);
                writer.WriteValueSafe(activatedByClientIds[index]);
            }

            return writer;
        }

        /// <summary>
        /// Aplica localmente un snapshot de sellos recibido desde servidor.
        /// </summary>
        private void HandleSealSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int sealCount);
            EnsureSealCapacity();
            int readableCount = Mathf.Min(sealCount, GetRequiredSealCount());

            for (int entryIndex = 0; entryIndex < sealCount; entryIndex++)
            {
                reader.ReadValueSafe(out int sealIndex);
                reader.ReadValueSafe(out bool isActivated);
                reader.ReadValueSafe(out ulong activatedByClientId);

                if (sealIndex < 0 || sealIndex >= readableCount)
                {
                    continue;
                }

                activatedStates[sealIndex] = isActivated;
                activatedByClientIds[sealIndex] = activatedByClientId;
                ritualSeals[sealIndex]?.ApplyNetworkState(isActivated, activatedByClientId);
            }

            hasReceivedSealSnapshot = true;
            PublishSealProgress();
        }

        /// <summary>
        /// Atiende la solicitud de snapshot de sellos enviada por un cliente.
        /// </summary>
        private void HandleSealSnapshotRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerActive())
            {
                return;
            }

            SendSealSnapshotToClient(senderClientId);
        }

        /// <summary>
        /// Solicita al servidor el estado actual de los sellos.
        /// </summary>
        private void SendSealSnapshotRequestToServer()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || !networkManager.IsListening || !networkManager.IsClient || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp))
            {
                networkManager.CustomMessagingManager.SendNamedMessage(
                    SealSnapshotRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Determina si esta instancia cliente necesita pedir el snapshot inicial de sellos.
        /// </summary>
        private bool ShouldRequestSealSnapshot()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null
                && networkManager.IsListening
                && networkManager.IsClient
                && !networkManager.IsServer
                && !hasReceivedSealSnapshot
                && Time.unscaledTime >= nextSealSnapshotRequestTime;
        }

        /// <summary>
        /// Determina si se pueden enviar mensajes de sellos desde servidor.
        /// </summary>
        private static bool CanSendSealMessages()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer
                && NetworkManager.Singleton.CustomMessagingManager != null;
        }

        /// <summary>
        /// Determina si la instancia local actua como servidor activo.
        /// </summary>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer;
        }
    }
}
