using HitoriKakurembo.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace HitoriKakurembo.Network
{
    /// <summary>
    /// Configura Unity Transport y expone puntos de entrada explicitos para iniciar la sesion como host, cliente o servidor.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        /// <summary>
        /// Direccion por defecto utilizada para configurar el transporte.
        /// </summary>
        [SerializeField] private string address = "127.0.0.1";

        /// <summary>
        /// Puerto por defecto utilizado para configurar el transporte.
        /// </summary>
        [SerializeField] private ushort port = 7777;

        /// <summary>
        /// Indica si el transporte ya fue configurado externamente para usar Relay y no debe sobrescribirse con IP y puerto directos.
        /// </summary>
        [SerializeField] private bool useRelayTransport;

        /// <summary>
        /// Referencia cacheada al <see cref="NetworkManager"/> activo.
        /// </summary>
        private NetworkManager cachedNetworkManager;

        /// <summary>
        /// Referencia cacheada al componente <see cref="UnityTransport"/> asociado al network manager activo.
        /// </summary>
        private UnityTransport cachedTransport;

        /// <summary>
        /// Resuelve dependencias de red y registra el bootstrap dentro del localizador de servicios.
        /// </summary>
        private void Awake()
        {
            CacheDependencies();
            ServiceLocator.Register<NetworkBootstrap>(this);
        }

        /// <summary>
        /// Inicia la sesion de red en modo host.
        /// </summary>
        public bool StartHost()
        {
            return StartNetwork(NetworkMode.Host);
        }

        /// <summary>
        /// Inicia la sesion de red en modo cliente.
        /// </summary>
        public bool StartClient()
        {
            return StartNetwork(NetworkMode.Client);
        }

        /// <summary>
        /// Inicia la sesion de red en modo servidor dedicado.
        /// </summary>
        public bool StartServer()
        {
            return StartNetwork(NetworkMode.Server);
        }

        /// <summary>
        /// Cierra la sesion de red actual cuando existe una sesion escuchando.
        /// </summary>
        public void Shutdown()
        {
            CacheDependencies();
            useRelayTransport = false;

            if (cachedNetworkManager != null && cachedNetworkManager.IsListening)
            {
                cachedNetworkManager.Shutdown();
            }
        }

        /// <summary>
        /// Define si el siguiente arranque de red debe respetar una configuracion Relay aplicada externamente al transporte.
        /// </summary>
        /// <param name="value">
        /// <see langword="true"/> para preservar la configuracion Relay ya aplicada; <see langword="false"/> para volver a usar conexion directa por IP y puerto.
        /// </param>
        public void UseRelayTransport(bool value)
        {
            useRelayTransport = value;
        }

        /// <summary>
        /// Aplica una nueva direccion y un nuevo puerto al transporte.
        /// </summary>
        /// <param name="newAddress">
        /// Direccion IPv4 o hostname que debe utilizar el transporte.
        /// </param>
        /// <param name="newPort">
        /// Puerto UDP que debe utilizar el transporte.
        /// </param>
        public void ConfigureTransport(string newAddress, ushort newPort)
        {
            address = newAddress;
            port = newPort;

            CacheDependencies();

            if (cachedTransport != null)
            {
                cachedTransport.SetConnectionData(address, port);
            }
        }

        /// <summary>
        /// Inicia el network manager en el modo solicitado.
        /// </summary>
        /// <param name="mode">
        /// Modo de inicio requerido por el llamador.
        /// </param>
        private bool StartNetwork(NetworkMode mode)
        {
            CacheDependencies();

            if (cachedNetworkManager == null)
            {
                Debug.LogWarning("NetworkBootstrap could not find a NetworkManager in the scene.");
                return false;
            }

            if (cachedNetworkManager.IsListening)
            {
                Debug.LogWarning("NetworkManager is already running.");
                return false;
            }

            if (!useRelayTransport)
            {
                ConfigureTransport(address, port);
            }

            bool started = mode switch
            {
                NetworkMode.Host => cachedNetworkManager.StartHost(),
                NetworkMode.Client => cachedNetworkManager.StartClient(),
                NetworkMode.Server => cachedNetworkManager.StartServer(),
                _ => false
            };

            Debug.Log(started
                ? $"Network started as {mode} on {address}:{port}."
                : $"Network failed to start as {mode}.");

            return started;
        }

        /// <summary>
        /// Resuelve el network manager activo y su transporte asociado desde la escena actual.
        /// </summary>
        private void CacheDependencies()
        {
            cachedNetworkManager = NetworkManager.Singleton ?? FindAnyObjectByType<NetworkManager>();
            cachedTransport = cachedNetworkManager != null
                ? cachedNetworkManager.GetComponent<UnityTransport>()
                : null;
        }

        /// <summary>
        /// Enumera los modos de ejecucion de red soportados por el bootstrap.
        /// </summary>
        private enum NetworkMode
        {
            /// <summary>
            /// Ejecuta servidor y cliente local dentro del mismo proceso.
            /// </summary>
            Host,

            /// <summary>
            /// Conecta este proceso como cliente a un host o servidor externo.
            /// </summary>
            Client,

            /// <summary>
            /// Ejecuta un servidor sin cliente local asociado.
            /// </summary>
            Server
        }
    }
}
