namespace HitoriKakurembo.PlayerSystem.Data
{
    /// <summary>
    /// Enumera los roles jugables que otros sistemas podran asignar al jugador.
    /// Este enum solo describe identidad de rol; no implementa seleccion, balance ni habilidades.
    /// </summary>
    public enum PlayerRoleType
    {
        /// <summary>
        /// El jugador no tiene un rol asignado.
        /// </summary>
        None = 0,

        /// <summary>
        /// Rol preparado para futuras mecanicas de percepcion espiritual.
        /// </summary>
        Medium = 1,

        /// <summary>
        /// Rol preparado para futuras mecanicas de limpieza o contencion ritual.
        /// </summary>
        Exorcist = 2,

        /// <summary>
        /// Rol preparado para futuras mecanicas de evidencia visual.
        /// </summary>
        Photographer = 3,

        /// <summary>
        /// Rol preparado para futuras mecanicas de investigacion y lectura del entorno.
        /// </summary>
        Investigator = 4,

        /// <summary>
        /// Rol preparado para futuras mecanicas defensivas o de soporte.
        /// </summary>
        Monk = 5,

        /// <summary>
        /// Rol generico para jugadores del equipo superviviente.
        /// </summary>
        Survivor = 6
    }

    /// <summary>
    /// Define el equipo logico actual del jugador dentro de una sesion multijugador.
    /// El Player System solo almacena este dato; la asignacion real pertenece a sistemas externos.
    /// </summary>
    public enum PlayerTeam
    {
        /// <summary>
        /// El jugador todavia no pertenece a un equipo definido.
        /// </summary>
        None = 0,

        /// <summary>
        /// Equipo de jugadores supervivientes.
        /// </summary>
        Survivor = 1,

        /// <summary>
        /// Equipo reservado para el jugador que actue como muneco.
        /// </summary>
        Doll = 2,

        /// <summary>
        /// Estado de equipo usado cuando el jugador observa sin participar activamente.
        /// </summary>
        Spectator = 3
    }

    /// <summary>
    /// Representa el estado de vida o participacion actual del jugador.
    /// Estos estados sirven para decidir si puede moverse, interactuar o ser controlado.
    /// </summary>
    public enum PlayerLifeStateType
    {
        /// <summary>
        /// El jugador esta activo y puede participar normalmente.
        /// </summary>
        Alive = 0,

        /// <summary>
        /// El jugador esta incapacitado, pero no completamente muerto.
        /// </summary>
        Downed = 1,

        /// <summary>
        /// El jugador esta muerto y no debe ejecutar acciones normales.
        /// </summary>
        Dead = 2,

        /// <summary>
        /// El jugador observa la partida sin participar directamente.
        /// </summary>
        Spectator = 3,

        /// <summary>
        /// El jugador fue transformado temporalmente por una mecanica futura.
        /// </summary>
        TemporarilyTransformed = 4
    }

    /// <summary>
    /// Clasifica items desde la perspectiva del jugador sin definir todavia su comportamiento concreto.
    /// </summary>
    public enum PlayerItemType
    {
        /// <summary>
        /// No existe un tipo de item valido.
        /// </summary>
        None = 0,

        /// <summary>
        /// Item relacionado con pasos del ritual.
        /// </summary>
        Ritual = 1,

        /// <summary>
        /// Item relacionado con deteccion, pistas o evidencia.
        /// </summary>
        Detection = 2,

        /// <summary>
        /// Item relacionado con proteccion o defensa.
        /// </summary>
        Defensive = 3,

        /// <summary>
        /// Item que puede consumirse al usarse.
        /// </summary>
        Consumable = 4,

        /// <summary>
        /// Herramienta utilitaria que puede tener usos repetidos o contextuales.
        /// </summary>
        Tool = 5
    }
}
