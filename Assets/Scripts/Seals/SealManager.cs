using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Core;
using HitoriKakurembo.Network;
using HitoriKakurembo.Rounds;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Gestiona los seis sellos rituales, valida activaciones en servidor y sincroniza su estado con todos los clientes.
    /// </summary>
    public class SealManager : MonoBehaviour
    {
        /// <summary>
        /// Cantidad fija de sellos esperada por el flujo ritual base.
        /// </summary>
        public const int RequiredSealCount = 6;

        /// <summary>
        /// Mensaje NGO usado por clientes para pedir al servidor la activacion de un sello cercano.
        /// </summary>
        private const string SealActivationRequestMessageName = "HK_SealActivationRequest";

        /// <summary>
        /// Mensaje NGO usado por el servidor para publicar el estado de los seis sellos.
        /// </summary>
        private const string SealSnapshotMessageName = "HK_SealSnapshot";

        /// <summary>
        /// Mensaje NGO usado por clientes que acaban de cargar la escena para pedir un snapshot de sellos.
        /// </summary>
        private const string SealSnapshotRequestMessageName = "HK_SealSnapshotRequest";

        /// <summary>
        /// Capacidad maxima del buffer de snapshot de sellos.
        /// </summary>
        private const int SealSnapshotWriterCapacity = 256;

        /// <summary>
        /// Intervalo con el que un cliente reintenta pedir snapshot si aun no lo recibio.
        /// </summary>
        private const float SealSnapshotRequestRetryInterval = 1f;

        /// <summary>
        /// Slots de sellos registrados por indice logico.
        /// </summary>
        [SerializeField] private RitualSeal[] ritualSeals = new RitualSeal[RequiredSealCount];

        /// <summary>
        /// Distancia maxima entre jugador y sello aceptada por el servidor para validar una activacion.
        /// </summary>
        [SerializeField] private float activationRange = 3f;

        /// <summary>
        /// Estado autoritativo cacheado por indice de sello.
        /// </summary>
        private readonly bool[] activatedStates = new bool[RequiredSealCount];

        /// <summary>
        /// Client id del jugador que activo cada sello, usado para UI y depuracion.
        /// </summary>
        private readonly ulong[] activatedByClientIds = new ulong[RequiredSealCount];

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
        /// Obtiene el arreglo de sellos actualmente conocido por el manager.
        /// </summary>
        public RitualSeal[] RitualSeals => ritualSeals;

        /// <summary>
        /// Obtiene la distancia maxima de activacion validada por servidor.
        /// </summary>
        public float ActivationRange => activationRange;

        /// <summary>
        /// Registra este manager en el localizador de servicios e inicializa caches autoritativos.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<SealManager>(this);
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
        /// Cancela handlers de red para evitar duplicados al cambiar de escena.
        /// </summary>
        private void OnDisable()
        {
            UnregisterSealMessageHandlers();
        }

        /// <summary>
        /// Mantiene conectada la mensajeria NGO y solicita snapshot inicial en clientes.
        /// </summary>
        private void Update()
        {
            RegisterSealMessageHandlers();

            if (ShouldRequestSealSnapshot())
            {
                nextSealSnapshotRequestTime = Time.unscaledTime + SealSnapshotRequestRetryInterval;
                SendSealSnapshotRequestToServer();
            }
        }

        /// <summary>
        /// Registra un sello en el slot definido por su indice logico.
        /// </summary>
        /// <param name="seal">
        /// Sello que debe agregarse al conjunto gestionado.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el sello fue registrado correctamente; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool RegisterSeal(RitualSeal seal)
        {
            if (seal == null)
            {
                return false;
            }

            int index = Mathf.Clamp(seal.SealIndex, 0, RequiredSealCount - 1);
            ritualSeals[index] = seal;
            seal.ApplyNetworkState(activatedStates[index], activatedByClientIds[index]);
            return true;
        }

        /// <summary>
        /// Obtiene el sello registrado en el indice solicitado.
        /// </summary>
        /// <param name="index">
        /// Posicion logica del sello.
        /// </param>
        /// <returns>
        /// Sello registrado en el indice solicitado, o <see langword="null"/> si el indice no es valido o no existe sello asignado.
        /// </returns>
        public RitualSeal GetSeal(int index)
        {
            if (index < 0 || index >= ritualSeals.Length)
            {
                return null;
            }

            return ritualSeals[index];
        }

        /// <summary>
        /// Solicita activar un sello desde el jugador local; el servidor conserva la validacion definitiva.
        /// </summary>
        /// <param name="sealIndex">
        /// Indice del sello que el cliente intenta activar.
        /// </param>
        public void RequestActivateSealFromLocalPlayer(int sealIndex)
        {
            if (sealIndex < 0 || sealIndex >= RequiredSealCount)
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
        /// <returns>
        /// Numero de sellos activos.
        /// </returns>
        public int GetActivatedSealCount()
        {
            return activatedStates.Count(isActivated => isActivated);
        }

        /// <summary>
        /// Construye un texto compacto con el progreso global de sellos.
        /// </summary>
        /// <returns>
        /// Texto listo para mostrarse en HUD.
        /// </returns>
        public string GetSealProgressSummary()
        {
            return $"Sellos activados: {GetActivatedSealCount()}/{RequiredSealCount}";
        }

        /// <summary>
        /// Construye una lista legible de estados de sellos para debug visual de la fase.
        /// </summary>
        /// <returns>
        /// Texto multilina con el estado de cada sello.
        /// </returns>
        public string GetSealStatusList()
        {
            List<string> entries = new List<string>(RequiredSealCount);

            for (int index = 0; index < RequiredSealCount; index++)
            {
                entries.Add($"Sello {index + 1}: {(activatedStates[index] ? "Activo" : "Pendiente")}");
            }

            return string.Join("\n", entries);
        }

        /// <summary>
        /// Determina si la totalidad de sellos requeridos ya se encuentra activa.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el conteo de sellos activos alcanza el total requerido; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool AreAllSealsActive()
        {
            return GetActivatedSealCount() >= RequiredSealCount;
        }

        /// <summary>
        /// Restablece todos los sellos conocidos al estado no activado y publica el snapshot desde servidor.
        /// </summary>
        public void ResetAllSeals()
        {
            InitializeActivatorCache();

            for (int index = 0; index < RequiredSealCount; index++)
            {
                activatedStates[index] = false;
                activatedByClientIds[index] = ulong.MaxValue;
                ritualSeals[index]?.ApplyNetworkState(false, ulong.MaxValue);
            }

            if (IsServerActive())
            {
                SendSealSnapshotToAllClients();
            }
        }

        /// <summary>
        /// Inicializa los activadores con un valor centinela conocido.
        /// </summary>
        private void InitializeActivatorCache()
        {
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
        /// <param name="senderClientId">
        /// Cliente que solicita activar el sello.
        /// </param>
        /// <param name="reader">
        /// Buffer con el indice de sello solicitado.
        /// </param>
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
        /// <param name="clientId">
        /// Cliente que intenta activar el sello.
        /// </param>
        /// <param name="sealIndex">
        /// Sello solicitado.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la activacion fue aceptada por servidor.
        /// </returns>
        private bool TryActivateSealOnServer(ulong clientId, int sealIndex)
        {
            if (sealIndex < 0 || sealIndex >= RequiredSealCount || activatedStates[sealIndex])
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

            activatedStates[sealIndex] = true;
            activatedByClientIds[sealIndex] = clientId;
            seal.ApplyNetworkState(true, clientId);
            SendSealSnapshotToAllClients();

            if (AreAllSealsActive())
            {
                RoundManager roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();
                roundManager?.NotifyAllSealsActivatedOnServer();
            }

            return true;
        }

        /// <summary>
        /// Valida si un jugador puede completar un sello en el estado actual de la ronda.
        /// </summary>
        /// <param name="player">
        /// Jugador que intenta activar el sello.
        /// </param>
        /// <param name="seal">
        /// Sello objetivo.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el jugador es superviviente, esta vivo y se encuentra en rango.
        /// </returns>
        private bool CanPlayerActivateSeal(NetworkPlayer player, RitualSeal seal)
        {
            if (player == null || seal == null || player.IsDoll || !player.IsAlive)
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
        /// <param name="clientId">
        /// Cliente que debe recibir el snapshot.
        /// </param>
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
        /// Construye un snapshot serializado con los seis estados de sellos.
        /// </summary>
        /// <returns>
        /// Writer temporal que debe liberarse despues de enviar el mensaje.
        /// </returns>
        private FastBufferWriter CreateSealSnapshotWriter()
        {
            FastBufferWriter writer = new FastBufferWriter(SealSnapshotWriterCapacity, Allocator.Temp);
            writer.WriteValueSafe(RequiredSealCount);

            for (int index = 0; index < RequiredSealCount; index++)
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
        /// <param name="senderClientId">
        /// Cliente que envio el mensaje; normalmente el servidor.
        /// </param>
        /// <param name="reader">
        /// Buffer con el estado de los sellos.
        /// </param>
        private void HandleSealSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int sealCount);

            for (int entryIndex = 0; entryIndex < sealCount; entryIndex++)
            {
                reader.ReadValueSafe(out int sealIndex);
                reader.ReadValueSafe(out bool isActivated);
                reader.ReadValueSafe(out ulong activatedByClientId);

                if (sealIndex < 0 || sealIndex >= RequiredSealCount)
                {
                    continue;
                }

                activatedStates[sealIndex] = isActivated;
                activatedByClientIds[sealIndex] = activatedByClientId;
                ritualSeals[sealIndex]?.ApplyNetworkState(isActivated, activatedByClientId);
            }

            hasReceivedSealSnapshot = true;
        }

        /// <summary>
        /// Atiende la solicitud de snapshot de sellos enviada por un cliente.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que solicita el estado.
        /// </param>
        /// <param name="reader">
        /// Buffer vacio requerido por la firma de NGO.
        /// </param>
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
        /// <returns>
        /// <see langword="true"/> cuando conviene solicitar estado al servidor.
        /// </returns>
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
        /// <returns>
        /// <see langword="true"/> cuando la mensajeria de Netcode esta lista.
        /// </returns>
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
        /// <returns>
        /// <see langword="true"/> cuando el servidor de NGO esta activo localmente.
        /// </returns>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer;
        }
    }
}
