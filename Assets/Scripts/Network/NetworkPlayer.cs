using HitoriKakurembo.Core;
using HitoriKakurembo.Player;
using HitoriKakurembo.Roles;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HitoriKakurembo.Network
{
    /// <summary>
    /// Almacena el estado sincronizado de identidad, rol y puntuacion para un jugador de red individual.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        /// <summary>
        /// Referencia cacheada al manejador local de rol para mantener sincronizado el estado visual y logico del jugador.
        /// </summary>
        private PlayerRoleHandler roleHandler;

        /// <summary>
        /// Referencia cacheada al controlador de movimiento para teleports seguros de spawn y cambio de ronda.
        /// </summary>
        private PlayerController playerController;

        /// <summary>
        /// Referencia cacheada al controlador visual que instancia el modelo artistico seleccionado.
        /// </summary>
        private PlayerVisualModelController visualModelController;

        /// <summary>
        /// Referencia cacheada al transform de red para publicar teleports cuando el owner aplica una correccion fuerte.
        /// </summary>
        private NetworkTransform networkTransform;

        /// <summary>
        /// Identificador sincronizado del cliente propietario de este objeto de jugador.
        /// </summary>
        private readonly NetworkVariable<ulong> playerId = new NetworkVariable<ulong>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Nombre visible sincronizado del jugador.
        /// </summary>
        private readonly NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Indice sincronizado dentro del lobby, asignado por el servidor para ordenar jugadores de forma estable en UI y rondas.
        /// </summary>
        private readonly NetworkVariable<int> playerIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Estado sincronizado que indica si el jugador ya confirmo que esta listo para iniciar la partida.
        /// </summary>
        private readonly NetworkVariable<bool> isReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Bandera sincronizada que indica si el jugador actual actua como muneco.
        /// </summary>
        private readonly NetworkVariable<bool> isDoll = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Estado sincronizado que indica si el jugador sigue vivo durante la ronda actual.
        /// </summary>
        private readonly NetworkVariable<bool> isAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Rol sincronizado del jugador cuando pertenece al equipo superviviente.
        /// </summary>
        private readonly NetworkVariable<PlayerRoleType> currentRole = new NetworkVariable<PlayerRoleType>(
            PlayerRoleType.Survivor,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Indice sincronizado del modelo visual elegido por el jugador en el lobby.
        /// El servidor lo valida y todos los clientes lo usan para instanciar el mismo personaje.
        /// </summary>
        private readonly NetworkVariable<int> selectedCharacterModelIndex = new NetworkVariable<int>(
            PlayerCharacterModelCatalog.DefaultModelIndex,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Puntuacion sincronizada acumulada del jugador.
        /// </summary>
        private readonly NetworkVariable<int> currentScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Puntuacion sincronizada obtenida unicamente durante la ronda actual.
        /// </summary>
        private readonly NetworkVariable<int> currentRoundScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Obtiene el <see cref="NetworkObject"/> asociado al jugador.
        /// </summary>
        public NetworkObject CachedNetworkObject => NetworkObject;

        /// <summary>
        /// Obtiene el identificador sincronizado del jugador.
        /// </summary>
        public ulong PlayerId => playerId.Value;

        /// <summary>
        /// Obtiene el nombre visible sincronizado del jugador.
        /// </summary>
        public string PlayerName => playerName.Value.ToString();

        /// <summary>
        /// Obtiene el indice sincronizado del jugador dentro del lobby.
        /// </summary>
        public int PlayerIndex => playerIndex.Value;

        /// <summary>
        /// Obtiene si el jugador ya marco que esta listo en el lobby.
        /// </summary>
        public bool IsReady => isReady.Value;

        /// <summary>
        /// Obtiene un valor que indica si el jugador es actualmente el muneco.
        /// </summary>
        public bool IsDoll => isDoll.Value;

        /// <summary>
        /// Obtiene si el jugador sigue vivo dentro de la ronda actual.
        /// </summary>
        public bool IsAlive => isAlive.Value;

        /// <summary>
        /// Obtiene el rol sincronizado del jugador.
        /// </summary>
        public PlayerRoleType CurrentRole => currentRole.Value;

        /// <summary>
        /// Obtiene el indice del modelo visual elegido por el jugador en lobby.
        /// Si el jugador es muneco, el controlador visual puede forzar el modelo de muneco sin cambiar este valor elegido.
        /// </summary>
        public int SelectedCharacterModelIndex => selectedCharacterModelIndex.Value;

        /// <summary>
        /// Obtiene la puntuacion sincronizada actual del jugador.
        /// </summary>
        public int CurrentScore => currentScore.Value;

        /// <summary>
        /// Obtiene la puntuacion sincronizada obtenida durante la ronda actual.
        /// </summary>
        public int CurrentRoundScore => currentRoundScore.Value;

        /// <summary>
        /// Inicializa valores sincronizados en el servidor y registra el jugador en <see cref="NetworkGameManager"/>.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            roleHandler = GetComponent<PlayerRoleHandler>();
            playerController = GetComponent<PlayerController>();
            visualModelController = GetComponent<PlayerVisualModelController>();
            networkTransform = GetComponent<NetworkTransform>();
            NetworkGameManager networkGameManager = FindAnyObjectByType<NetworkGameManager>();
            networkGameManager?.RegisterPlayer(this);

            if (IsServer)
            {
                playerId.Value = OwnerClientId;
                RelaySessionManager relaySessionManager = ServiceLocator.Resolve<RelaySessionManager>() ?? FindAnyObjectByType<RelaySessionManager>();
                SetPlayerName(relaySessionManager?.GetApprovedPlayerName(OwnerClientId));
                SetReadyState(relaySessionManager?.GetLobbyReadyStateForClient(OwnerClientId, false) ?? false);
                SetAliveState(true);
                SetCharacterModelIndex(selectedCharacterModelIndex.Value);
                ResetCurrentRoundScore();
                networkGameManager?.ReindexConnectedPlayers();
            }

            isReady.OnValueChanged += HandleReadyStateChanged;
            isDoll.OnValueChanged += HandleDollStateChanged;
            isAlive.OnValueChanged += HandleAliveStateChanged;
            currentRole.OnValueChanged += HandleRoleChanged;
            selectedCharacterModelIndex.OnValueChanged += HandleCharacterModelIndexChanged;
            currentRoundScore.OnValueChanged += HandleRoundScoreChanged;
            ApplyRoleStateToComponents();
            ApplyVisualStateToComponents();
        }

        /// <summary>
        /// Elimina el jugador del registro del manager de red cuando el objeto sale de la sesion.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            isReady.OnValueChanged -= HandleReadyStateChanged;
            isDoll.OnValueChanged -= HandleDollStateChanged;
            isAlive.OnValueChanged -= HandleAliveStateChanged;
            currentRole.OnValueChanged -= HandleRoleChanged;
            selectedCharacterModelIndex.OnValueChanged -= HandleCharacterModelIndexChanged;
            currentRoundScore.OnValueChanged -= HandleRoundScoreChanged;
            FindAnyObjectByType<NetworkGameManager>()?.UnregisterPlayer(this);
        }

        /// <summary>
        /// Actualiza el nombre visible del jugador en el servidor.
        /// </summary>
        /// <param name="newName">
        /// Nombre solicitado. Si llega vacio, se utiliza un nombre por defecto basado en el owner client id.
        /// </param>
        public void SetPlayerName(string newName)
        {
            if (!IsServer)
            {
                return;
            }

            string finalName = string.IsNullOrWhiteSpace(newName) ? $"Jugador {OwnerClientId}" : newName.Trim();
            playerName.Value = new FixedString64Bytes(finalName);
        }

        /// <summary>
        /// Actualiza en el servidor el indice de lobby asignado a este jugador.
        /// </summary>
        /// <param name="value">
        /// Indice cero-basado que representa el orden actual dentro de la sala.
        /// </param>
        public void SetPlayerIndex(int value)
        {
            if (!IsServer)
            {
                return;
            }

            playerIndex.Value = Mathf.Max(0, value);
        }

        /// <summary>
        /// Solicita cambiar el estado ready del jugador local; el servidor valida y aplica el cambio real.
        /// </summary>
        /// <param name="value">
        /// Nuevo estado de confirmacion solicitado por el jugador propietario.
        /// </param>
        public void SubmitReadyState(bool value)
        {
            if (IsServer)
            {
                TryApplyReadyStateOnServer(value);
                return;
            }

            SubmitReadyStateServerRpc(value);
        }

        /// <summary>
        /// Actualiza en el servidor si este jugador ya esta listo para iniciar la partida.
        /// </summary>
        /// <param name="value">
        /// Nuevo estado ready validado por la autoridad.
        /// </param>
        public void SetReadyState(bool value)
        {
            if (!IsServer)
            {
                return;
            }

            isReady.Value = value;
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Actualiza en el servidor si este jugador debe considerarse el muneco de la ronda.
        /// </summary>
        /// <param name="value">
        /// Nuevo estado del flag de muneco.
        /// </param>
        public void SetDollState(bool value)
        {
            if (!IsServer)
            {
                return;
            }

            isDoll.Value = value;
        }

        /// <summary>
        /// Actualiza en el servidor si el jugador sigue vivo durante la ronda actual.
        /// </summary>
        /// <param name="value">
        /// Nuevo estado de vida que debe sincronizarse con todos los clientes.
        /// </param>
        public void SetAliveState(bool value)
        {
            if (!IsServer)
            {
                return;
            }

            isAlive.Value = value;
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Actualiza en el servidor el rol asignado al jugador.
        /// </summary>
        /// <param name="roleType">
        /// Rol que debe almacenarse en el estado sincronizado.
        /// </param>
        public void SetRole(PlayerRoleType roleType)
        {
            if (!IsServer)
            {
                return;
            }

            currentRole.Value = roleType;
        }

        /// <summary>
        /// Solicita desde el propietario local cambiar el modelo visual elegido en el lobby.
        /// El cliente solo pide el cambio; el servidor valida que el lobby siga abierto y replica el indice final.
        /// </summary>
        /// <param name="modelIndex">
        /// Indice solicitado dentro de <see cref="PlayerCharacterModelCatalog"/>.
        /// </param>
        public void SubmitCharacterModelIndex(int modelIndex)
        {
            int safeModelIndex = PlayerCharacterModelCatalog.NormalizeIndex(modelIndex);

            if (IsServer)
            {
                TryApplyCharacterModelIndexOnServer(safeModelIndex);
                return;
            }

            SubmitCharacterModelIndexServerRpc(safeModelIndex);
        }

        /// <summary>
        /// Actualiza en el servidor el modelo visual elegido por el jugador.
        /// No asigna el modelo de muneco; ese cambio visual depende de <see cref="IsDoll"/>.
        /// </summary>
        /// <param name="modelIndex">
        /// Indice validado del modelo elegido.
        /// </param>
        public void SetCharacterModelIndex(int modelIndex)
        {
            if (!IsServer)
            {
                return;
            }

            selectedCharacterModelIndex.Value = PlayerCharacterModelCatalog.NormalizeIndex(modelIndex);
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Agrega puntos a la puntuacion del jugador en el servidor.
        /// </summary>
        /// <param name="amount">
        /// Cantidad de puntos a agregar. Valores negativos se recortan a cero.
        /// </param>
        public void AddScore(int amount)
        {
            if (!IsServer)
            {
                return;
            }

            currentScore.Value += Mathf.Max(0, amount);
        }

        /// <summary>
        /// Agrega puntos a la puntuacion de la ronda actual en el servidor.
        /// </summary>
        /// <param name="amount">
        /// Cantidad de puntos de ronda a agregar. Valores negativos se recortan a cero.
        /// </param>
        public void AddRoundScore(int amount)
        {
            if (!IsServer)
            {
                return;
            }

            currentRoundScore.Value += Mathf.Max(0, amount);
        }

        /// <summary>
        /// Reinicia en el servidor la puntuacion sincronizada del jugador.
        /// </summary>
        public void ResetScore()
        {
            if (!IsServer)
            {
                return;
            }

            currentScore.Value = 0;
        }

        /// <summary>
        /// Reinicia en el servidor la puntuacion acumulada solo para la ronda actual.
        /// </summary>
        public void ResetCurrentRoundScore()
        {
            if (!IsServer)
            {
                return;
            }

            currentRoundScore.Value = 0;
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Solicita desde servidor que el jugador quede en una posicion inicial de partida validada.
        /// </summary>
        /// <param name="position">
        /// Posicion mundial calculada por el servidor para este jugador.
        /// </param>
        /// <param name="rotation">
        /// Rotacion mundial calculada por el servidor para este jugador.
        /// </param>
        public void TeleportToGameSpawnOnServer(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
            {
                return;
            }

            ApplyTeleportLocally(position, rotation);
            ApplyGameSpawnTeleportOwnerRpc(position, rotation);
        }

        /// <summary>
        /// RPC enviado por el cliente propietario para pedir al servidor cambiar su estado ready.
        /// </summary>
        /// <param name="value">
        /// Estado ready solicitado por el cliente.
        /// </param>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitReadyStateServerRpc(bool value)
        {
            TryApplyReadyStateOnServer(value);
        }

        /// <summary>
        /// RPC enviado por el propietario para pedir al servidor un cambio de modelo visual en lobby.
        /// </summary>
        /// <param name="modelIndex">
        /// Indice solicitado por el cliente propietario.
        /// </param>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitCharacterModelIndexServerRpc(int modelIndex)
        {
            TryApplyCharacterModelIndexOnServer(modelIndex);
        }

        /// <summary>
        /// RPC enviado al owner para corregir su posicion local aunque el transform sea owner-authoritative.
        /// </summary>
        /// <param name="position">
        /// Posicion mundial que el servidor asigno al jugador.
        /// </param>
        /// <param name="rotation">
        /// Rotacion mundial que el servidor asigno al jugador.
        /// </param>
        [Rpc(SendTo.Owner)]
        private void ApplyGameSpawnTeleportOwnerRpc(Vector3 position, Quaternion rotation)
        {
            ApplyTeleportLocally(position, rotation);

            networkTransform ??= GetComponent<NetworkTransform>();

            if (networkTransform != null && networkTransform.IsSpawned && networkTransform.IsOwner)
            {
                networkTransform.SetState(position, rotation, transform.localScale, false);
            }
        }

        /// <summary>
        /// Valida en servidor si el lobby acepta cambios ready y aplica el estado solicitado.
        /// </summary>
        /// <param name="value">
        /// Estado ready que se desea aplicar.
        /// </param>
        private void TryApplyReadyStateOnServer(bool value)
        {
            RelaySessionManager relaySessionManager = ServiceLocator.Resolve<RelaySessionManager>() ?? FindAnyObjectByType<RelaySessionManager>();

            if (relaySessionManager != null && !relaySessionManager.CanChangeReadyStateOnServer())
            {
                return;
            }

            if (relaySessionManager != null)
            {
                relaySessionManager.SetLobbyReadyStateOnServer(OwnerClientId, value);
                return;
            }

            SetReadyState(value);
        }

        /// <summary>
        /// Valida que la seleccion de modelo ocurra dentro del lobby abierto y aplica el valor en servidor.
        /// </summary>
        /// <param name="modelIndex">
        /// Indice solicitado por el jugador propietario.
        /// </param>
        private void TryApplyCharacterModelIndexOnServer(int modelIndex)
        {
            RelaySessionManager relaySessionManager = ServiceLocator.Resolve<RelaySessionManager>() ?? FindAnyObjectByType<RelaySessionManager>();

            if (relaySessionManager != null && !relaySessionManager.CanChangeReadyStateOnServer())
            {
                return;
            }

            SetCharacterModelIndex(modelIndex);
        }

        /// <summary>
        /// Notifica al manager de sesion que un dato visible del lobby cambio y debe republicarse.
        /// </summary>
        private void NotifyLobbyStateChangedOnServer()
        {
            if (!IsServer)
            {
                return;
            }

            RelaySessionManager relaySessionManager = ServiceLocator.Resolve<RelaySessionManager>() ?? FindAnyObjectByType<RelaySessionManager>();
            relaySessionManager?.NotifyLobbyPlayerStateChanged();
        }

        /// <summary>
        /// Aplica un teleport local evitando que CharacterController conserve velocidad de caida previa.
        /// </summary>
        /// <param name="position">
        /// Posicion mundial final.
        /// </param>
        /// <param name="rotation">
        /// Rotacion mundial final.
        /// </param>
        private void ApplyTeleportLocally(Vector3 position, Quaternion rotation)
        {
            playerController ??= GetComponent<PlayerController>();

            if (playerController != null)
            {
                playerController.TeleportTo(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// Reacciona al cambio sincronizado de ready para que la UI local pueda refrescarse sin esperar otro evento externo.
        /// </summary>
        /// <param name="previousValue">
        /// Estado ready anterior.
        /// </param>
        /// <param name="newValue">
        /// Estado ready nuevo.
        /// </param>
        private void HandleReadyStateChanged(bool previousValue, bool newValue)
        {
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Reaplica el equipo y rol localmente cuando cambia la bandera sincronizada que identifica al muneco.
        /// </summary>
        /// <param name="previousValue">
        /// Valor anterior de la bandera sincronizada.
        /// </param>
        /// <param name="newValue">
        /// Nuevo valor de la bandera sincronizada.
        /// </param>
        private void HandleDollStateChanged(bool previousValue, bool newValue)
        {
            ApplyRoleStateToComponents();
            ApplyVisualStateToComponents();
        }

        /// <summary>
        /// Reacciona al cambio sincronizado de vida para mantener actualizadas las vistas de lobby y partida.
        /// </summary>
        /// <param name="previousValue">
        /// Estado de vida anterior.
        /// </param>
        /// <param name="newValue">
        /// Estado de vida nuevo.
        /// </param>
        private void HandleAliveStateChanged(bool previousValue, bool newValue)
        {
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Reaplica el estado de rol local cuando el servidor actualiza el rol sincronizado del jugador.
        /// </summary>
        /// <param name="previousValue">
        /// Rol anterior del jugador.
        /// </param>
        /// <param name="newValue">
        /// Rol nuevo asignado por el servidor.
        /// </param>
        private void HandleRoleChanged(PlayerRoleType previousValue, PlayerRoleType newValue)
        {
            ApplyRoleStateToComponents();
        }

        /// <summary>
        /// Reaplica el modelo visual cuando el jugador cambia su seleccion desde el lobby.
        /// </summary>
        /// <param name="previousValue">
        /// Indice visual anterior.
        /// </param>
        /// <param name="newValue">
        /// Nuevo indice visual sincronizado.
        /// </param>
        private void HandleCharacterModelIndexChanged(int previousValue, int newValue)
        {
            ApplyVisualStateToComponents();
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Reacciona al cambio sincronizado de puntos de ronda para mantener snapshots de lobby y resultados actualizados.
        /// </summary>
        /// <param name="previousValue">
        /// Puntos de ronda anteriores.
        /// </param>
        /// <param name="newValue">
        /// Nuevos puntos de ronda.
        /// </param>
        private void HandleRoundScoreChanged(int previousValue, int newValue)
        {
            NotifyLobbyStateChangedOnServer();
        }

        /// <summary>
        /// Sincroniza el <see cref="PlayerRoleHandler"/> local con el estado de red actual del jugador.
        /// </summary>
        private void ApplyRoleStateToComponents()
        {
            if (roleHandler == null)
            {
                return;
            }

            if (IsDoll)
            {
                roleHandler.SetAsDoll();
                return;
            }

            roleHandler.AssignRole(CurrentRole, false);
        }

        /// <summary>
        /// Sincroniza el controlador visual local con la seleccion de personaje y el estado actual de muneco.
        /// </summary>
        private void ApplyVisualStateToComponents()
        {
            visualModelController = visualModelController != null ? visualModelController : GetComponent<PlayerVisualModelController>();
            visualModelController?.ApplyVisualModel(SelectedCharacterModelIndex, IsDoll);
        }
    }
}
