using HitoriKakurembo.House;
using HitoriKakurembo.Network;
using HitoriKakurembo.Ritual;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Seals;
using HitoriKakurembo.UI;
using HitoriKakurembo.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitoriKakurembo.Core
{
    /// <summary>
    /// Centraliza el registro de managers de escena para que el resto del proyecto los resuelva mediante <see cref="ServiceLocator"/>.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        /// <summary>
        /// Indica si los servicios deben reconstruirse automaticamente despues de cada carga de escena.
        /// </summary>
        [SerializeField] private bool refreshServicesOnSceneLoad = true;

        /// <summary>
        /// Inicializa la instancia singleton y registra los servicios disponibles al arrancar.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            RefreshServices();
        }

        /// <summary>
        /// Se suscribe a las notificaciones de carga de escena cuando el refresco automatico esta habilitado.
        /// </summary>
        private void OnEnable()
        {
            if (refreshServicesOnSceneLoad)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
        }

        /// <summary>
        /// Cancela la suscripcion a las notificaciones de carga de escena.
        /// </summary>
        private void OnDisable()
        {
            if (refreshServicesOnSceneLoad)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        /// <summary>
        /// Reconstruye el registro de servicios una vez que Unity termina de cargar una escena.
        /// </summary>
        /// <param name="scene">
        /// Escena recien cargada.
        /// </param>
        /// <param name="loadSceneMode">
        /// Modo de carga utilizado por Unity.
        /// </param>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            RefreshServices();
        }

        /// <summary>
        /// Reescanea los managers activos y los publica nuevamente en el localizador de servicios.
        /// </summary>
        [ContextMenu("Refresh Services")]
        public void RefreshServices()
        {
            ServiceLocator.Register<GameManager>(this);
            RegisterIfFound<SceneLoader>();
            RegisterIfFound<NetworkBootstrap>();
            RegisterIfFound<RelaySessionManager>();
            RegisterIfFound<NetworkGameManager>();
            RegisterIfFound<PlayerConnectionManager>();
            RegisterIfFound<RoundManager>();
            RegisterIfFound<ScoreManager>();
            RegisterIfFound<RitualManager>();
            RegisterIfFound<SealManager>();
            RegisterIfFound<HouseManager>();
            RegisterIfFound<UIManager>();
        }

        /// <summary>
        /// Busca el primer objeto activo del tipo indicado y lo registra cuando existe.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo de objeto Unity que se desea exponer como servicio.
        /// </typeparam>
        private static void RegisterIfFound<T>() where T : UnityEngine.Object
        {
            T service = FindAnyObjectByType<T>();

            if (service != null)
            {
                ServiceLocator.Register(service);
            }
        }
    }
}
