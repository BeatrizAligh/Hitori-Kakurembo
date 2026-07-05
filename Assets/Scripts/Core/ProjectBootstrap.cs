using HitoriKakurembo.Network;
using HitoriKakurembo.UI;
using UnityEngine;

namespace HitoriKakurembo.Core
{
    /// <summary>
    /// Bootstrap legado del prototipo Hitori Kakurembo.
    /// Queda desactivado por defecto porque el flujo multiplayer modular vive en Assets/TestMultiplayer.
    /// </summary>
    [System.Obsolete("Legacy bootstrap desactivado por defecto. Usa TestMultiplayer o define HITORI_KAKUREMBO_LEGACY_BOOTSTRAP para reactivarlo.")]
    public static class ProjectBootstrap
    {
        /// <summary>
        /// Crea el contenedor persistente de sistemas antes de que Unity cargue la primera escena jugable.
        /// Para reactivar este flujo viejo agrega HITORI_KAKUREMBO_LEGACY_BOOTSTRAP a Scripting Define Symbols.
        /// </summary>
#if HITORI_KAKUREMBO_LEGACY_BOOTSTRAP
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void Initialize()
        {
#if !HITORI_KAKUREMBO_LEGACY_BOOTSTRAP
            return;
#else
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
#endif
        }
    }
}
