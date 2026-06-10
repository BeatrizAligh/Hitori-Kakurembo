using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Enumera los tipos de rol disponibles para jugadores del equipo superviviente.
    /// </summary>
    public enum PlayerRoleType
    {
        /// <summary>
        /// Indica que el jugador no tiene un rol de superviviente asignado.
        /// </summary>
        None = 0,

        /// <summary>
        /// Rol orientado a percepcion o lectura espiritual.
        /// </summary>
        Medium = 1,

        /// <summary>
        /// Rol orientado a contencion o limpieza ritual.
        /// </summary>
        Exorcist = 2,

        /// <summary>
        /// Rol orientado a captura de evidencia visual.
        /// </summary>
        Photographer = 3,

        /// <summary>
        /// Rol orientado a investigacion de pistas y entorno.
        /// </summary>
        Investigator = 4,

        /// <summary>
        /// Rol orientado a soporte defensivo o bendiciones.
        /// </summary>
        Monk = 5,

        /// <summary>
        /// Rol generico de superviviente cuando no existe una especializacion adicional.
        /// </summary>
        Survivor = 6,

        /// <summary>
        /// Rol provisional asociado al celular y a recursos de soporte.
        /// </summary>
        Oxygenated = 7
    }

    /// <summary>
    /// Enumera los equipos logicos utilizados por la partida.
    /// </summary>
    public enum PlayerTeam
    {
        /// <summary>
        /// Indica que el jugador todavia no pertenece a un equipo definido.
        /// </summary>
        None = 0,

        /// <summary>
        /// Equipo formado por los jugadores que intentan sobrevivir al ritual.
        /// </summary>
        Survivors = 1,

        /// <summary>
        /// Equipo formado por el jugador que actua como muneco.
        /// </summary>
        Doll = 2
    }

    /// <summary>
    /// Define la base comun de los datos de rol almacenados como ScriptableObject.
    /// </summary>
    public abstract class RoleBase : ScriptableObject
    {
        /// <summary>
        /// Nombre visible que puede mostrarse en interfaces, debug o flujos de seleccion.
        /// </summary>
        [SerializeField] private string displayName = "Role";

        /// <summary>
        /// Obtiene el tipo de rol representado por el asset.
        /// </summary>
        public abstract PlayerRoleType RoleType { get; }

        /// <summary>
        /// Obtiene el equipo logico al que pertenece el rol.
        /// </summary>
        public virtual PlayerTeam Team => PlayerTeam.Survivors;

        /// <summary>
        /// Obtiene el nombre visible final del rol, usando el tipo como respaldo cuando no se define un nombre personalizado.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RoleType.ToString() : displayName;
    }
}
