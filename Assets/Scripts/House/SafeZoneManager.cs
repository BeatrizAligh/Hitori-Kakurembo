using HitoriKakurembo.Core;
using HitoriKakurembo.Network;
using HitoriKakurembo.Rounds;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.House
{
    /// <summary>
    /// Evalua si la posicion del muneco se encuentra dentro o fuera de una zona visible o segura del mapa.
    /// </summary>
    public class SafeZoneManager : MonoBehaviour
    {
        /// <summary>
        /// Mensaje NGO usado por servidor para publicar el estado visible del muneco.
        /// </summary>
        private const string SafeZoneSnapshotMessageName = "HK_SafeZoneSnapshot";

        /// <summary>
        /// Mensaje NGO usado por clientes para pedir el estado actual de la zona visible.
        /// </summary>
        private const string SafeZoneSnapshotRequestMessageName = "HK_SafeZoneSnapshotRequest";

        /// <summary>
        /// Capacidad maxima del buffer de snapshot de zona visible.
        /// </summary>
        private const int SafeZoneSnapshotWriterCapacity = 128;

        /// <summary>
        /// Frecuencia con la que el servidor publica si el muneco esta dentro o fuera de zona visible.
        /// </summary>
        private const float SafeZoneSnapshotSendInterval = 0.5f;

        /// <summary>
        /// Frecuencia de reintento de clientes que aun no recibieron snapshot.
        /// </summary>
        private const float SafeZoneSnapshotRequestInterval = 1f;

        /// <summary>
        /// Collider que delimita la zona visible o segura utilizada para las comprobaciones.
        /// </summary>
        [SerializeField] private Collider visibleZoneCollider = null;

        /// <summary>
        /// Instancia de mensajeria NGO sobre la que se registraron los handlers.
        /// </summary>
        private CustomMessagingManager registeredMessagingManager;

        /// <summary>
        /// Indica si los handlers de zona visible ya fueron registrados.
        /// </summary>
        private bool safeZoneMessageHandlersRegistered;

        /// <summary>
        /// Indica si el cliente ya recibio al menos un snapshot de zona visible.
        /// </summary>
        private bool hasReceivedSafeZoneSnapshot;

        /// <summary>
        /// Proximo tiempo local en el que el servidor publicara estado de zona visible.
        /// </summary>
        private float nextSafeZoneSnapshotSendTime;

        /// <summary>
        /// Proximo tiempo local en el que el cliente pedira snapshot si aun no tiene datos.
        /// </summary>
        private float nextSafeZoneSnapshotRequestTime;

        /// <summary>
        /// Obtiene si el servidor considera que el muneco esta dentro de la zona visible.
        /// </summary>
        public bool IsCurrentDollInsideVisibleZone { get; private set; }

        /// <summary>
        /// Obtiene el centro sincronizado del volumen visible.
        /// </summary>
        public Vector3 VisibleZoneCenter { get; private set; }

        /// <summary>
        /// Obtiene el tamano sincronizado del volumen visible.
        /// </summary>
        public Vector3 VisibleZoneSize { get; private set; }

        /// <summary>
        /// Registra el manager como servicio local para que UI y sistemas de ronda lo puedan consultar.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<SafeZoneManager>(this);
            RefreshVisibleZoneBounds();
        }

        /// <summary>
        /// Registra handlers de red mientras el manager esta activo.
        /// </summary>
        private void OnEnable()
        {
            RegisterSafeZoneMessageHandlers();
        }

        /// <summary>
        /// Cancela handlers de red para evitar duplicados al cambiar de escena.
        /// </summary>
        private void OnDisable()
        {
            UnregisterSafeZoneMessageHandlers();
        }

        /// <summary>
        /// En servidor calcula y publica el estado de zona; en clientes solicita snapshot inicial cuando hace falta.
        /// </summary>
        private void Update()
        {
            RegisterSafeZoneMessageHandlers();

            if (IsServerActive())
            {
                TickServerSafeZoneSync();
                return;
            }

            if (ShouldRequestSafeZoneSnapshot())
            {
                nextSafeZoneSnapshotRequestTime = Time.unscaledTime + SafeZoneSnapshotRequestInterval;
                SendSafeZoneSnapshotRequestToServer();
            }
        }

        /// <summary>
        /// Asigna programaticamente el collider que delimita la zona visible utilizada por el prototipo.
        /// </summary>
        /// <param name="zoneCollider">
        /// Collider que debe utilizarse para las comprobaciones de zona segura o visible.
        /// </param>
        public void SetVisibleZoneCollider(Collider zoneCollider)
        {
            visibleZoneCollider = zoneCollider;
            RefreshVisibleZoneBounds();
        }

        /// <summary>
        /// Determina si el muneco se encuentra dentro del volumen configurado como zona visible.
        /// </summary>
        /// <param name="dollTransform">
        /// Transform del muneco que debe evaluarse.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el collider existe y contiene la posicion del muneco; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool IsDollInsideVisibleZone(Transform dollTransform)
        {
            return dollTransform != null
                && visibleZoneCollider != null
                && visibleZoneCollider.bounds.Contains(dollTransform.position);
        }

        /// <summary>
        /// Determina si el muneco se encuentra fuera del volumen configurado como zona visible.
        /// </summary>
        /// <param name="dollTransform">
        /// Transform del muneco que debe evaluarse.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el muneco no se encuentra dentro de la zona visible; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool IsDollOutsideVisibleZone(Transform dollTransform)
        {
            return !IsDollInsideVisibleZone(dollTransform);
        }

        /// <summary>
        /// Construye un resumen legible para HUD y depuracion de la zona visible.
        /// </summary>
        /// <returns>
        /// Texto corto con estado del muneco y tamano del volumen.
        /// </returns>
        public string GetVisibleZoneSummary()
        {
            string dollState = IsCurrentDollInsideVisibleZone ? "dentro" : "fuera";
            return $"Zona visible: muneco {dollState} | Centro: {VisibleZoneCenter:F1} | Tamano: {VisibleZoneSize:F1}";
        }

        /// <summary>
        /// Actualiza centro y tamano locales desde el collider configurado.
        /// </summary>
        private void RefreshVisibleZoneBounds()
        {
            if (visibleZoneCollider == null)
            {
                VisibleZoneCenter = Vector3.zero;
                VisibleZoneSize = Vector3.zero;
                return;
            }

            Bounds bounds = visibleZoneCollider.bounds;
            VisibleZoneCenter = bounds.center;
            VisibleZoneSize = bounds.size;
        }

        /// <summary>
        /// Calcula el estado autoritativo del muneco y envia snapshots a intervalos cortos.
        /// </summary>
        private void TickServerSafeZoneSync()
        {
            RefreshVisibleZoneBounds();

            RoundManager roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();
            NetworkPlayer doll = roundManager != null ? roundManager.GetCurrentDoll() : null;
            IsCurrentDollInsideVisibleZone = IsDollInsideVisibleZone(doll != null ? doll.transform : null);

            if (Time.unscaledTime < nextSafeZoneSnapshotSendTime)
            {
                return;
            }

            nextSafeZoneSnapshotSendTime = Time.unscaledTime + SafeZoneSnapshotSendInterval;
            SendSafeZoneSnapshotToAllClients();
        }

        /// <summary>
        /// Registra los mensajes personalizados usados por la sincronizacion de zona visible.
        /// </summary>
        private void RegisterSafeZoneMessageHandlers()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            CustomMessagingManager messagingManager = networkManager.CustomMessagingManager;

            if (safeZoneMessageHandlersRegistered && registeredMessagingManager == messagingManager)
            {
                return;
            }

            UnregisterSafeZoneMessageHandlers();
            messagingManager.RegisterNamedMessageHandler(SafeZoneSnapshotMessageName, HandleSafeZoneSnapshotMessage);
            messagingManager.RegisterNamedMessageHandler(SafeZoneSnapshotRequestMessageName, HandleSafeZoneSnapshotRequestMessage);
            registeredMessagingManager = messagingManager;
            safeZoneMessageHandlersRegistered = true;
        }

        /// <summary>
        /// Cancela los handlers registrados para la zona visible.
        /// </summary>
        private void UnregisterSafeZoneMessageHandlers()
        {
            if (!safeZoneMessageHandlersRegistered || registeredMessagingManager == null)
            {
                return;
            }

            registeredMessagingManager.UnregisterNamedMessageHandler(SafeZoneSnapshotMessageName);
            registeredMessagingManager.UnregisterNamedMessageHandler(SafeZoneSnapshotRequestMessageName);
            registeredMessagingManager = null;
            safeZoneMessageHandlersRegistered = false;
        }

        /// <summary>
        /// Publica el snapshot de zona visible a todos los clientes.
        /// </summary>
        private void SendSafeZoneSnapshotToAllClients()
        {
            if (!CanSendSafeZoneMessages())
            {
                return;
            }

            using (FastBufferWriter writer = CreateSafeZoneSnapshotWriter())
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(
                    SafeZoneSnapshotMessageName,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Publica el snapshot de zona visible a un cliente especifico.
        /// </summary>
        /// <param name="clientId">
        /// Cliente que debe recibir el estado actual.
        /// </param>
        private void SendSafeZoneSnapshotToClient(ulong clientId)
        {
            if (!CanSendSafeZoneMessages())
            {
                return;
            }

            using (FastBufferWriter writer = CreateSafeZoneSnapshotWriter())
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    SafeZoneSnapshotMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Construye el payload serializado de zona visible.
        /// </summary>
        /// <returns>
        /// Writer temporal que debe liberarse tras el envio.
        /// </returns>
        private FastBufferWriter CreateSafeZoneSnapshotWriter()
        {
            FastBufferWriter writer = new FastBufferWriter(SafeZoneSnapshotWriterCapacity, Allocator.Temp);
            writer.WriteValueSafe(IsCurrentDollInsideVisibleZone);
            writer.WriteValueSafe(VisibleZoneCenter);
            writer.WriteValueSafe(VisibleZoneSize);
            return writer;
        }

        /// <summary>
        /// Aplica en cliente el snapshot de zona visible enviado por servidor.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que envio el mensaje; normalmente el servidor.
        /// </param>
        /// <param name="reader">
        /// Buffer con los datos de zona visible.
        /// </param>
        private void HandleSafeZoneSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (IsServerActive())
            {
                return;
            }

            reader.ReadValueSafe(out bool isDollInside);
            reader.ReadValueSafe(out Vector3 center);
            reader.ReadValueSafe(out Vector3 size);
            IsCurrentDollInsideVisibleZone = isDollInside;
            VisibleZoneCenter = center;
            VisibleZoneSize = size;
            hasReceivedSafeZoneSnapshot = true;
        }

        /// <summary>
        /// Atiende la peticion de snapshot enviada por un cliente.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que solicita estado.
        /// </param>
        /// <param name="reader">
        /// Buffer sin datos adicionales.
        /// </param>
        private void HandleSafeZoneSnapshotRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerActive())
            {
                return;
            }

            SendSafeZoneSnapshotToClient(senderClientId);
        }

        /// <summary>
        /// Solicita al servidor el snapshot de zona visible.
        /// </summary>
        private void SendSafeZoneSnapshotRequestToServer()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || !networkManager.IsListening || !networkManager.IsClient || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp))
            {
                networkManager.CustomMessagingManager.SendNamedMessage(
                    SafeZoneSnapshotRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Determina si el cliente debe pedir el snapshot inicial.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando conviene pedir estado al servidor.
        /// </returns>
        private bool ShouldRequestSafeZoneSnapshot()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null
                && networkManager.IsListening
                && networkManager.IsClient
                && !networkManager.IsServer
                && !hasReceivedSafeZoneSnapshot
                && Time.unscaledTime >= nextSafeZoneSnapshotRequestTime;
        }

        /// <summary>
        /// Determina si se pueden enviar mensajes de zona visible desde servidor.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando NGO y mensajeria estan listos.
        /// </returns>
        private static bool CanSendSafeZoneMessages()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer
                && NetworkManager.Singleton.CustomMessagingManager != null;
        }

        /// <summary>
        /// Determina si la instancia local actua actualmente como servidor.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando existe servidor NGO activo.
        /// </returns>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer;
        }
    }
}
