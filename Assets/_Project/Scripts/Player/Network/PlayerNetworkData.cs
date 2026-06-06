using System;
using HitoriKakurembo.PlayerSystem.Data;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.PlayerSystem.Network
{
    /// <summary>
    /// Almacena los datos basicos sincronizados del jugador dentro de una sesion Netcode for GameObjects.
    /// Este componente no decide rondas, roles ni equipos; solo expone un estado de red que otros sistemas pueden asignar desde servidor.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerNetworkData : NetworkBehaviour
    {
        /// <summary>
        /// Identificador sincronizado del cliente propietario de este jugador.
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
        /// Indice sincronizado del jugador dentro de una lista externa, como lobby o scoreboard.
        /// </summary>
        private readonly NetworkVariable<int> playerIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Estado sincronizado que indica si el jugador esta listo para un flujo externo de partida.
        /// </summary>
        private readonly NetworkVariable<bool> isReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Estado sincronizado que indica si el jugador debe considerarse vivo a nivel general.
        /// </summary>
        private readonly NetworkVariable<bool> isAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Bandera sincronizada que permite a sistemas externos marcar si este jugador actua como muneco.
        /// </summary>
        private readonly NetworkVariable<bool> isDoll = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Rol sincronizado actual del jugador.
        /// </summary>
        private readonly NetworkVariable<PlayerRoleType> currentRole = new NetworkVariable<PlayerRoleType>(
            PlayerRoleType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Equipo sincronizado actual del jugador.
        /// </summary>
        private readonly NetworkVariable<PlayerTeam> currentTeam = new NetworkVariable<PlayerTeam>(
            PlayerTeam.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// Evento emitido localmente cuando cambia el estado ready sincronizado.
        /// </summary>
        public event Action<bool> ReadyStateChanged;

        /// <summary>
        /// Evento emitido localmente cuando cambia el estado vivo sincronizado.
        /// </summary>
        public event Action<bool> AliveStateChanged;

        /// <summary>
        /// Evento emitido localmente cuando cambia la bandera de muneco.
        /// </summary>
        public event Action<bool> DollStateChanged;

        /// <summary>
        /// Evento emitido localmente cuando cambia el rol sincronizado.
        /// </summary>
        public event Action<PlayerRoleType> RoleChanged;

        /// <summary>
        /// Evento emitido localmente cuando cambia el equipo sincronizado.
        /// </summary>
        public event Action<PlayerTeam> TeamChanged;

        /// <summary>
        /// Evento emitido localmente cuando cambia el indice sincronizado del jugador.
        /// </summary>
        public event Action<int> PlayerIndexChanged;

        /// <summary>
        /// Evento emitido localmente cuando cambia el nombre visible sincronizado.
        /// </summary>
        public event Action<string> PlayerNameChanged;

        /// <summary>
        /// Obtiene el identificador sincronizado del jugador.
        /// </summary>
        public ulong PlayerId => playerId.Value;

        /// <summary>
        /// Obtiene el nombre visible sincronizado del jugador.
        /// </summary>
        public string PlayerName => playerName.Value.ToString();

        /// <summary>
        /// Obtiene el indice sincronizado del jugador.
        /// </summary>
        public int PlayerIndex => playerIndex.Value;

        /// <summary>
        /// Obtiene si el jugador esta marcado como listo.
        /// </summary>
        public bool IsReady => isReady.Value;

        /// <summary>
        /// Obtiene si el jugador esta marcado como vivo.
        /// </summary>
        public bool IsAlive => isAlive.Value;

        /// <summary>
        /// Obtiene si el jugador esta marcado como muneco.
        /// </summary>
        public bool IsDoll => isDoll.Value;

        /// <summary>
        /// Obtiene el rol sincronizado actual.
        /// </summary>
        public PlayerRoleType CurrentRole => currentRole.Value;

        /// <summary>
        /// Obtiene el equipo sincronizado actual.
        /// </summary>
        public PlayerTeam CurrentTeam => currentTeam.Value;

        /// <summary>
        /// Inicializa valores de servidor y registra callbacks locales de NetworkVariables.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                playerId.Value = OwnerClientId;

                if (playerName.Value.IsEmpty)
                {
                    SetPlayerName($"Jugador {OwnerClientId}");
                }
            }

            SubscribeToNetworkVariableChanges();
            PublishCurrentValues();
        }

        /// <summary>
        /// Limpia callbacks cuando el objeto sale de la sesion de red.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkVariableChanges();
        }

        /// <summary>
        /// Solicita al servidor cambiar el estado ready del jugador propietario.
        /// </summary>
        /// <param name="ready">
        /// Nuevo estado ready solicitado por el owner.
        /// </param>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SetReadyStateServerRpc(bool ready)
        {
            isReady.Value = ready;
        }

        /// <summary>
        /// Actualiza en servidor si el jugador esta vivo.
        /// </summary>
        /// <param name="alive">
        /// Nuevo estado de vida basico.
        /// </param>
        public void SetAliveState(bool alive)
        {
            if (!IsServer)
            {
                return;
            }

            isAlive.Value = alive;
        }

        /// <summary>
        /// Actualiza en servidor si el jugador actua como muneco.
        /// </summary>
        /// <param name="doll">
        /// Nuevo estado de muneco.
        /// </param>
        public void SetDollState(bool doll)
        {
            if (!IsServer)
            {
                return;
            }

            isDoll.Value = doll;
        }

        /// <summary>
        /// Actualiza en servidor el rol sincronizado del jugador.
        /// </summary>
        /// <param name="role">
        /// Rol que debe almacenarse.
        /// </param>
        public void SetRole(PlayerRoleType role)
        {
            if (!IsServer)
            {
                return;
            }

            currentRole.Value = role;
        }

        /// <summary>
        /// Actualiza en servidor el equipo sincronizado del jugador.
        /// </summary>
        /// <param name="team">
        /// Equipo que debe almacenarse.
        /// </param>
        public void SetTeam(PlayerTeam team)
        {
            if (!IsServer)
            {
                return;
            }

            currentTeam.Value = team;
        }

        /// <summary>
        /// Actualiza en servidor el indice sincronizado del jugador.
        /// </summary>
        /// <param name="index">
        /// Indice cero-basado asignado por un sistema externo.
        /// </param>
        public void SetPlayerIndex(int index)
        {
            if (!IsServer)
            {
                return;
            }

            playerIndex.Value = index;
        }

        /// <summary>
        /// Actualiza en servidor el nombre visible sincronizado del jugador.
        /// </summary>
        /// <param name="newName">
        /// Nombre solicitado. Si llega vacio, se usa un nombre por defecto basado en owner id.
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
        /// Conecta callbacks de cambio para publicar eventos C# locales.
        /// </summary>
        private void SubscribeToNetworkVariableChanges()
        {
            isReady.OnValueChanged += HandleReadyStateChanged;
            isAlive.OnValueChanged += HandleAliveStateChanged;
            isDoll.OnValueChanged += HandleDollStateChanged;
            currentRole.OnValueChanged += HandleRoleChanged;
            currentTeam.OnValueChanged += HandleTeamChanged;
            playerIndex.OnValueChanged += HandlePlayerIndexChanged;
            playerName.OnValueChanged += HandlePlayerNameChanged;
        }

        /// <summary>
        /// Desconecta callbacks de cambio para evitar referencias invalidas al despawnear.
        /// </summary>
        private void UnsubscribeFromNetworkVariableChanges()
        {
            isReady.OnValueChanged -= HandleReadyStateChanged;
            isAlive.OnValueChanged -= HandleAliveStateChanged;
            isDoll.OnValueChanged -= HandleDollStateChanged;
            currentRole.OnValueChanged -= HandleRoleChanged;
            currentTeam.OnValueChanged -= HandleTeamChanged;
            playerIndex.OnValueChanged -= HandlePlayerIndexChanged;
            playerName.OnValueChanged -= HandlePlayerNameChanged;
        }

        /// <summary>
        /// Publica los valores actuales cuando el objeto aparece en red para inicializar listeners tardios.
        /// </summary>
        private void PublishCurrentValues()
        {
            ReadyStateChanged?.Invoke(IsReady);
            AliveStateChanged?.Invoke(IsAlive);
            DollStateChanged?.Invoke(IsDoll);
            RoleChanged?.Invoke(CurrentRole);
            TeamChanged?.Invoke(CurrentTeam);
            PlayerIndexChanged?.Invoke(PlayerIndex);
            PlayerNameChanged?.Invoke(PlayerName);
        }

        /// <summary>
        /// Notifica localmente un cambio del estado ready.
        /// </summary>
        private void HandleReadyStateChanged(bool previousValue, bool newValue)
        {
            ReadyStateChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Notifica localmente un cambio del estado vivo.
        /// </summary>
        private void HandleAliveStateChanged(bool previousValue, bool newValue)
        {
            AliveStateChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Notifica localmente un cambio del estado de muneco.
        /// </summary>
        private void HandleDollStateChanged(bool previousValue, bool newValue)
        {
            DollStateChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Notifica localmente un cambio de rol.
        /// </summary>
        private void HandleRoleChanged(PlayerRoleType previousValue, PlayerRoleType newValue)
        {
            RoleChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Notifica localmente un cambio de equipo.
        /// </summary>
        private void HandleTeamChanged(PlayerTeam previousValue, PlayerTeam newValue)
        {
            TeamChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Notifica localmente un cambio de indice.
        /// </summary>
        private void HandlePlayerIndexChanged(int previousValue, int newValue)
        {
            PlayerIndexChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Notifica localmente un cambio de nombre visible.
        /// </summary>
        private void HandlePlayerNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            PlayerNameChanged?.Invoke(newValue.ToString());
        }
    }
}
