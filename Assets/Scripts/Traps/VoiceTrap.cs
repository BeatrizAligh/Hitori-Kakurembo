namespace HitoriKakurembo.Traps
{
    /// <summary>
    /// Implementa la trampa base de tipo Voice.
    /// </summary>
    public class VoiceTrap : TrapBase
    {
        /// <summary>
        /// Obtiene el tipo concreto de trampa.
        /// </summary>
        public override TrapType Type => TrapType.Voice;
    }
}
