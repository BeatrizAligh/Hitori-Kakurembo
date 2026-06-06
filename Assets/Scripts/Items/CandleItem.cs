namespace HitoriKakurembo.Items
{
    /// <summary>
    /// Implementa el item basico de tipo Candle.
    /// </summary>
    public class CandleItem : ItemBase
    {
        /// <summary>
        /// Obtiene el tipo concreto del item.
        /// </summary>
        public override ItemType Type => ItemType.Candle;
    }
}
