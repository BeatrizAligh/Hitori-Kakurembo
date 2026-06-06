using System;
using System.Collections.Generic;
using HitoriKakurembo.Core;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Network
{
    /// <summary>
    /// Mantiene un registro liviano de conexiones activas para lobby y orquestacion de sesion.
    /// </summary>
    public class PlayerConnectionManager : MonoBehaviour
    {
        /// <summary>
        /// Conjunto de client ids actualmente conectados segun NGO.
        /// </summary>
        private readonly HashSet<ulong> connectedClientIds = new HashSet<ulong>();

        /// <summary>
        /// Indica si ya se realizo la suscripcion a callbacks del network manager.
        /// </summary>
        private bool callbacksRegistered;

        /// <summary>
        /// Evento emitido cuando se registra una nueva conexion de jugador.
        /// </summary>
        public event Action<ulong> OnPlayerConnected;

        /// <summary>
        /// Evento emitido cuando se elimina una conexion de jugador existente.
        /// </summary>
        public event Action<ulong> OnPlayerDisconnected;

        /// <summary>
        /// Obtiene la cantidad de clientes conectados actualmente.
        /// </summary>
        public int ConnectedCount => connectedClientIds.Count;

        /// <summary>
        /// Registra este manager en el localizador de servicios.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<PlayerConnectionManager>(this);
        }

        /// <summary>
        /// Se suscribe a callbacks de conexion mientras el componente esta activo.
        /// </summary>
        private void OnEnable()
        {
            SubscribeToCallbacks();
        }

        /// <summary>
        /// Reintenta la suscripcion cuando el <see cref="NetworkManager"/> aun no existia al habilitarse este componente.
        /// </summary>
        private void Update()
        {
            if (!callbacksRegistered)
            {
                SubscribeToCallbacks();
            }
        }

        /// <summary>
        /// Cancela la suscripcion a callbacks de conexion cuando el componente se desactiva.
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeFromCallbacks();
        }

        /// <summary>
        /// Determina si el client id indicado se encuentra conectado.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente que se desea consultar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el cliente esta presente en el registro; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool IsClientConnected(ulong clientId)
        {
            return connectedClientIds.Contains(clientId);
        }

        /// <summary>
        /// Devuelve la coleccion actual de client ids conectados.
        /// </summary>
        /// <returns>
        /// Coleccion de solo lectura con los identificadores conectados.
        /// </returns>
        public IReadOnlyCollection<ulong> GetConnectedClientIds()
        {
            return connectedClientIds;
        }

        /// <summary>
        /// Se suscribe a callbacks de conexion de NGO cuando existe un network manager disponible.
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
        /// Registra el client id conectado y emite el evento correspondiente.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente recien conectado.
        /// </param>
        private void HandleClientConnected(ulong clientId)
        {
            connectedClientIds.Add(clientId);
            OnPlayerConnected?.Invoke(clientId);
        }

        /// <summary>
        /// Elimina el client id desconectado y emite el evento correspondiente.
        /// </summary>
        /// <param name="clientId">
        /// Identificador del cliente desconectado.
        /// </param>
        private void HandleClientDisconnected(ulong clientId)
        {
            connectedClientIds.Remove(clientId);
            OnPlayerDisconnected?.Invoke(clientId);
        }

        /// <summary>
        /// Carga en el cache los clientes que ya estaban conectados cuando este manager termino de suscribirse.
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
        }
    }
}
