using System;
using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Core;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Network
{
    /// <summary>
    /// Mantiene el registro de clientes conectados y de los <see cref="NetworkPlayer"/> presentes en la sesion.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        /// <summary>
        /// Conjunto de identificadores de cliente reportados por NGO como conectados.
        /// </summary>
        private readonly HashSet<ulong> connectedClientIds = new HashSet<ulong>();

        /// <summary>
        /// Lista de componentes <see cref="NetworkPlayer"/> registrados en la escena.
        /// </summary>
        private readonly List<NetworkPlayer> connectedPlayers = new List<NetworkPlayer>();

        /// <summary>
        /// Indica si ya se realizo la suscripcion a callbacks de conexion.
        /// </summary>
        private bool callbacksRegistered;

        /// <summary>
        /// Evento emitido cuando NGO informa la conexion de un cliente.
        /// </summary>
        public event Action<ulong> ClientConnected;

        /// <summary>
        /// Evento emitido cuando NGO informa la desconexion de un cliente.
        /// </summary>
        public event Action<ulong> ClientDisconnected;

        /// <summary>
        /// Obtiene la lista de solo lectura de jugadores registrados.
        /// </summary>
        public IReadOnlyList<NetworkPlayer> ConnectedPlayers => connectedPlayers;

        /// <summary>
        /// Obtiene la cantidad actual de clientes conectados.
        /// </summary>
        public int ConnectedClientCount => connectedClientIds.Count;

        /// <summary>
        /// Registra el manager en el localizador de servicios y reconstruye el cache inicial de jugadores.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<NetworkGameManager>(this);
            RefreshConnectedPlayers();
        }

        /// <summary>
        /// Se suscribe a callbacks de NGO mientras el componente esta activo.
        /// </summary>
        private void OnEnable()
        {
            SubscribeToCallbacks();
        }

        /// <summary>
        /// Reintenta la suscripcion cuando el <see cref="NetworkManager"/> aparece despues de habilitar este componente.
        /// </summary>
        private void Update()
        {
            if (!callbacksRegistered)
            {
                SubscribeToCallbacks();
            }
        }

        /// <summary>
        /// Cancela la suscripcion a callbacks de NGO cuando el componente se desactiva.
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeFromCallbacks();
        }

        /// <summary>
        /// Registra un <see cref="NetworkPlayer"/> para que el resto del juego pueda resolverlo por owner client id.
        /// </summary>
        /// <param name="player">
        /// Componente de jugador que debe agregarse al registro.
        /// </param>
        public void RegisterPlayer(NetworkPlayer player)
        {
            if (player == null)
            {
                return;
            }

            connectedClientIds.Add(player.OwnerClientId);

            if (connectedPlayers.All(existing => existing == null || existing.OwnerClientId != player.OwnerClientId))
            {
                connectedPlayers.Add(player);
            }

            CleanupPlayerList();
            ReindexConnectedPlayers();
        }

        /// <summary>
        /// Elimina un <see cref="NetworkPlayer"/> del registro interno.
        /// </summary>
        /// <param name="player">
        /// Componente de jugador que debe removerse.
        /// </param>
        public void UnregisterPlayer(NetworkPlayer player)
        {
            if (player == null)
            {
                return;
            }

            connectedPlayers.RemoveAll(existing => existing == null || existing == player);
            CleanupPlayerList();
            ReindexConnectedPlayers();
        }

        /// <summary>
        /// Devuelve el jugador registrado que pertenece al client id especificado.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente propietario del jugador.
        /// </param>
        /// <returns>
        /// Instancia de <see cref="NetworkPlayer"/> asociada al cliente, o <see langword="null"/> si no existe coincidencia.
        /// </returns>
        public NetworkPlayer GetPlayer(ulong clientId)
        {
            CleanupPlayerList();
            return connectedPlayers.FirstOrDefault(player => player != null && player.OwnerClientId == clientId);
        }

        /// <summary>
        /// Construye una copia de la lista actual de jugadores registrados.
        /// </summary>
        /// <returns>
        /// Lista nueva con los jugadores registrados en este momento.
        /// </returns>
        public List<NetworkPlayer> GetPlayersSnapshot()
        {
            CleanupPlayerList();
            return new List<NetworkPlayer>(connectedPlayers);
        }

        /// <summary>
        /// Recalcula los indices de lobby en servidor usando el orden actual de client ids conectados.
        /// </summary>
        public void ReindexConnectedPlayers()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            CleanupPlayerList();

            List<NetworkPlayer> orderedPlayers = connectedPlayers
                .Where(player => player != null && NetworkManager.Singleton.ConnectedClientsIds.Contains(player.OwnerClientId))
                .OrderBy(player => player.OwnerClientId)
                .ToList();

            for (int playerIndex = 0; playerIndex < orderedPlayers.Count; playerIndex++)
            {
                orderedPlayers[playerIndex].SetPlayerIndex(playerIndex);
            }
        }

        /// <summary>
        /// Reconstruye el cache de jugadores escaneando la escena actual.
        /// </summary>
        [ContextMenu("Refresh Connected Players")]
        public void RefreshConnectedPlayers()
        {
            connectedPlayers.Clear();
            connectedClientIds.Clear();

            foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>())
            {
                RegisterPlayer(player);
            }
        }

        /// <summary>
        /// Se suscribe a callbacks de conexion de NGO cuando existe un network manager activo.
        /// </summary>
        private void SubscribeToCallbacks()
        {
            if (callbacksRegistered || NetworkManager.Singleton == null)
            {
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            callbacksRegistered = true;
            SeedConnectedClients();
        }

        /// <summary>
        /// Cancela la suscripcion a callbacks de conexion de NGO.
        /// </summary>
        private void UnsubscribeFromCallbacks()
        {
            if (!callbacksRegistered || NetworkManager.Singleton == null)
            {
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            callbacksRegistered = false;
        }

        /// <summary>
        /// Actualiza caches y emite eventos cuando NGO informa una nueva conexion.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente recien conectado.
        /// </param>
        private void HandleClientConnected(ulong clientId)
        {
            connectedClientIds.Add(clientId);
            ReindexConnectedPlayers();
            ClientConnected?.Invoke(clientId);
            Debug.Log($"Client connected: {clientId}");
        }

        /// <summary>
        /// Actualiza caches y emite eventos cuando NGO informa una desconexion.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente desconectado.
        /// </param>
        private void HandleClientDisconnected(ulong clientId)
        {
            connectedClientIds.Remove(clientId);
            connectedPlayers.RemoveAll(player => player == null || player.OwnerClientId == clientId);
            ReindexConnectedPlayers();
            ClientDisconnected?.Invoke(clientId);
            Debug.Log($"Client disconnected: {clientId}");
        }

        /// <summary>
        /// Elimina referencias nulas dejadas por objetos de jugador destruidos o despawned.
        /// </summary>
        private void CleanupPlayerList()
        {
            connectedPlayers.RemoveAll(player => player == null);
        }

        /// <summary>
        /// Sincroniza el cache local con los clientes que NGO ya conoce como conectados al momento de suscribirse.
        /// </summary>
        private void SeedConnectedClients()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            connectedClientIds.Clear();

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                connectedClientIds.Add(clientId);
            }

            RefreshConnectedPlayers();
        }
    }
}
