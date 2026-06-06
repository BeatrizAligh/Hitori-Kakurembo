using HitoriKakurembo.Roles;
using UnityEngine;

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Conserva el rol y equipo efectivo del jugador para que otros sistemas consuman una unica fuente de verdad.
    /// </summary>
    public class PlayerRoleHandler : MonoBehaviour
    {
        /// <summary>
        /// Referencia opcional al asset de rol asignado al jugador.
        /// </summary>
        [SerializeField] private RoleBase currentRoleData;

        /// <summary>
        /// Tipo de rol efectivo asignado al jugador.
        /// </summary>
        [SerializeField] private PlayerRoleType currentRole = PlayerRoleType.Survivor;

        /// <summary>
        /// Equipo efectivo al que pertenece el jugador.
        /// </summary>
        [SerializeField] private PlayerTeam currentTeam = PlayerTeam.Survivors;

        /// <summary>
        /// Obtiene el asset de rol actualmente asociado al jugador.
        /// </summary>
        public RoleBase CurrentRoleData => currentRoleData;

        /// <summary>
        /// Obtiene el tipo de rol efectivo actual.
        /// </summary>
        public PlayerRoleType CurrentRole => currentRole;

        /// <summary>
        /// Obtiene el equipo efectivo actual.
        /// </summary>
        public PlayerTeam CurrentTeam => currentTeam;

        /// <summary>
        /// Asigna un rol usando un asset de <see cref="RoleBase"/>.
        /// </summary>
        /// <param name="roleData">
        /// Asset de rol que debe aplicarse al jugador.
        /// </param>
        public void AssignRole(RoleBase roleData)
        {
            currentRoleData = roleData;

            if (roleData == null)
            {
                currentRole = PlayerRoleType.Survivor;
                currentTeam = PlayerTeam.Survivors;
                return;
            }

            currentRole = roleData.RoleType;
            currentTeam = roleData.Team;
        }

        /// <summary>
        /// Asigna un rol directo sin depender de un asset de rol.
        /// </summary>
        /// <param name="roleType">
        /// Tipo de rol que debe quedar aplicado.
        /// </param>
        /// <param name="isDoll">
        /// Indica si el jugador debe pertenecer al equipo del muneco.
        /// </param>
        public void AssignRole(PlayerRoleType roleType, bool isDoll)
        {
            currentRoleData = null;
            currentRole = roleType;
            currentTeam = isDoll ? PlayerTeam.Doll : PlayerTeam.Survivors;
        }

        /// <summary>
        /// Configura el estado del jugador como miembro del equipo del muneco.
        /// </summary>
        public void SetAsDoll()
        {
            currentRoleData = null;
            currentRole = PlayerRoleType.None;
            currentTeam = PlayerTeam.Doll;
        }
    }
}
