using HitoriKakurembo.Multiplayer.Data;
using TestMultiplayer.Networking;
using Unity.Netcode;

namespace HitoriKakurembo.Multiplayer.Networking
{
    /// <summary>
    /// Identidad multiplayer persistente de un jugador de Hitori Kakurembo.
    /// Extiende el PlayerBrain base con datos propios del juego: equipo, rol, vida y estado de muneco.
    /// </summary>
    public class HitoriPlayerBrain : TestMultiplayerPlayerBrain
    {
        private readonly NetworkVariable<HitoriPlayerTeam> currentTeam = new NetworkVariable<HitoriPlayerTeam>(
            HitoriPlayerTeam.Survivor,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<HitoriPlayerRole> currentRole = new NetworkVariable<HitoriPlayerRole>(
            HitoriPlayerRole.Survivor,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<HitoriLifeState> lifeState = new NetworkVariable<HitoriLifeState>(
            HitoriLifeState.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isDoll = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public HitoriPlayerTeam CurrentTeam => currentTeam.Value;
        public HitoriPlayerRole CurrentRole => currentRole.Value;
        public HitoriLifeState LifeState => lifeState.Value;
        public bool IsDoll => isDoll.Value;
        public bool IsAlive => lifeState.Value == HitoriLifeState.Alive;
        public bool IsSurvivor => currentTeam.Value == HitoriPlayerTeam.Survivor && !isDoll.Value;

        /// <summary>
        /// Inicializa los datos base de TestMultiplayer y deja el jugador en estado superviviente por defecto.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                SetTeamOnServer(HitoriPlayerTeam.Survivor);
                SetRoleOnServer(HitoriPlayerRole.Survivor);
                SetLifeStateOnServer(HitoriLifeState.Alive);
                SetDollStateOnServer(false);
            }
        }

        /// <summary>
        /// Cambia el equipo desde el servidor. Futuras rondas lo usaran para alternar superviviente, muneco y espectador.
        /// </summary>
        public void SetTeamOnServer(HitoriPlayerTeam team)
        {
            if (!IsServer)
            {
                return;
            }

            currentTeam.Value = team;
        }

        /// <summary>
        /// Cambia el rol de superviviente desde el servidor.
        /// </summary>
        public void SetRoleOnServer(HitoriPlayerRole role)
        {
            if (!IsServer)
            {
                return;
            }

            currentRole.Value = role;
        }

        /// <summary>
        /// Cambia el estado de vida desde el servidor.
        /// </summary>
        public void SetLifeStateOnServer(HitoriLifeState state)
        {
            if (!IsServer)
            {
                return;
            }

            lifeState.Value = state;
        }

        /// <summary>
        /// Marca si este jugador actua como muneco en la ronda actual.
        /// </summary>
        public void SetDollStateOnServer(bool value)
        {
            if (!IsServer)
            {
                return;
            }

            isDoll.Value = value;
            currentTeam.Value = value ? HitoriPlayerTeam.Doll : HitoriPlayerTeam.Survivor;
        }
    }
}
