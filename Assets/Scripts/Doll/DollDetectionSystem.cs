using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Player;
using UnityEngine;

namespace HitoriKakurembo.Doll
{
    /// <summary>
    /// Detecta jugadores cercanos al muneco dentro de un radio configurable.
    /// </summary>
    public class DollDetectionSystem : MonoBehaviour
    {
        /// <summary>
        /// Radio utilizado para considerar que un jugador fue detectado.
        /// </summary>
        [SerializeField] private float detectionRadius = 6f;

        /// <summary>
        /// Obtiene la lista de jugadores dentro del radio de deteccion actual.
        /// </summary>
        /// <returns>
        /// Lista de jugadores detectados alrededor del muneco.
        /// </returns>
        public IReadOnlyList<PlayerController> GetDetectedPlayers()
        {
            return FindObjectsByType<PlayerController>()
                .Where(player => player != null && player.gameObject != gameObject)
                .Where(player => Vector3.Distance(transform.position, player.transform.position) <= detectionRadius)
                .ToList();
        }
    }
}
