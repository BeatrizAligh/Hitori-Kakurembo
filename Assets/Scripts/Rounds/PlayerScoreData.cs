using System;

namespace HitoriKakurembo.Rounds
{
    /// <summary>
    /// Representa un snapshot serializable de puntuacion utilizado por interfaz y herramientas de depuracion.
    /// </summary>
    [Serializable]
    public class PlayerScoreData
    {
        /// <summary>
        /// Identificador del jugador representado por esta entrada.
        /// </summary>
        public ulong PlayerId;

        /// <summary>
        /// Nombre visible del jugador representado por esta entrada.
        /// </summary>
        public string PlayerName;

        /// <summary>
        /// Puntuacion acumulada del jugador.
        /// </summary>
        public int Score;

        /// <summary>
        /// Indica si el jugador actuo como muneco durante la ronda asociada al snapshot.
        /// </summary>
        public bool WasDollThisRound;
    }
}
