using HitoriKakurembo.Network;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Controla exclusivamente la representacion visual del jugador de red.
    /// Mantiene la capsula runtime como base fisica para Netcode y CharacterController, pero oculta su renderer y coloca encima el modelo artistico seleccionado.
    /// </summary>
    public class PlayerVisualModelController : NetworkBehaviour
    {
        /// <summary>
        /// Nombre del hijo que agrupa el modelo visual instanciado.
        /// </summary>
        private const string VisualRootName = "CharacterVisualRoot";

        /// <summary>
        /// Margen usado para que el modelo quede levemente por debajo de la altura total del CharacterController y no atraviese el techo visualmente.
        /// </summary>
        private const float CharacterHeightFill = 0.95f;

        /// <summary>
        /// Raiz donde se instancia el modelo visual seleccionado.
        /// </summary>
        private Transform visualRoot;

        /// <summary>
        /// Instancia actual del FBX cargado como visual del jugador.
        /// </summary>
        private GameObject currentModelInstance;

        /// <summary>
        /// Renderer original de la capsula runtime. Se conserva como fallback si el modelo artistico no puede cargarse.
        /// </summary>
        private Renderer primitiveRenderer;

        /// <summary>
        /// CharacterController que define la altura y base fisica del jugador.
        /// </summary>
        private CharacterController characterController;

        /// <summary>
        /// Estado de red del jugador que informa si debe verse como superviviente o como muneco.
        /// </summary>
        private NetworkPlayer networkPlayer;

        /// <summary>
        /// Indice efectivo aplicado actualmente para evitar reinstanciar el mismo modelo cada frame.
        /// </summary>
        private int appliedModelIndex = int.MinValue;

        /// <summary>
        /// Indica si el visual actual fue aplicado bajo estado de muneco.
        /// </summary>
        private bool appliedDollState;

        /// <summary>
        /// Prepara referencias y muestra un visual inicial para que el objeto no dependa de que la red ya haya spawneado.
        /// </summary>
        private void Awake()
        {
            CacheReferences();
            ApplyVisualModel(PlayerCharacterModelCatalog.DefaultModelIndex, false);
        }

        /// <summary>
        /// Reaplica el visual al completarse el spawn de Netcode, usando los valores sincronizados reales del jugador.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            CacheReferences();

            if (networkPlayer != null)
            {
                ApplyVisualModel(networkPlayer.SelectedCharacterModelIndex, networkPlayer.IsDoll);
            }
        }

        /// <summary>
        /// Aplica el modelo que debe verse para este jugador.
        /// Si el jugador es muneco, el catalogo fuerza el modelo de muneco aunque el usuario haya elegido otro en el lobby.
        /// </summary>
        /// <param name="selectedModelIndex">Indice elegido por el usuario en el lobby.</param>
        /// <param name="isDoll">Estado sincronizado de muneco asignado por el servidor.</param>
        public void ApplyVisualModel(int selectedModelIndex, bool isDoll)
        {
            CacheReferences();

            PlayerCharacterModelDefinition definition = isDoll
                ? PlayerCharacterModelCatalog.GetDollModel()
                : PlayerCharacterModelCatalog.GetModel(selectedModelIndex);

            if (currentModelInstance != null && appliedModelIndex == definition.Index && appliedDollState == isDoll)
            {
                return;
            }

            appliedModelIndex = definition.Index;
            appliedDollState = isDoll;
            ClearCurrentModel();

            GameObject modelPrefab = LoadModelPrefab(definition);

            if (modelPrefab == null)
            {
                SetPrimitiveRendererVisible(true);
                Debug.LogWarning($"No se pudo cargar el modelo de personaje '{definition.DisplayName}' desde '{definition.AssetPath}'. Se mantiene la capsula como fallback visual.");
                return;
            }

            EnsureVisualRoot();
            currentModelInstance = Instantiate(modelPrefab, visualRoot);
            currentModelInstance.name = $"Visual_{definition.DisplayName}";
            currentModelInstance.transform.localPosition = definition.LocalPosition;
            currentModelInstance.transform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
            currentModelInstance.transform.localScale = definition.LocalScale;

            ApplyVisibleChildFilter(currentModelInstance, definition);
            ConfigureAnimator(currentModelInstance, definition);
            NormalizeModelToCharacterController();
            SetPrimitiveRendererVisible(false);
            RefreshOwnerFirstPersonBodyVisibility();
        }

        /// <summary>
        /// Cachea componentes locales sin realizar busquedas globales.
        /// </summary>
        private void CacheReferences()
        {
            primitiveRenderer = primitiveRenderer != null ? primitiveRenderer : GetComponent<Renderer>();
            characterController = characterController != null ? characterController : GetComponent<CharacterController>();
            networkPlayer = networkPlayer != null ? networkPlayer : GetComponent<NetworkPlayer>();
            EnsureVisualRoot();
        }

        /// <summary>
        /// Garantiza que exista una raiz local dedicada al modelo artistico.
        /// </summary>
        private void EnsureVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform existingRoot = transform.Find(VisualRootName);

            if (existingRoot != null)
            {
                visualRoot = existingRoot;
                return;
            }

            GameObject visualRootObject = new GameObject(VisualRootName);
            visualRootObject.transform.SetParent(transform, false);
            visualRootObject.transform.localPosition = Vector3.zero;
            visualRootObject.transform.localRotation = Quaternion.identity;
            visualRootObject.transform.localScale = Vector3.one;
            visualRoot = visualRootObject.transform;
        }

        /// <summary>
        /// Carga el FBX configurado para el modelo.
        /// En esta fase se usa AssetDatabase dentro del editor para evitar mover assets o crear prefabs adicionales.
        /// </summary>
        /// <param name="definition">Definicion visual solicitada.</param>
        /// <returns>Prefab/modelo cargado o null si no esta disponible.</returns>
        private static GameObject LoadModelPrefab(PlayerCharacterModelDefinition definition)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(definition.AssetPath);
