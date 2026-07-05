using System.Collections;
using HitoriKakurembo.Multiplayer.Networking;
using TestMultiplayer.Gameplay;
using TestMultiplayer.Networking;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Multiplayer.Gameplay
{
    /// <summary>
    /// Spawner server-side de pawns jugables para la escena de juego de Hitori.
    /// Usa los PlayerBrain conectados por la sesion base y asigna un cuerpo a cada jugador que aun no tenga pawn.
    /// </summary>
    public class HitoriGamePawnSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkPawn survivorPawnPrefab;
        [SerializeField] private NetworkPawn dollPawnPrefab;
        [SerializeField] private float spawnSpacing = 2.75f;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                StartCoroutine(SpawnWhenBrainsExist());
            }
        }

        /// <summary>
        /// Espera unos frames a que Netcode termine de materializar los PlayerBrain antes de asignar cuerpos.
        /// </summary>
        private IEnumerator SpawnWhenBrainsExist()
        {
            yield return null;
            yield return null;

            TestMultiplayerSessionManager session = TestMultiplayerSessionManager.Instance;

            if (session == null || survivorPawnPrefab == null)
            {
                yield break;
            }

            int index = 0;

            foreach (TestMultiplayerPlayerBrain brain in session.Brains)
            {
                if (brain == null || brain.CurrentPawn != null)
                {
                    continue;
                }

                NetworkPawn prefab = ResolvePawnPrefab(brain);
                Vector3 position = new Vector3((index - 2) * spawnSpacing, 1f, 0f);
                NetworkPawn pawn = Instantiate(prefab, position, Quaternion.identity);
                pawn.NetworkObject.Spawn(true);
                brain.AssignPawnOnServer(pawn);
                index++;
            }
        }

        /// <summary>
        /// Selecciona el prefab correcto segun el estado sincronizado del brain.
        /// Por ahora todos usan superviviente salvo que el brain ya venga marcado como muneco.
        /// </summary>
        private NetworkPawn ResolvePawnPrefab(TestMultiplayerPlayerBrain brain)
        {
            if (brain is HitoriPlayerBrain hitoriBrain && hitoriBrain.IsDoll && dollPawnPrefab != null)
            {
                return dollPawnPrefab;
            }

            return survivorPawnPrefab;
        }
    }
}
