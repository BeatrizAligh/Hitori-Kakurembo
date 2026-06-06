using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitoriKakurembo.Core
{
    /// <summary>
    /// Encapsula las transiciones de escena y decide si la carga debe ser local o sincronizada por NGO.
    /// </summary>
    public class SceneLoader : MonoBehaviour
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
        /// Nombre canonico de la escena de pruebas de red.
        /// </summary>
        private const string NetworkTestSceneName = "NetworkTestScene";

        /// <summary>
        /// Registra este cargador en el localizador de servicios.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<SceneLoader>(this);
        }

        /// <summary>
        /// Carga la escena de menu principal.
        /// </summary>
        public void LoadMainMenu()
        {
            LoadScene(MainMenuSceneName);
        }

        /// <summary>
        /// Carga la escena de lobby.
        /// </summary>
        public void LoadLobby()
        {
            LoadScene(LobbySceneName);
        }

        /// <summary>
        /// Carga la escena principal de juego.
        /// </summary>
        public void LoadGame()
        {
            LoadScene(GameSceneName);
        }

        /// <summary>
        /// Carga la escena de pruebas de red.
        /// </summary>
        public void LoadNetworkTest()
        {
            LoadScene(NetworkTestSceneName);
        }

        /// <summary>
        /// Carga la escena solicitada utilizando sincronizacion de red cuando existe un servidor activo.
        /// </summary>
        /// <param name="sceneName">
        /// Nombre de la escena que debe cargarse.
        /// </param>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("SceneLoader received an empty scene name.");
                return;
            }

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager != null && networkManager.IsListening && networkManager.IsServer)
            {
                networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return;
            }

            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
    }
}
