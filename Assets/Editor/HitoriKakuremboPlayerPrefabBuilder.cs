using System;
using HitoriKakurembo.Doll;
using HitoriKakurembo.Network;
using HitoriKakurembo.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace HitoriKakurembo.EditorTools
{
    /// <summary>
    /// Utilidad de editor para construir el prefab de jugador multiplayer del prototipo.
    /// Centraliza los componentes requeridos para evitar crear prefabs incompletos o con scripts faltantes al actualizar el Player System.
    /// </summary>
    public static class HitoriKakuremboPlayerPrefabBuilder
    {
        /// <summary>
        /// Ruta final donde queda guardado el prefab autorado de jugador.
        /// </summary>
        private const string PlayerPrefabPath = "Assets/Prefabs/Players/NetworkPlayer.prefab";

        /// <summary>
        /// Ruta del script de interaccion nuevo agregado por el usuario.
        /// Se carga por asset para adjuntarlo sin modificar su contenido.
        /// </summary>
        private const string ExternalPlayerInteractionScriptPath = "Assets/Scripts/Interactables/PlayerInteraction.cs";

        /// <summary>
        /// Nombre del hijo visual que usara PlayerVisualModelController para instanciar modelos.
        /// </summary>
        private const string VisualRootName = "CharacterVisualRoot";

        /// <summary>
        /// Crea o reemplaza el prefab de jugador multiplayer en Assets/Prefabs/Players.
        /// </summary>
        [MenuItem("Hitori Kakurembo/Build Network Player Prefab")]
        public static void CreateNetworkPlayerPrefab()
        {
            EnsurePrefabDirectory();

            GameObject playerObject = new GameObject("NetworkPlayer");
            playerObject.tag = "Player";
            playerObject.transform.position = Vector3.zero;
            playerObject.transform.rotation = Quaternion.identity;
            playerObject.transform.localScale = Vector3.one;

            ConfigureCharacterController(playerObject.AddComponent<CharacterController>());
            playerObject.AddComponent<NetworkObject>();
            ConfigureNetworkTransform(playerObject.AddComponent<NetworkTransform>());
            playerObject.AddComponent<NetworkPlayer>();
            playerObject.AddComponent<PlayerVisualModelController>();
            playerObject.AddComponent<PlayerController>();
            playerObject.AddComponent<PlayerFirstPersonCamera>();
            playerObject.AddComponent<HitoriKakurembo.Player.PlayerInteraction>();
            playerObject.AddComponent<PlayerInventory>();
            playerObject.AddComponent<PlayerRoleHandler>();
            playerObject.AddComponent<PlayerVisibilityHandler>();
            playerObject.AddComponent<DollAbilityManager>();
            playerObject.AddComponent<DollMirrorTeleport>();
            playerObject.AddComponent<DollTrapManager>();
            AddExternalPlayerInteraction(playerObject);
            EnsureVisualRoot(playerObject.transform);

            PrefabUtility.SaveAsPrefabAsset(playerObject, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(playerObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prefab de jugador multiplayer creado en {PlayerPrefabPath}.");
        }

        /// <summary>
        /// Crea la carpeta de destino si aun no existe.
        /// </summary>
        private static void EnsurePrefabDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Players"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Players");
            }
        }

        /// <summary>
        /// Configura el CharacterController que mantiene la colision fisica del jugador aunque el visual sea un modelo artistico.
        /// </summary>
        /// <param name="characterController">Controlador a configurar.</param>
        private static void ConfigureCharacterController(CharacterController characterController)
        {
            characterController.height = 2f;
            characterController.radius = 0.45f;
            characterController.center = Vector3.zero;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 45f;
        }

        /// <summary>
        /// Configura NetworkTransform con autoridad del owner para conservar el comportamiento actual de movimiento sincronizado.
        /// </summary>
        /// <param name="networkTransform">Transform de red a configurar.</param>
        private static void ConfigureNetworkTransform(NetworkTransform networkTransform)
        {
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
        }

        /// <summary>
        /// Adjunta el PlayerInteraction externo usando MonoScript para evitar ambiguedad con HitoriKakurembo.Player.PlayerInteraction.
        /// </summary>
        /// <param name="playerObject">GameObject raiz del prefab.</param>
        private static void AddExternalPlayerInteraction(GameObject playerObject)
        {
            MonoScript interactionScript = AssetDatabase.LoadAssetAtPath<MonoScript>(ExternalPlayerInteractionScriptPath);

            if (interactionScript == null)
            {
                Debug.LogWarning($"No se encontro el script externo de interaccion en {ExternalPlayerInteractionScriptPath}. El prefab se creo sin ese componente.");
                return;
            }

            Type interactionType = interactionScript.GetClass();

            if (interactionType == null || !typeof(Component).IsAssignableFrom(interactionType))
            {
                Debug.LogWarning($"El script {ExternalPlayerInteractionScriptPath} no expone una clase Component valida. El prefab se creo sin ese componente.");
                return;
            }

            playerObject.AddComponent(interactionType);
        }

        /// <summary>
        /// Crea una raiz visual vacia para que el controlador de modelos tenga un punto estable de instanciacion.
        /// </summary>
        /// <param name="playerTransform">Transform raiz del jugador.</param>
        private static void EnsureVisualRoot(Transform playerTransform)
        {
            GameObject visualRootObject = new GameObject(VisualRootName);
            visualRootObject.transform.SetParent(playerTransform, false);
            visualRootObject.transform.localPosition = Vector3.zero;
            visualRootObject.transform.localRotation = Quaternion.identity;
            visualRootObject.transform.localScale = Vector3.one;
        }
    }
}
