namespace HitoriKakurembo.Traps
{
    /// <summary>
    /// Implementa la trampa base de tipo Teleport.
    /// </summary>
    public class TeleportTrap : TrapBase
    {
        /// <summary>
        /// Obtiene el tipo concreto de trampa.
        /// </summary>
        public override TrapType Type => TrapType.Teleport;
    }
}
