using HitoriKakurembo.House;
using HitoriKakurembo.Ritual;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Seals;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitoriKakurembo.Core
{
    /// <summary>
    /// Monta la composicion minima de escena necesaria para que el loop multijugador funcione aun cuando las escenas sigan casi vacias.
    /// </summary>
    public class PrototypeSceneInstaller : MonoBehaviour
    {
        /// <summary>
        /// Nombre canonico de la escena de lobby donde los jugadores esperan antes de iniciar la partida.
        /// </summary>
        private const string LobbySceneName = "LobbyScene";

        /// <summary>
        /// Nombre canonico de la escena principal de juego donde se crea el entorno 3D provisional.
        /// </summary>
        private const string GameSceneName = "GameScene";

        /// <summary>
        /// Cantidad de sellos placeholder que debe existir en la escena de juego del prototipo.
        /// </summary>
        private const int DefaultSealCount = 6;

        /// <summary>
        /// Cantidad de espejos placeholder usados para probar teletransporte del muneco.
        /// </summary>
        private const int DefaultMirrorCount = 4;

        /// <summary>
        /// Se suscribe a la carga de escenas para ensamblar los objetos faltantes segun el contexto.
        /// </summary>
        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        /// <summary>
        /// Cancela la suscripcion a la carga de escenas cuando el instalador se desactiva.
        /// </summary>
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// Ensambla la escena actualmente activa al iniciar la aplicacion.
        /// </summary>
        private void Start()
        {
            EnsureSceneComposition(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Revisa la escena cargada y crea la infraestructura de prototipo que aun no exista.
        /// </summary>
        /// <param name="scene">
        /// Escena que Unity acaba de terminar de cargar.
        /// </param>
        /// <param name="loadSceneMode">
        /// Modo con el que se realizo la carga.
        /// </param>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            EnsureSceneComposition(scene);
        }

        /// <summary>
        /// Crea la composicion minima requerida para la escena indicada.
        /// </summary>
        /// <param name="scene">
        /// Escena que debe validarse y completarse.
        /// </param>
        private void EnsureSceneComposition(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            if (scene.name == LobbySceneName)
            {
                EnsureLobbyWorld(scene);
                return;
            }

            if (scene.name != GameSceneName)
            {
                return;
            }

            GameObject gameplayManagersRoot = EnsureGameplayManagers(scene);
            EnsureGameplayWorld(scene, gameplayManagersRoot);
        }

        /// <summary>
        /// Crea una composicion fisica minima para que los jugadores no caigan mientras esperan en lobby.
        /// </summary>
        /// <param name="scene">
        /// Escena de lobby actualmente cargada.
        /// </param>
        private void EnsureLobbyWorld(Scene scene)
        {
            EnsureMainCamera(scene);
            EnsureDirectionalLight(scene);
            EnsureFloor(scene, "PrototypeLobbyFloor", new Vector3(4f, 1f, 4f), new Color(0.16f, 0.2f, 0.24f, 1f));
        }

        /// <summary>
        /// Garantiza que la escena de juego tenga los managers basicos de ronda, ritual, casa y sellos.
        /// </summary>
        /// <param name="scene">
        /// Escena sobre la que deben quedar creados los managers.
        /// </param>
        /// <returns>
        /// Objeto raiz que contiene la agrupacion local de managers de gameplay.
        /// </returns>
        private GameObject EnsureGameplayManagers(Scene scene)
        {
            GameObject managersRoot = GameObject.Find("PrototypeGameplayManagers");

            if (managersRoot == null)
            {
                managersRoot = new GameObject("PrototypeGameplayManagers");
                SceneManager.MoveGameObjectToScene(managersRoot, scene);
                managersRoot.SetActive(false);
                managersRoot.AddComponent<DynamicRoomManager>();
                managersRoot.AddComponent<SafeZoneManager>();
                managersRoot.AddComponent<RitualManager>();
                managersRoot.AddComponent<SealManager>();
                managersRoot.AddComponent<RoundManager>();
                managersRoot.AddComponent<ScoreManager>();
                managersRoot.AddComponent<HouseManager>();
                managersRoot.SetActive(true);
                return managersRoot;
            }

            EnsureComponent<DynamicRoomManager>(managersRoot);
            EnsureComponent<SafeZoneManager>(managersRoot);
            EnsureComponent<RitualManager>(managersRoot);
            EnsureComponent<SealManager>(managersRoot);
            EnsureComponent<RoundManager>(managersRoot);
            EnsureComponent<ScoreManager>(managersRoot);
            EnsureComponent<HouseManager>(managersRoot);
            return managersRoot;
        }

        /// <summary>
        /// Crea los elementos visuales y espaciales minimos para poder probar el flujo dentro de la escena de juego.
        /// </summary>
        /// <param name="scene">
        /// Escena de juego actualmente cargada.
        /// </param>
        /// <param name="gameplayManagersRoot">
        /// Objeto que contiene los managers de gameplay ya asegurados por el instalador.
        /// </param>
        private void EnsureGameplayWorld(Scene scene, GameObject gameplayManagersRoot)
        {
            EnsureMainCamera(scene);
            EnsureDirectionalLight(scene);
            EnsureFloor(scene, "PrototypeFloor", new Vector3(3f, 1f, 3f), new Color(0.18f, 0.22f, 0.25f, 1f));
            EnsureSafeZone(scene, gameplayManagersRoot);
            EnsureSealPlaceholders(scene);
            EnsureMirrorPlaceholders(scene);
        }

        /// <summary>
        /// Garantiza una camara principal de prueba para observar el mapa y a los jugadores spawneados.
        /// </summary>
        /// <param name="scene">
        /// Escena donde debe existir la camara.
        /// </param>
        private static void EnsureMainCamera(Scene scene)
        {
            if (Camera.main != null || Object.FindAnyObjectByType<Camera>() != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("PrototypeMainCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            cameraObject.transform.position = new Vector3(0f, 12f, -14f);
            cameraObject.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
        }

        /// <summary>
        /// Crea una luz direccional de apoyo cuando la escena de juego todavia no tiene iluminacion.
        /// </summary>
        /// <param name="scene">
        /// Escena donde debe existir la luz principal.
        /// </param>
        private static void EnsureDirectionalLight(Scene scene)
        {
            if (Object.FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("PrototypeDirectionalLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);

            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        /// <summary>
        /// Crea un plano simple que actua como suelo de navegacion para el movimiento provisional del jugador.
        /// </summary>
        /// <param name="scene">
        /// Escena donde debe existir el suelo.
        /// </param>
        private static void EnsureFloor(Scene scene, string floorName, Vector3 scale, Color color)
        {
            if (GameObject.Find(floorName) != null)
            {
                return;
            }

            GameObject floorObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorObject.name = floorName;
            SceneManager.MoveGameObjectToScene(floorObject, scene);
            floorObject.transform.position = Vector3.zero;
            floorObject.transform.localScale = scale;

            Renderer rendererComponent = floorObject.GetComponent<Renderer>();

            if (rendererComponent != null)
            {
                rendererComponent.material.color = color;
            }
        }

        /// <summary>
        /// Crea un volumen de zona segura visible y lo conecta con el <see cref="SafeZoneManager"/> de la escena.
        /// </summary>
        /// <param name="scene">
        /// Escena donde debe existir la zona segura.
        /// </param>
        /// <param name="gameplayManagersRoot">
        /// Objeto que contiene los managers de gameplay y desde el cual se resuelve el <see cref="SafeZoneManager"/>.
        /// </param>
        private static void EnsureSafeZone(Scene scene, GameObject gameplayManagersRoot)
        {
            GameObject safeZoneObject = GameObject.Find("PrototypeSafeZone");

            if (safeZoneObject == null)
            {
                safeZoneObject = new GameObject("PrototypeSafeZone");
                SceneManager.MoveGameObjectToScene(safeZoneObject, scene);

                BoxCollider zoneCollider = safeZoneObject.AddComponent<BoxCollider>();
                zoneCollider.isTrigger = true;
                zoneCollider.center = new Vector3(0f, 2f, 0f);
                zoneCollider.size = new Vector3(18f, 4f, 18f);
            }

            SafeZoneManager safeZoneManager = gameplayManagersRoot.GetComponent<SafeZoneManager>();
            Collider visibleZoneCollider = safeZoneObject.GetComponent<Collider>();

            if (safeZoneManager != null && visibleZoneCollider != null)
            {
                safeZoneManager.SetVisibleZoneCollider(visibleZoneCollider);
            }
        }

        /// <summary>
        /// Crea placeholders de sellos rituales y los registra dentro del <see cref="SealManager"/> de la escena.
        /// </summary>
        /// <param name="scene">
        /// Escena donde deben existir los sellos de prueba.
        /// </param>
        private static void EnsureSealPlaceholders(Scene scene)
        {
            GameObject sealsRoot = GameObject.Find("PrototypeSeals");

            if (sealsRoot == null)
            {
                sealsRoot = new GameObject("PrototypeSeals");
                SceneManager.MoveGameObjectToScene(sealsRoot, scene);
            }

            SealManager sealManager = Object.FindAnyObjectByType<SealManager>();

            if (sealManager == null)
            {
                return;
            }

            for (int sealIndex = 0; sealIndex < DefaultSealCount; sealIndex++)
            {
                string sealObjectName = $"PrototypeSeal_{sealIndex + 1}";
                Transform existingSealTransform = sealsRoot.transform.Find(sealObjectName);
                RitualSeal ritualSeal = existingSealTransform != null
                    ? existingSealTransform.GetComponent<RitualSeal>()
                    : null;

                if (ritualSeal == null)
                {
                    GameObject sealObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    sealObject.name = sealObjectName;
                    SceneManager.MoveGameObjectToScene(sealObject, scene);
                    sealObject.transform.SetParent(sealsRoot.transform, false);
                    sealObject.transform.position = GetSealPosition(sealIndex, DefaultSealCount);
                    sealObject.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);

                    Renderer rendererComponent = sealObject.GetComponent<Renderer>();

                    if (rendererComponent != null)
                    {
                        rendererComponent.material.color = new Color(0.92f, 0.76f, 0.32f, 1f);
                    }

                    ritualSeal = sealObject.AddComponent<RitualSeal>();
                }

                ritualSeal.SetSealIndex(sealIndex);
                EnsureSealActivationZone(sealObjectName, ritualSeal);
                sealManager.RegisterSeal(ritualSeal);
            }
        }

        /// <summary>
        /// Garantiza que cada sello placeholder tenga una zona de activacion asociada al mismo objeto.
        /// </summary>
        /// <param name="sealObjectName">
        /// Nombre del objeto de sello que debe contener la zona.
        /// </param>
        /// <param name="ritualSeal">
        /// Sello ritual que controlara la zona interactiva.
        /// </param>
        private static void EnsureSealActivationZone(string sealObjectName, RitualSeal ritualSeal)
        {
            if (ritualSeal == null)
            {
                return;
            }

            GameObject sealObject = ritualSeal.gameObject;
            SealActivationZone activationZone = sealObject.GetComponent<SealActivationZone>();

            if (activationZone == null)
            {
                activationZone = sealObject.AddComponent<SealActivationZone>();
            }

            if (activationZone != null)
            {
                activationZone.SetTargetSeal(ritualSeal);
            }

            SphereCollider zoneCollider = sealObject.GetComponent<SphereCollider>();

            if (zoneCollider == null)
            {
                zoneCollider = sealObject.AddComponent<SphereCollider>();
            }

            if (zoneCollider == null)
            {
                Debug.LogWarning($"No se pudo crear SphereCollider para la zona de activacion del sello {sealObjectName}.");
                return;
            }

            zoneCollider.isTrigger = true;
            zoneCollider.radius = 2.2f;
            zoneCollider.center = Vector3.zero;
            sealObject.name = sealObjectName;
        }

        /// <summary>
        /// Calcula una posicion circular simple para distribuir visualmente los sellos placeholder en la escena.
        /// </summary>
        /// <param name="sealIndex">
        /// Indice del sello cuya posicion debe calcularse.
        /// </param>
        /// <param name="totalSeals">
        /// Cantidad total de sellos que se distribuiran.
        /// </param>
        /// <returns>
        /// Posicion resultante para el sello solicitado.
        /// </returns>
        private static Vector3 GetSealPosition(int sealIndex, int totalSeals)
        {
            float angle = (Mathf.PI * 2f * sealIndex) / totalSeals;
            float radius = 7f;
            return new Vector3(Mathf.Cos(angle) * radius, 0.2f, Mathf.Sin(angle) * radius);
        }

        /// <summary>
        /// Crea espejos placeholder enlazados para probar el teletransporte del muneco sin assets definitivos.
        /// </summary>
        /// <param name="scene">
        /// Escena donde deben existir los espejos de prueba.
        /// </param>
        private static void EnsureMirrorPlaceholders(Scene scene)
        {
            GameObject mirrorsRoot = GameObject.Find("PrototypeMirrors");

            if (mirrorsRoot == null)
            {
                mirrorsRoot = new GameObject("PrototypeMirrors");
                SceneManager.MoveGameObjectToScene(mirrorsRoot, scene);
            }

            MirrorPortal[] portals = new MirrorPortal[DefaultMirrorCount];

            for (int mirrorIndex = 0; mirrorIndex < DefaultMirrorCount; mirrorIndex++)
            {
                string mirrorObjectName = $"PrototypeMirror_{mirrorIndex + 1}";
                Transform existingMirrorTransform = mirrorsRoot.transform.Find(mirrorObjectName);
                MirrorPortal mirrorPortal = existingMirrorTransform != null
                    ? existingMirrorTransform.GetComponent<MirrorPortal>()
                    : null;

                if (mirrorPortal == null)
                {
                    GameObject mirrorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mirrorObject.name = mirrorObjectName;
                    SceneManager.MoveGameObjectToScene(mirrorObject, scene);
                    mirrorObject.transform.SetParent(mirrorsRoot.transform, false);
                    mirrorObject.transform.position = GetMirrorPosition(mirrorIndex);
                    mirrorObject.transform.rotation = GetMirrorRotation(mirrorIndex);
                    mirrorObject.transform.localScale = new Vector3(1.2f, 2.2f, 0.15f);

                    Renderer rendererComponent = mirrorObject.GetComponent<Renderer>();

                    if (rendererComponent != null)
                    {
                        rendererComponent.material.color = new Color(0.38f, 0.68f, 0.9f, 1f);
                    }

                    mirrorPortal = mirrorObject.AddComponent<MirrorPortal>();
                }

                mirrorPortal.SetPortalIndex(mirrorIndex);
                portals[mirrorIndex] = mirrorPortal;
            }

            for (int mirrorIndex = 0; mirrorIndex < portals.Length; mirrorIndex++)
            {
                MirrorPortal portal = portals[mirrorIndex];

                if (portal == null)
                {
                    continue;
                }

                int linkedIndex = (mirrorIndex + 2) % portals.Length;
                portal.SetLinkedPortal(portals[linkedIndex]);
            }
        }

        /// <summary>
        /// Devuelve una posicion cardinal para distribuir espejos de prueba alrededor del mapa.
        /// </summary>
        /// <param name="mirrorIndex">
        /// Indice del espejo solicitado.
        /// </param>
        /// <returns>
        /// Posicion mundial del espejo.
        /// </returns>
        private static Vector3 GetMirrorPosition(int mirrorIndex)
        {
            switch (mirrorIndex)
            {
                case 0:
                    return new Vector3(0f, 1.15f, 10f);
                case 1:
                    return new Vector3(10f, 1.15f, 0f);
                case 2:
                    return new Vector3(0f, 1.15f, -10f);
                default:
                    return new Vector3(-10f, 1.15f, 0f);
            }
        }

        /// <summary>
        /// Orienta cada espejo hacia el centro del mapa para facilitar pruebas visuales.
        /// </summary>
        /// <param name="mirrorIndex">
        /// Indice del espejo solicitado.
        /// </param>
        /// <returns>
        /// Rotacion mundial del espejo.
        /// </returns>
        private static Quaternion GetMirrorRotation(int mirrorIndex)
        {
            Vector3 position = GetMirrorPosition(mirrorIndex);
            Vector3 directionToCenter = (Vector3.zero - position).normalized;
            directionToCenter.y = 0f;
            return directionToCenter.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(directionToCenter, Vector3.up)
                : Quaternion.identity;
        }

        /// <summary>
        /// Agrega el componente solicitado al objeto cuando aun no existe una instancia del mismo tipo.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo de componente Unity que debe asegurarse sobre el objeto.
        /// </typeparam>
        /// <param name="target">
        /// GameObject que debe contener el componente solicitado.
        /// </param>
        private static void EnsureComponent<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }
    }
}
