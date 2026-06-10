using System;

namespace HitoriKakurembo.Spawning
{
    /// <summary>
    /// Clasifica el tipo funcional de una zona de spawn.
    /// Permite separar zonas generales, habitaciones, paredes o areas reservadas para objetivos rituales sin depender del nombre del GameObject.
    /// </summary>
    public enum SpawnAreaType
    {
        /// <summary>
        /// Zona generica util para prototipos o areas no especializadas.
        /// </summary>
        Generic = 0,

        /// <summary>
        /// Zona que representa una habitacion o volumen jugable completo.
        /// </summary>
        Room = 1,

        /// <summary>
        /// Zona pensada principalmente para colocar objetos sobre paredes.
        /// </summary>
        WallZone = 2,

        /// <summary>
        /// Zona pensada principalmente para colocar objetos sobre suelo.
        /// </summary>
        FloorZone = 3,

        /// <summary>
        /// Zona pensada principalmente para objetos colgantes o colocacion en techo.
        /// </summary>
        CeilingZone = 4,

        /// <summary>
        /// Zona reservada para objetivos o elementos rituales.
        /// </summary>
        Ritual = 5,

        /// <summary>
        /// Zona reservada para items fisicos de gameplay.
        /// </summary>
        Item = 6
    }

    /// <summary>
    /// Describe el tipo de superficie detectado a partir de la normal fisica.
    /// Es un enum con flags para que una SpawnArea pueda aceptar varias superficies a la vez.
    /// </summary>
    [Flags]
    public enum SurfaceType
    {
        /// <summary>
        /// No se detecto una superficie valida.
        /// </summary>
        None = 0,

        /// <summary>
        /// Superficie principalmente vertical, como una pared.
        /// </summary>
        Wall = 1 << 0,

        /// <summary>
        /// Superficie principalmente horizontal hacia arriba, como un piso.
        /// </summary>
        Floor = 1 << 1,

        /// <summary>
        /// Superficie principalmente horizontal hacia abajo, como un techo.
        /// </summary>
        Ceiling = 1 << 2,

        /// <summary>
        /// Superficie con pendiente intermedia.
        /// </summary>
        Inclined = 1 << 3,

        /// <summary>
        /// Cualquier superficie soportada por el detector.
        /// </summary>
        Any = Wall | Floor | Ceiling | Inclined
    }
}
