using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Rounds;
using UnityEngine;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Mantiene una copia local de las puntuaciones que deben mostrarse en el scoreboard.
    /// </summary>
    public class ScoreboardUI : MonoBehaviour
    {
        /// <summary>
        /// Ultima coleccion de puntuaciones recibida por la interfaz.
        /// </summary>
        [SerializeField] private List<PlayerScoreData> displayedScores = new List<PlayerScoreData>();

        /// <summary>
        /// Obtiene la vista de solo lectura de las puntuaciones actualmente almacenadas.
        /// </summary>
        public IReadOnlyList<PlayerScoreData> DisplayedScores => displayedScores;

        /// <summary>
        /// Sustituye la lista de puntuaciones visibles por una copia de la coleccion recibida.
        /// </summary>
        /// <param name="scores">
        /// Coleccion de puntuaciones que debe reflejar la interfaz.
        /// </param>
        public void SetScores(IEnumerable<PlayerScoreData> scores)
        {
            displayedScores = scores != null
                ? scores.Select(score => new PlayerScoreData
                {
                    PlayerId = score.PlayerId,
                    PlayerName = score.PlayerName,
                    Score = score.Score,
                    WasDollThisRound = score.WasDollThisRound
                }).ToList()
                : new List<PlayerScoreData>();
        }
    }
}
