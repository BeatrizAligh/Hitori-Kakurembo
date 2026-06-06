namespace HitoriKakurembo.Traps
{
    /// <summary>
    /// Implementa la trampa base de tipo Screamer.
    /// </summary>
    public class ScreamerTrap : TrapBase
    {
        /// <summary>
        /// Obtiene el tipo concreto de trampa.
        /// </summary>
        public override TrapType Type => TrapType.Screamer;
    }
}
