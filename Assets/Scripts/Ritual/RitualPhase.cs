namespace HitoriKakurembo.Ritual
{
    /// <summary>
    /// Define las fases principales del flujo ritual utilizadas por la partida.
    /// </summary>
    public enum RitualPhase
    {
        /// <summary>
        /// Fase previa al ritual en la que se preparan objetos, jugadores y condiciones.
        /// </summary>
        Preparation = 0,

        /// <summary>
        /// Fase en la que el ritual se considera iniciado y la ronda entra en juego activo.
        /// </summary>
        Ritual = 1,

        /// <summary>
        /// Fase de caceria posterior a la preparacion ritual.
        /// </summary>
        Hunt = 2
    }
}