#else
            return null;
#endif
        }

        /// <summary>
        /// Deja visible unicamente el submodelo indicado dentro del FBX.
        /// Esto evita que modelos superpuestos del mismo archivo aparezcan al mismo tiempo sin destruir huesos ni jerarquia de animacion.
        /// </summary>
        /// <param name="modelInstance">Instancia completa del FBX.</param>
        /// <param name="definition">Definicion con el nombre del hijo que debe quedar visible.</param>
        private static void ApplyVisibleChildFilter(GameObject modelInstance, PlayerCharacterModelDefinition definition)
        {
            if (modelInstance == null || string.IsNullOrWhiteSpace(definition.VisibleChildName))
            {
                return;
            }

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            Transform visibleChild = FindChildByName(modelInstance.transform, definition.VisibleChildName);
            bool foundAnyRenderer = false;

            foreach (Renderer rendererComponent in renderers)
            {
                if (rendererComponent == null)
                {
                    continue;
                }

                bool shouldBeVisible = visibleChild != null
                    ? IsSameOrChildOf(rendererComponent.transform, visibleChild)
                    : rendererComponent.name.IndexOf(definition.VisibleChildName, System.StringComparison.OrdinalIgnoreCase) >= 0;

                rendererComponent.enabled = shouldBeVisible;
                foundAnyRenderer |= shouldBeVisible;
            }

            if (!foundAnyRenderer)
            {
                foreach (Renderer rendererComponent in renderers)
                {
                    if (rendererComponent != null)
                    {
                        rendererComponent.enabled = true;
                    }
                }

                Debug.LogWarning($"El modelo '{definition.DisplayName}' no encontro el subobjeto visible '{definition.VisibleChildName}'. Revisa la asignacion en PlayerCharacterModelCatalog.");
            }
        }

        /// <summary>
        /// Busca un hijo por nombre sin depender de mayusculas/minusculas ni de la profundidad dentro del FBX.
        /// </summary>
        /// <param name="root">Raiz de busqueda.</param>
        /// <param name="childName">Nombre del hijo solicitado.</param>
        /// <returns>Transform encontrado o null.</returns>
        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// Determina si un transform es el hijo seleccionado o pertenece a su jerarquia.
        /// </summary>
        /// <param name="candidate">Transform a evaluar.</param>
        /// <param name="expectedParent">Submodelo visible esperado.</param>
        /// <returns>True cuando el renderer pertenece al submodelo solicitado.</returns>
        private static bool IsSameOrChildOf(Transform candidate, Transform expectedParent)
        {
            Transform current = candidate;

            while (current != null)
            {
                if (current == expectedParent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Asigna el Animator Controller propio del modelo cuando existe.
        /// Esto permite que cada FBX conserve su controlador artistico sin acoplar el sistema de red a estados de animacion concretos.
        /// </summary>
        /// <param name="modelInstance">Instancia visual creada.</param>
        /// <param name="definition">Definicion de modelo con la ruta del controller.</param>
        private static void ConfigureAnimator(GameObject modelInstance, PlayerCharacterModelDefinition definition)
        {
            if (modelInstance == null)
            {
                return;
            }

            Animator animator = modelInstance.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;

            if (string.IsNullOrWhiteSpace(definition.AnimatorControllerPath))
            {
                return;
            }

#if UNITY_EDITOR
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(definition.AnimatorControllerPath);

            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
#endif
        }

        /// <summary>
        /// Ajusta el tamano y la posicion del modelo para que encaje con la capsula fisica usada por el CharacterController.
        /// </summary>
        private void NormalizeModelToCharacterController()
        {
            if (currentModelInstance == null || visualRoot == null)
            {
                return;
            }

            Renderer[] renderers = currentModelInstance.GetComponentsInChildren<Renderer>(true);

            if (!TryCalculateBounds(renderers, out Bounds bounds) || bounds.size.y <= 0.001f)
            {
                return;
            }

            float controllerHeight = characterController != null ? characterController.height : 2f;
            float targetHeight = Mathf.Max(0.1f, controllerHeight * CharacterHeightFill);
            float scaleFactor = Mathf.Clamp(targetHeight / bounds.size.y, 0.01f, 100f);
            currentModelInstance.transform.localScale *= scaleFactor;

            renderers = currentModelInstance.GetComponentsInChildren<Renderer>(true);

            if (!TryCalculateBounds(renderers, out bounds))
            {
                return;
            }

            float controllerBottom = characterController != null
                ? transform.position.y + characterController.center.y - (characterController.height * 0.5f)
                : transform.position.y - 1f;

            Vector3 worldOffset = new Vector3(
                transform.position.x - bounds.center.x,
                controllerBottom - bounds.min.y,
                transform.position.z - bounds.center.z);

            visualRoot.position += worldOffset;
        }

        /// <summary>
        /// Calcula los bounds combinados de todos los renderers del modelo.
        /// </summary>
        /// <param name="renderers">Renderers encontrados en la instancia visual.</param>
        /// <param name="bounds">Bounds combinados resultantes.</param>
        /// <returns>True cuando existe al menos un renderer valido.</returns>
        private static bool TryCalculateBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (renderers == null)
            {
                return false;
            }

            foreach (Renderer rendererComponent in renderers)
            {
                if (rendererComponent == null || !rendererComponent.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererComponent.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(rendererComponent.bounds);
            }

            return hasBounds;
        }

        /// <summary>
        /// Elimina la instancia visual anterior antes de crear una nueva.
        /// </summary>
        private void ClearCurrentModel()
        {
            if (visualRoot != null)
            {
                for (int i = visualRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = visualRoot.GetChild(i);

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                    else
#endif
                    {
                        Destroy(child.gameObject);
                    }
                }

                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
            }

            currentModelInstance = null;
        }

        /// <summary>
        /// Muestra u oculta el renderer de capsula que queda como fallback visual.
        /// </summary>
        /// <param name="visible">True para mostrar la capsula; false para ocultarla.</param>
        private void SetPrimitiveRendererVisible(bool visible)
        {
            if (primitiveRenderer != null)
            {
                primitiveRenderer.enabled = visible;
            }
        }

        /// <summary>
        /// Si el jugador local esta en primera persona, solicita refrescar los renderers ocultos para que el nuevo modelo no tape la camara.
        /// </summary>
        private void RefreshOwnerFirstPersonBodyVisibility()
        {
            PlayerFirstPersonCamera firstPersonCamera = GetComponent<PlayerFirstPersonCamera>();
            firstPersonCamera?.RefreshLocalBodyVisibility();
        }
    }
}
