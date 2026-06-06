using HitoriKakurembo.Network;
using HitoriKakurembo.UI;
using UnityEngine;

namespace HitoriKakurembo.Core
{
    /// <summary>
    /// Garantiza que la infraestructura minima del prototipo exista desde cualquier escena sin requerir configuracion manual previa.
    /// </summary>
    public static class ProjectBootstrap
    {
        /// <summary>
        /// Crea el contenedor persistente de sistemas antes de que Unity cargue la primera escena jugable.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            bool hasGameManager = Object.FindAnyObjectByType<GameManager>() != null;
            bool hasSceneLoader = Object.FindAnyObjectByType<SceneLoader>() != null;
            bool hasRelaySessionManager = Object.FindAnyObjectByType<RelaySessionManager>() != null;
            bool hasSceneInstaller = Object.FindAnyObjectByType<PrototypeSceneInstaller>() != null;
            bool hasSessionUi = Object.FindAnyObjectByType<PrototypeSessionUI>() != null;

            if (hasGameManager && hasSceneLoader && hasRelaySessionManager && hasSceneInstaller && hasSessionUi)
            {
                return;
            }

            GameObject bootstrapRoot = new GameObject("HK_ProjectBootstrap");
            Object.DontDestroyOnLoad(bootstrapRoot);
            bootstrapRoot.SetActive(false);

            if (!hasGameManager)
            {
                bootstrapRoot.AddComponent<GameManager>();
            }

            if (!hasSceneLoader)
            {
                bootstrapRoot.AddComponent<SceneLoader>();
            }

            if (!hasRelaySessionManager)
            {
                bootstrapRoot.AddComponent<RelaySessionManager>();
            }

            if (!hasSceneInstaller)
            {
                bootstrapRoot.AddComponent<PrototypeSceneInstaller>();
            }

            if (!hasSessionUi)
            {
                bootstrapRoot.AddComponent<PrototypeSessionUI>();
            }

            bootstrapRoot.SetActive(true);
        }
    }
}
