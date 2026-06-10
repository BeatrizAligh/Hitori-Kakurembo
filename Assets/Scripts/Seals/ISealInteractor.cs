namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Contrato minimo que un jugador debe exponer para interactuar con sellos.
    /// Evita que RitualSeal dependa directamente de un PlayerSystem concreto o de managers de ronda.
    /// </summary>
    public interface ISealInteractor
    {
        /// <summary>
        /// Client id propietario del jugador dentro de Netcode.
        /// </summary>
        ulong ClientId { get; }

        /// <summary>
        /// Indica si el jugador pertenece al equipo superviviente.
        /// </summary>
        bool IsSurvivor { get; }

        /// <summary>
        /// Indica si el jugador es el muneco, oso o entidad.
        /// </summary>
        bool IsDoll { get; }

        /// <summary>
        /// Indica si el jugador puede seguir interactuando por estar vivo.
        /// </summary>
        bool IsAlive { get; }
    }
}
