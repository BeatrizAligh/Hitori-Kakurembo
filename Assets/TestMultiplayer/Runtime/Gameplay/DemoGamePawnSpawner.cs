using System.Collections;
using TestMultiplayer.Networking;
using Unity.Netcode;
using UnityEngine;

namespace TestMultiplayer.Gameplay
{
    public class DemoGamePawnSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkPawn pawnPrefab;
        [SerializeField] private float spawnSpacing = 2.5f;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                StartCoroutine(SpawnWhenBrainsExist());
            }
        }

        private IEnumerator SpawnWhenBrainsExist()
        {
            yield return null;
            yield return null;

            TestMultiplayerSessionManager session = TestMultiplayerSessionManager.Instance;

            if (session == null || pawnPrefab == null)
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

                Vector3 position = new Vector3((index - 2) * spawnSpacing, 0f, 0f);
                NetworkPawn pawn = Instantiate(pawnPrefab, position, Quaternion.identity);
                pawn.NetworkObject.Spawn(true);
                brain.AssignPawnOnServer(pawn);
                index++;
            }
        }
    }
}
