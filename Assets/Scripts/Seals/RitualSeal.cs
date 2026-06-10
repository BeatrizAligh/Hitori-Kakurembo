using System;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Representa un sello ritual individual dentro de la escena.
    /// El servidor controla su estado y los clientes solo solicitan activacion o desactivacion mediante RPC.
    /// </summary>
    public class RitualSeal : NetworkBehaviour
    {
        private static readonly Color InactiveColor = new Color(0.92f, 0.76f, 0.32f, 1f);
        private static readonly Color ActivatingColor = new Color(0.95f, 0.95f, 0.42f, 1f);
        private static readonly Color ActiveColor = new Color(0.34f, 1f, 0.72f, 1f);
        private static readonly Color DeactivatingColor = new Color(1f, 0.5f, 0.28f, 1f);
        private static readonly Color CorruptedColor = new Color(0.7f, 0.18f, 1f, 1f);
        private static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.28f, 1f);

        /// <summary>
        /// Definicion editable que describe tipo, duraciones, prefab y efectos del sello.
        /// </summary>
        [SerializeField] private SealDefinition sealDefinition;

        /// <summary>
        /// Indice logico del sello dentro del conjunto requerido por la ronda.
        /// </summary>
        [SerializeField] private int sealIndex;

        /// <summary>
        /// Estado local cacheado para objetos no spawneados por Netcode o snapshots legacy.
        /// </summary>
        [SerializeField] private SealState currentStateCache = SealState.Inactive;

        /// <summary>
        /// Client id del jugador que esta activando o desactivando el sello.
        /// </summary>
        [SerializeField] private ulong activatingPlayerClientIdCache = ulong.MaxValue;

        /// <summary>
        /// Progreso local de activacion/desactivacion.
        /// </summary>
        [SerializeField] private float activationProgressCache;

        /// <summary>
        /// Indica si el sello fue creado por el sistema de spawn.
        /// </summary>
        [SerializeField] private bool isSpawnedBySystemCache;

        /// <summary>
        /// Distancia maxima local de interaccion usada por este sello cuando valida directamente.
        /// </summary>
        [SerializeField] private float interactionRange = 3f;

        /// <summary>
        /// Renderer cacheado para color de debug.
        /// </summary>
        private Renderer cachedRenderer;

        private readonly NetworkVariable<SealState> currentState = new NetworkVariable<SealState>(
            SealState.Inactive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> activatingPlayerClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> networkActivationProgress = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isSpawnedBySystem = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> networkSealIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Evento local emitido cuando cambia el estado del sello.
        /// </summary>
        public event Action<RitualSeal, SealState> StateChanged;

        public SealDefinition Definition => sealDefinition;
        public int SealIndex => sealIndex;
        public SealState CurrentState => currentStateCache;
        public bool IsActivated => currentStateCache == SealState.Active;
        public ulong ActivatingPlayerClientId => activatingPlayerClientIdCache;
        public ulong ActivatedByClientId => IsActivated ? activatingPlayerClientIdCache : ulong.MaxValue;
        public float NetworkActivationProgress => activationProgressCache;
        public bool IsSpawnedBySystem => isSpawnedBySystemCache;

        /// <summary>
        /// Inicializa referencias visuales.
        /// </summary>
        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            ApplyVisualState();
        }

        /// <summary>
        /// Registra callbacks de NetworkVariables y publica el estado inicial sincronizado.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            currentState.OnValueChanged += HandleStateChanged;
            activatingPlayerClientId.OnValueChanged += HandleActivatorChanged;
            networkActivationProgress.OnValueChanged += HandleProgressChanged;
            isSpawnedBySystem.OnValueChanged += HandleSpawnedBySystemChanged;
            networkSealIndex.OnValueChanged += HandleSealIndexChanged;

            if (IsServer)
            {
                currentState.Value = currentStateCache;
                activatingPlayerClientId.Value = activatingPlayerClientIdCache;
                networkActivationProgress.Value = activationProgressCache;
                isSpawnedBySystem.Value = isSpawnedBySystemCache;
                networkSealIndex.Value = sealIndex;
            }

            SyncCacheFromNetworkVariables();
            ApplyVisualState();
        }

        /// <summary>
        /// Cancela callbacks de red al despawnear.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            currentState.OnValueChanged -= HandleStateChanged;
            activatingPlayerClientId.OnValueChanged -= HandleActivatorChanged;
            networkActivationProgress.OnValueChanged -= HandleProgressChanged;
            isSpawnedBySystem.OnValueChanged -= HandleSpawnedBySystemChanged;
            networkSealIndex.OnValueChanged -= HandleSealIndexChanged;
        }

        /// <summary>
        /// Avanza activacion o desactivacion solo en servidor.
        /// </summary>
        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (currentState.Value == SealState.Activating)
            {
                TickTimedState(GetActivationDuration(), CompleteActivationServer);
                return;
            }

            if (currentState.Value == SealState.Deactivating)
            {
                TickTimedState(GetDeactivationDuration(), CompleteDeactivationServer);
            }
        }

        /// <summary>
        /// Configura este sello despues de spawnearlo por sistema.
        /// </summary>
        public void ConfigureFromDefinition(SealDefinition definition, int index, bool spawnedBySystem)
        {
            sealDefinition = definition;
            SetSealIndex(index);
            isSpawnedBySystemCache = spawnedBySystem;

            if (IsServer && IsSpawned)
            {
                isSpawnedBySystem.Value = spawnedBySystem;
            }
        }

        /// <summary>
        /// Actualiza el indice logico del sello aplicando un limite inferior de cero.
        /// </summary>
        public void SetSealIndex(int index)
        {
            sealIndex = Mathf.Max(0, index);

            if (IsServer && IsSpawned)
            {
                networkSealIndex.Value = sealIndex;
            }
        }

        /// <summary>
        /// Solicita al servidor activar el sello.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestActivateServerRpc(ulong playerClientId, RpcParams rpcParams = default)
        {
            ulong senderClientId = ResolveSenderClientId(playerClientId, rpcParams);
            StartActivationServer(senderClientId);
        }

        /// <summary>
        /// Solicita al servidor desactivar o corromper el sello.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestDeactivateServerRpc(ulong playerClientId, RpcParams rpcParams = default)
        {
            ulong senderClientId = ResolveSenderClientId(playerClientId, rpcParams);
            StartDeactivationServer(senderClientId);
        }

        /// <summary>
        /// Cambia el estado desde servidor.
        /// </summary>
        public void SetStateServer(SealState newState)
        {
            if (!IsServer)
            {
                return;
            }

            SetStateInternal(newState);
        }

        /// <summary>
        /// Valida si el jugador puede activar este sello.
        /// </summary>
        public bool CanPlayerActivate(ulong playerClientId)
        {
            ISealInteractor interactor = ResolveInteractor(playerClientId);

            if (interactor == null || !interactor.IsSurvivor || interactor.IsDoll || !interactor.IsAlive)
            {
                return false;
            }

            if (sealDefinition != null && !sealDefinition.CanBeActivatedBySurvivor)
            {
                return false;
            }

            if (currentStateCache == SealState.Active)
            {
                return false;
            }

            if (currentStateCache == SealState.Corrupted && (sealDefinition == null || !sealDefinition.CanBeReactivated))
            {
                return false;
            }

            if (currentStateCache == SealState.Disabled || currentStateCache == SealState.Activating || currentStateCache == SealState.Deactivating)
            {
                return false;
            }

            return IsInteractorInRange(interactor);
        }

        /// <summary>
        /// Valida si el jugador puede desactivar o corromper este sello.
        /// </summary>
        public bool CanDollDeactivate(ulong playerClientId)
        {
            ISealInteractor interactor = ResolveInteractor(playerClientId);

            if (interactor == null || !interactor.IsDoll || !interactor.IsAlive)
            {
                return false;
            }

            if (sealDefinition != null && !sealDefinition.CanBeDeactivatedByDoll)
            {
                return false;
            }

            if (currentStateCache != SealState.Active)
            {
                return false;
            }

            return IsInteractorInRange(interactor);
        }

        /// <summary>
        /// Inicia activacion en servidor.
        /// </summary>
        public void StartActivationServer(ulong playerClientId)
        {
            if (!IsServer || !CanPlayerActivate(playerClientId))
            {
                return;
            }

            SetActivatorInternal(playerClientId);
            SetProgressInternal(0f);

            if (GetActivationDuration() <= 0f)
            {
                CompleteActivationServer();
                return;
            }

            SetStateInternal(SealState.Activating);
        }

        /// <summary>
        /// Cancela activacion en servidor.
        /// </summary>
        public void CancelActivationServer()
        {
            if (!IsServer || currentState.Value != SealState.Activating)
            {
                return;
            }

            SetProgressInternal(0f);
            SetActivatorInternal(ulong.MaxValue);
            SetStateInternal(SealState.Inactive);
        }

        /// <summary>
        /// Completa activacion en servidor.
        /// </summary>
        public void CompleteActivationServer()
        {
            if (!IsServer)
            {
                return;
            }

            SetProgressInternal(1f);
            SetStateInternal(SealState.Active);
            PlaySealStateFxClientRpc(SealState.Active);
        }

        /// <summary>
        /// Inicia desactivacion o corrupcion en servidor.
        /// </summary>
        public void StartDeactivationServer(ulong playerClientId)
        {
            if (!IsServer || !CanDollDeactivate(playerClientId))
            {
                return;
            }

            SetActivatorInternal(playerClientId);
            SetProgressInternal(0f);

            if (GetDeactivationDuration() <= 0f)
            {
                CompleteDeactivationServer();
                return;
            }

            SetStateInternal(SealState.Deactivating);
        }

        /// <summary>
        /// Completa desactivacion o corrupcion en servidor.
        /// </summary>
        public void CompleteDeactivationServer()
        {
            if (!IsServer)
            {
                return;
            }

            SetProgressInternal(1f);
            SealState finalState = sealDefinition != null && sealDefinition.CanBeCorrupted
                ? SealState.Corrupted
                : SealState.Inactive;
            SetStateInternal(finalState);
            PlaySealStateFxClientRpc(finalState);
        }

        /// <summary>
        /// Compatibilidad con el flujo anterior: activa inmediatamente desde servidor o estado local.
        /// </summary>
        public void ActivateSeal(ulong activatorClientId)
        {
            if (IsServer)
            {
                SetActivatorInternal(activatorClientId);
                SetProgressInternal(1f);
                SetStateInternal(SealState.Active);
                return;
            }

            SetLocalState(SealState.Active, activatorClientId, 1f, isSpawnedBySystemCache);
        }

        /// <summary>
        /// Compatibilidad con el flujo anterior.
        /// </summary>
        public void ActivateSeal()
        {
            ActivateSeal(ulong.MaxValue);
        }

        /// <summary>
        /// Restaura el sello al estado inicial.
        /// </summary>
        public void ResetSeal()
        {
            if (IsServer)
            {
                SetActivatorInternal(ulong.MaxValue);
                SetProgressInternal(0f);
                SetStateInternal(SealState.Inactive);
                return;
            }

            SetLocalState(SealState.Inactive, ulong.MaxValue, 0f, isSpawnedBySystemCache);
        }

        /// <summary>
        /// Aplica un estado de snapshot legacy recibido desde SealManager.
        /// </summary>
        public void ApplyNetworkState(bool activated, ulong activatorClientId)
        {
            SealState state = activated ? SealState.Active : SealState.Inactive;
            SetLocalState(state, activatorClientId, activated ? 1f : 0f, isSpawnedBySystemCache);
        }

        /// <summary>
        /// Avanza un estado temporizado hasta completarlo.
        /// </summary>
        private void TickTimedState(float duration, Action onComplete)
        {
            if (duration <= 0f)
            {
                onComplete?.Invoke();
                return;
            }

            float progress = Mathf.Clamp01(networkActivationProgress.Value + (Time.deltaTime / duration));
            SetProgressInternal(progress);

            if (progress >= 1f)
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Cambia estado sincronizado o local segun disponibilidad de Netcode.
        /// </summary>
        private void SetStateInternal(SealState newState)
        {
            if (IsServer && IsSpawned)
            {
                currentState.Value = newState;
            }

            SetLocalState(newState, activatingPlayerClientIdCache, activationProgressCache, isSpawnedBySystemCache);
        }

        /// <summary>
        /// Cambia activador sincronizado o local.
        /// </summary>
        private void SetActivatorInternal(ulong clientId)
        {
            if (IsServer && IsSpawned)
            {
                activatingPlayerClientId.Value = clientId;
            }

            activatingPlayerClientIdCache = clientId;
        }

        /// <summary>
        /// Cambia progreso sincronizado o local.
        /// </summary>
        private void SetProgressInternal(float progress)
        {
            float safeProgress = Mathf.Clamp01(progress);

            if (IsServer && IsSpawned)
            {
                networkActivationProgress.Value = safeProgress;
            }

            activationProgressCache = safeProgress;
        }

        /// <summary>
        /// Aplica estado local y refresca visuales.
        /// </summary>
        private void SetLocalState(SealState state, ulong clientId, float progress, bool spawnedBySystem)
        {
            bool changed = currentStateCache != state;
            currentStateCache = state;
            activatingPlayerClientIdCache = clientId;
            activationProgressCache = Mathf.Clamp01(progress);
            isSpawnedBySystemCache = spawnedBySystem;
            ApplyVisualState();

            if (changed)
            {
                StateChanged?.Invoke(this, state);
            }
        }

        /// <summary>
        /// Sincroniza cache local desde NetworkVariables.
        /// </summary>
        private void SyncCacheFromNetworkVariables()
        {
            sealIndex = networkSealIndex.Value;
            SetLocalState(currentState.Value, activatingPlayerClientId.Value, networkActivationProgress.Value, isSpawnedBySystem.Value);
        }

        private void HandleStateChanged(SealState previousValue, SealState newValue)
        {
            SetLocalState(newValue, activatingPlayerClientId.Value, networkActivationProgress.Value, isSpawnedBySystem.Value);
        }

        private void HandleActivatorChanged(ulong previousValue, ulong newValue)
        {
            activatingPlayerClientIdCache = newValue;
        }

        private void HandleProgressChanged(float previousValue, float newValue)
        {
            activationProgressCache = newValue;
        }

        private void HandleSpawnedBySystemChanged(bool previousValue, bool newValue)
        {
            isSpawnedBySystemCache = newValue;
        }

        private void HandleSealIndexChanged(int previousValue, int newValue)
        {
            sealIndex = Mathf.Max(0, newValue);
        }

        /// <summary>
        /// Reproduce efectos puntuales locales. Por ahora instancia placeholders si existen en la definicion.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PlaySealStateFxClientRpc(SealState state)
        {
            if (sealDefinition == null)
            {
                return;
            }

            GameObject vfxPrefab = state == SealState.Active ? sealDefinition.ActivationVfx : sealDefinition.DeactivationVfx;

            if (vfxPrefab != null)
            {
                Instantiate(vfxPrefab, transform.position, transform.rotation);
            }

            AudioClip clip = state == SealState.Active ? sealDefinition.ActivationSound : sealDefinition.DeactivationSound;

            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position);
            }
        }

        /// <summary>
        /// Obtiene duracion de activacion desde definicion o fallback.
        /// </summary>
        private float GetActivationDuration()
        {
            return sealDefinition != null ? sealDefinition.ActivationDuration : 0f;
        }

        /// <summary>
        /// Obtiene duracion de desactivacion desde definicion o fallback.
        /// </summary>
        private float GetDeactivationDuration()
        {
            return sealDefinition != null ? sealDefinition.DeactivationDuration : 0f;
        }

        /// <summary>
        /// Resuelve el interactor asociado a un client id desde el PlayerObject de Netcode.
        /// </summary>
        private static ISealInteractor ResolveInteractor(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || !networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            {
                return null;
            }

            MonoBehaviour[] components = client.PlayerObject.GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour component in components)
            {
                if (component is ISealInteractor interactor)
                {
                    return interactor;
                }
            }

            return null;
        }

        /// <summary>
        /// Verifica distancia fisica entre el interactor y el sello cuando se puede resolver un componente.
        /// </summary>
        private bool IsInteractorInRange(ISealInteractor interactor)
        {
            if (interactor is not Component component)
            {
                return true;
            }

            float range = interactionRange > 0f ? interactionRange : 3f;
            return Vector3.Distance(component.transform.position, transform.position) <= range;
        }

        /// <summary>
        /// Evita confiar ciegamente en el client id enviado por parametro.
        /// </summary>
        private static ulong ResolveSenderClientId(ulong requestedClientId, RpcParams rpcParams)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            return senderClientId == 0 && requestedClientId != 0 ? requestedClientId : senderClientId;
        }

        /// <summary>
        /// Aplica color de debug segun estado actual.
        /// </summary>
        private void ApplyVisualState()
        {
            cachedRenderer = cachedRenderer != null ? cachedRenderer : GetComponent<Renderer>();

            if (cachedRenderer == null)
            {
                return;
            }

            cachedRenderer.material.color = GetColorForState(currentStateCache);
        }

        /// <summary>
        /// Devuelve color asociado al estado del sello.
        /// </summary>
        private static Color GetColorForState(SealState state)
        {
            switch (state)
            {
                case SealState.Activating:
                    return ActivatingColor;
                case SealState.Active:
                    return ActiveColor;
                case SealState.Deactivating:
                    return DeactivatingColor;
                case SealState.Corrupted:
                    return CorruptedColor;
                case SealState.Disabled:
                    return DisabledColor;
                default:
                    return InactiveColor;
            }
        }
    }
}
