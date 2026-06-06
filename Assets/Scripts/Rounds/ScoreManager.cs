using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Core;
using HitoriKakurembo.Network;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Rounds
{
    /// <summary>
    /// Centraliza las operaciones de puntuacion y construye snapshots listos para interfaz o depuracion.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        /// <summary>
        /// Referencia cacheada al manager de red usada para resolver jugadores.
        /// </summary>
        private NetworkGameManager networkGameManager;

        /// <summary>
        /// Referencia cacheada al manager de rondas usada para identificar al muneco actual.
        /// </summary>
        private RoundManager roundManager;

        /// <summary>
        /// Registra el manager en el localizador de servicios y resuelve dependencias iniciales.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<ScoreManager>(this);
            networkGameManager = ServiceLocator.Resolve<NetworkGameManager>() ?? FindAnyObjectByType<NetworkGameManager>();
            roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();
        }

        /// <summary>
        /// Agrega puntos al jugador que actualmente actua como muneco.
        /// </summary>
        /// <param name="amount">
        /// Cantidad de puntos que debe sumarse.
        /// </param>
        public void AddPointsToDoll(int amount)
        {
            if (!CanScore())
            {
                return;
            }

            roundManager = roundManager ?? FindAnyObjectByType<RoundManager>();
            NetworkPlayer doll = roundManager?.GetCurrentDoll();

            if (doll == null)
            {
                return;
            }

            AddPointsToNetworkPlayer(doll, amount);
        }

        /// <summary>
        /// Agrega puntos a todos los jugadores conectados que no son el muneco actual.
        /// </summary>
        /// <param name="amount">
        /// Cantidad de puntos que debe recibir cada superviviente.
        /// </param>
        public void AddPointsToSurvivors(int amount)
        {
            if (!CanScore())
            {
                return;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager == null)
            {
                return;
            }

            foreach (NetworkPlayer player in networkGameManager.ConnectedPlayers.Where(player => player != null && !player.IsDoll))
            {
                AddPointsToNetworkPlayer(player, amount);
            }
        }

        /// <summary>
        /// Agrega puntos solo a supervivientes que siguen vivos durante la ronda actual.
        /// </summary>
        /// <param name="amount">
        /// Cantidad de puntos que debe recibir cada superviviente vivo.
        /// </param>
        public void AddPointsToAliveSurvivors(int amount)
        {
            if (!CanScore())
            {
                return;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager == null)
            {
                return;
            }

            foreach (NetworkPlayer player in networkGameManager.ConnectedPlayers.Where(player => player != null && !player.IsDoll && player.IsAlive))
            {
                AddPointsToNetworkPlayer(player, amount);
            }
        }

        /// <summary>
        /// Agrega puntos a un jugador concreto identificado por su player id.
        /// </summary>
        /// <param name="playerId">
        /// Identificador del jugador que debe recibir puntos.
        /// </param>
        /// <param name="amount">
        /// Cantidad de puntos a sumar.
        /// </param>
        public void AddPointsToPlayer(ulong playerId, int amount)
        {
            if (!CanScore())
            {
                return;
            }

            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();
            NetworkPlayer player = networkGameManager?.GetPlayer(playerId);

            if (player == null)
            {
                return;
            }

            AddPointsToNetworkPlayer(player, amount);
        }

        /// <summary>
        /// Construye una copia ordenada del estado actual de puntuaciones.
        /// </summary>
        /// <returns>
        /// Lista de puntuaciones ordenada de mayor a menor puntaje.
        /// </returns>
        public List<PlayerScoreData> BuildScoreSnapshot()
        {
            networkGameManager = networkGameManager ?? FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager == null)
            {
                return new List<PlayerScoreData>();
            }

            return networkGameManager.ConnectedPlayers
                .Where(player => player != null)
                .Select(player => new PlayerScoreData
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    Score = player.CurrentScore,
                    WasDollThisRound = player.IsDoll
                })
                .OrderByDescending(score => score.Score)
                .ToList();
        }

        /// <summary>
        /// Determina si la instancia local tiene autoridad suficiente para modificar puntuaciones.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando existe una sesion NGO activa y la instancia local es el servidor; en caso contrario, <see langword="false"/>.
        /// </returns>
        private static bool CanScore()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }

        /// <summary>
        /// Suma puntos acumulados y puntos de ronda en una sola operacion autoritativa para mantener ambos contadores coherentes.
        /// </summary>
        /// <param name="player">
        /// Jugador sincronizado que debe recibir la puntuacion.
        /// </param>
        /// <param name="amount">
        /// Cantidad positiva de puntos que se agregara.
        /// </param>
        private static void AddPointsToNetworkPlayer(NetworkPlayer player, int amount)
        {
            if (player == null)
            {
                return;
            }

            int safeAmount = Mathf.Max(0, amount);
            player.AddScore(safeAmount);
            player.AddRoundScore(safeAmount);
        }
    }
}
