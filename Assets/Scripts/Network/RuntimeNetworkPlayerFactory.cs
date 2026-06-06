using HitoriKakurembo.Doll;
using HitoriKakurembo.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HitoriKakurembo.Network
{
    /// <summary>
    /// Construye el prefab de jugador de red utilizado por el prototipo cuando aun no existe un prefab authoring creado desde el editor.
    /// </summary>
    public static class RuntimeNetworkPlayerFactory
    {
        /// <summary>
        /// Nombre canonico utilizado para identificar el prefab runtime dentro de la jerarquia persistente.
        /// </summary>
        private const string RuntimePlayerPrefabName = "RuntimeNetworkPlayerPrefab";

        /// <summary>
        /// Crea una plantilla de jugador de red completamente funcional para NGO.
        /// </summary>
        /// <returns>
        /// GameObject desactivado que actua como prefab runtime y que puede registrarse en el <see cref="NetworkManager"/>.
        /// </returns>
        public static GameObject CreatePlayerPrefab()
        {
            GameObject playerPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerPrefab.name = RuntimePlayerPrefabName;
            playerPrefab.SetActive(false);

            playerPrefab.transform.position = Vector3.zero;
            playerPrefab.transform.rotation = Quaternion.identity;
            playerPrefab.transform.localScale = Vector3.one;

            Renderer rendererComponent = playerPrefab.GetComponent<Renderer>();

            if (rendererComponent != null)
            {
                rendererComponent.material.color = new Color(0.83f, 0.9f, 1f, 1f);
            }

            CapsuleCollider capsuleCollider = playerPrefab.GetComponent<CapsuleCollider>();

            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = false;
                Object.Destroy(capsuleCollider);
            }

            CharacterController characterController = playerPrefab.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.45f;
            characterController.center = Vector3.zero;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 45f;

            playerPrefab.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = playerPrefab.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncPositionX = true;
            networkTransform.SyncPositionY = true;
            networkTransform.SyncPositionZ = true;
            networkTransform.SyncRotAngleX = false;
            networkTransform.SyncRotAngleY = true;
            networkTransform.SyncRotAngleZ = false;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;
            networkTransform.PositionThreshold = 0.01f;
            networkTransform.RotAngleThreshold = 0.5f;
            networkTransform.UseUnreliableDeltas = true;
            networkTransform.Interpolate = true;
            playerPrefab.AddComponent<NetworkPlayer>();
            playerPrefab.AddComponent<PlayerController>();
            playerPrefab.AddComponent<PlayerFirstPersonCamera>();
            playerPrefab.AddComponent<PlayerInteraction>();
            playerPrefab.AddComponent<PlayerInventory>();
            playerPrefab.AddComponent<PlayerRoleHandler>();
            playerPrefab.AddComponent<PlayerVisibilityHandler>();
            playerPrefab.AddComponent<DollAbilityManager>();
            playerPrefab.AddComponent<DollMirrorTeleport>();
            playerPrefab.AddComponent<DollTrapManager>();

            return playerPrefab;
        }
    }

    /// <summary>
    /// Instanciador custom para que el prefab runtime pueda permanecer oculto como plantilla,
    /// pero cada jugador real aparezca activo, visible y listo para recibir input/sincronizacion.
    /// </summary>
    internal sealed class RuntimeNetworkPlayerPrefabHandler : INetworkPrefabInstanceHandler
    {
        /// <summary>
        /// Plantilla registrada en el NetworkManager como prefab de jugador.
        /// </summary>
        private readonly GameObject playerPrefabTemplate;

        /// <summary>
        /// Crea el handler con la plantilla runtime que NGO debe usar como origen del hash de prefab.
        /// </summary>
        /// <param name="playerPrefabTemplate">
        /// Objeto plantilla creado por <see cref="RuntimeNetworkPlayerFactory"/>.
        /// </param>
        public RuntimeNetworkPlayerPrefabHandler(GameObject playerPrefabTemplate)
        {
            this.playerPrefabTemplate = playerPrefabTemplate;
        }

        /// <summary>
        /// Crea una instancia activa del jugador cuando NGO necesita materializar el prefab en servidor, host o cliente.
        /// </summary>
        /// <param name="ownerClientId">
        /// Client id que sera propietario del NetworkObject instanciado.
        /// </param>
        /// <param name="position">
        /// Posicion inicial recibida desde la aprobacion de conexion o el mensaje de spawn.
        /// </param>
        /// <param name="rotation">
        /// Rotacion inicial recibida desde la aprobacion de conexion o el mensaje de spawn.
        /// </param>
        /// <returns>
        /// NetworkObject activo que NGO puede registrar como PlayerObject.
        /// </returns>
        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            if (playerPrefabTemplate == null)
            {
                Debug.LogError("No se pudo instanciar el jugador de red porque la plantilla runtime es nula.");
                return null;
            }

            GameObject playerInstance = Object.Instantiate(playerPrefabTemplate, position, rotation);
            playerInstance.name = $"NetworkPlayer_{ownerClientId}";
            playerInstance.SetActive(true);

            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("La instancia de jugador runtime no contiene NetworkObject.");
                Object.Destroy(playerInstance);
                return null;
            }

            return networkObject;
        }

        /// <summary>
        /// Destruye la instancia de jugador cuando NGO despawnea el objeto definitivamente.
        /// </summary>
        /// <param name="networkObject">
        /// NetworkObject de jugador que debe liberarse.
        /// </param>
        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null)
            {
                Object.Destroy(networkObject.gameObject);
            }
        }
    }
}
