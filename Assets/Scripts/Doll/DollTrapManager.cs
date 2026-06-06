using System.Collections.Generic;
using HitoriKakurembo.Core;
using HitoriKakurembo.Network;
using HitoriKakurembo.Ritual;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Traps;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Doll
{
    /// <summary>
    /// Mantiene trampas activas del muneco, valida colocacion en servidor y replica visuales de prototipo a clientes.
    /// </summary>
    public class DollTrapManager : NetworkBehaviour
    {
        /// <summary>
        /// Numero maximo de trampas simultaneas permitidas para el muneco.
        /// </summary>
        [SerializeField] private int maxPlacedTraps = 3;

        /// <summary>
        /// Radio de activacion validado por servidor cuando un superviviente entra cerca de una trampa.
        /// </summary>
        [SerializeField] private float triggerRadius = 1.8f;

        /// <summary>
        /// Distancia frontal a la que se coloca la trampa respecto al muneco.
        /// </summary>
        [SerializeField] private float placementForwardOffset = 1.4f;

        /// <summary>
        /// Segundos minimos entre colocaciones de trampa aceptadas por servidor.
        /// </summary>
        [SerializeField] private float placementCooldown = 5f;

        /// <summary>
        /// Puntos otorgados al muneco cuando una trampa afecta a un superviviente.
        /// </summary>
        [SerializeField] private int pointsPerTriggeredTrap = 5;

        /// <summary>
        /// Lista interna de trampas activas registradas.
        /// </summary>
        private readonly List<TrapBase> activeTraps = new List<TrapBase>();

        /// <summary>
        /// Estado autoritativo de trampas colocadas por este muneco durante la ronda.
        /// </summary>
        private readonly List<ServerTrapState> serverTraps = new List<ServerTrapState>();

        /// <summary>
        /// Visuales locales creados por RPC para representar trampas sincronizadas.
        /// </summary>
        private readonly Dictionary<int, GameObject> localTrapVisuals = new Dictionary<int, GameObject>();

        /// <summary>
        /// Siguiente identificador autoritativo que el servidor asignara a una trampa.
        /// </summary>
        private int nextTrapId = 1;

        /// <summary>
        /// Proximo tiempo de servidor permitido para colocar otra trampa.
        /// </summary>
        private float nextServerPlacementAllowedTime;

        /// <summary>
        /// Obtiene la vista de solo lectura de trampas activas.
        /// </summary>
        public IReadOnlyList<TrapBase> ActiveTraps => activeTraps;

        /// <summary>
        /// Obtiene un valor que indica si aun es posible registrar una nueva trampa.
        /// </summary>
        public bool CanPlaceTrap => activeTraps.Count < maxPlacedTraps;

        /// <summary>
        /// Obtiene cuantas trampas sincronizadas siguen armadas en servidor para este muneco.
        /// </summary>
        public int ArmedTrapCount
        {
            get
            {
                int count = 0;

                foreach (ServerTrapState trap in serverTraps)
                {
                    if (trap != null && trap.IsArmed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Procesa activaciones de trampa en servidor usando posiciones sincronizadas de jugadores.
        /// </summary>
        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            CheckTrapTriggersOnServer();
        }

        /// <summary>
        /// Destruye visuales locales cuando el jugador sale de la red o cambia de escena.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            ClearLocalTrapVisuals();
        }

        /// <summary>
        /// Solicita al servidor colocar una trampa de voz provisional frente al muneco local.
        /// </summary>
        public void RequestPlaceVoiceTrapFromLocalDoll()
        {
            if (!IsOwner)
            {
                return;
            }

            if (IsServer)
            {
                TryPlaceTrapOnServer(TrapType.Voice);
                return;
            }

            RequestPlaceTrapRpc((int)TrapType.Voice);
        }

        /// <summary>
        /// Intenta registrar una trampa nueva dentro del conjunto de trampas activas.
        /// </summary>
        /// <param name="trap">
        /// Trampa que se desea registrar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la trampa fue registrada; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool RegisterTrap(TrapBase trap)
        {
            CleanupDestroyedTraps();

            if (trap == null || !CanPlaceTrap)
            {
                return false;
            }

            activeTraps.Add(trap);
            return true;
        }

        /// <summary>
        /// Elimina una trampa concreta del registro de trampas activas.
        /// </summary>
        /// <param name="trap">
        /// Trampa que se desea remover.
        /// </param>
        public void RemoveTrap(TrapBase trap)
        {
            if (trap != null)
            {
                activeTraps.Remove(trap);
            }
        }

        /// <summary>
        /// Limpia referencias nulas dejadas por trampas destruidas o despawned.
        /// </summary>
        public void CleanupDestroyedTraps()
        {
            activeTraps.RemoveAll(trap => trap == null);
        }

        /// <summary>
        /// Limpia trampas autoritativas y visuales replicados al iniciar una nueva ronda.
        /// </summary>
        public void ClearAllNetworkTrapsOnServer()
        {
            if (!IsServer)
            {
                return;
            }

            serverTraps.Clear();
            nextTrapId = 1;
            nextServerPlacementAllowedTime = 0f;
            ClearTrapVisualsClientRpc();
        }

        /// <summary>
        /// RPC enviado por el propietario para pedir colocar una trampa; el servidor decide si es valida.
        /// </summary>
        /// <param name="trapTypeValue">
        /// Tipo de trampa solicitado como entero serializable.
        /// </param>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestPlaceTrapRpc(int trapTypeValue)
        {
            TryPlaceTrapOnServer((TrapType)trapTypeValue);
        }

        /// <summary>
        /// Valida y registra en servidor una trampa nueva asociada al muneco propietario.
        /// </summary>
        /// <param name="trapType">
        /// Tipo de trampa solicitado.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la trampa fue aceptada y replicada visualmente.
        /// </returns>
        private bool TryPlaceTrapOnServer(TrapType trapType)
        {
            if (!CanProcessTrapPlacementOnServer(trapType))
            {
                return false;
            }

            Vector3 trapPosition = transform.position + (transform.forward * placementForwardOffset);
            trapPosition.y = Mathf.Max(0.25f, trapPosition.y - 0.75f);

            ServerTrapState trapState = new ServerTrapState
            {
                TrapId = nextTrapId++,
                Type = trapType,
                Position = trapPosition,
                IsArmed = true
            };

            serverTraps.Add(trapState);
            nextServerPlacementAllowedTime = Time.time + placementCooldown;
            CreateTrapVisualClientRpc(trapState.TrapId, (int)trapState.Type, trapState.Position);
            return true;
        }

        /// <summary>
        /// Determina si el servidor acepta colocar una trampa en el estado actual de partida.
        /// </summary>
        /// <param name="trapType">
        /// Tipo de trampa solicitado por el muneco.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el jugador es muneco vivo, esta en caceria y respeta limites.
        /// </returns>
        private bool CanProcessTrapPlacementOnServer(TrapType trapType)
        {
            if (!IsServer || trapType == TrapType.None || Time.time < nextServerPlacementAllowedTime)
            {
                return false;
            }

            CleanupServerTrapList();

            if (ArmedTrapCount >= maxPlacedTraps)
            {
                return false;
            }

            NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();

            if (networkPlayer == null || !networkPlayer.IsDoll || !networkPlayer.IsAlive)
            {
                return false;
            }

            RoundManager roundManager = ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();

            return roundManager != null
                && roundManager.CurrentState == RoundState.Playing
                && roundManager.CurrentRitualPhase == RitualPhase.Hunt
                && roundManager.CurrentOutcome == RoundOutcome.None;
        }

        /// <summary>
        /// Revisa si algun superviviente vivo entro en el radio de una trampa armada.
        /// </summary>
        private void CheckTrapTriggersOnServer()
        {
            if (serverTraps.Count == 0)
            {
                return;
            }

            NetworkGameManager networkGameManager = ServiceLocator.Resolve<NetworkGameManager>() ?? FindAnyObjectByType<NetworkGameManager>();
            ScoreManager scoreManager = ServiceLocator.Resolve<ScoreManager>() ?? FindAnyObjectByType<ScoreManager>();

            if (networkGameManager == null)
            {
                return;
            }

            IReadOnlyList<NetworkPlayer> players = networkGameManager.GetPlayersSnapshot();

            foreach (ServerTrapState trap in serverTraps)
            {
                if (trap == null || !trap.IsArmed)
                {
                    continue;
                }

                NetworkPlayer triggeredPlayer = FindTriggeredSurvivor(players, trap.Position);

                if (triggeredPlayer == null)
                {
                    continue;
                }

                trap.IsArmed = false;
                scoreManager?.AddPointsToDoll(pointsPerTriggeredTrap);
                TriggerTrapVisualClientRpc(trap.TrapId, triggeredPlayer.OwnerClientId);
            }
        }

        /// <summary>
        /// Busca un superviviente vivo dentro del radio de activacion de una trampa.
        /// </summary>
        /// <param name="players">
        /// Jugadores sincronizados conocidos por el servidor.
        /// </param>
        /// <param name="trapPosition">
        /// Posicion mundial de la trampa.
        /// </param>
        /// <returns>
        /// Primer superviviente valido en rango o <see langword="null"/> si ninguno activo la trampa.
        /// </returns>
        private NetworkPlayer FindTriggeredSurvivor(IReadOnlyList<NetworkPlayer> players, Vector3 trapPosition)
        {
            if (players == null)
            {
                return null;
            }

            foreach (NetworkPlayer player in players)
            {
                if (player == null || player.IsDoll || !player.IsAlive)
                {
                    continue;
                }

                if (Vector3.Distance(player.transform.position, trapPosition) <= triggerRadius)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// Elimina estados de servidor desarmados para que puedan colocarse nuevas trampas despues de activarse.
        /// </summary>
        private void CleanupServerTrapList()
        {
            serverTraps.RemoveAll(trap => trap == null || !trap.IsArmed);
        }

        /// <summary>
        /// Crea en cada cliente un visual simple para representar una trampa colocada por servidor.
        /// </summary>
        /// <param name="trapId">
        /// Identificador autoritativo de la trampa.
        /// </param>
        /// <param name="trapTypeValue">
        /// Tipo de trampa serializado.
        /// </param>
        /// <param name="position">
        /// Posicion validada por servidor.
        /// </param>
        [Rpc(SendTo.ClientsAndHost)]
        private void CreateTrapVisualClientRpc(int trapId, int trapTypeValue, Vector3 position)
        {
            if (localTrapVisuals.ContainsKey(trapId))
            {
                return;
            }

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = $"PrototypeNetworkTrap_{OwnerClientId}_{trapId}";
            visual.transform.position = position;
            visual.transform.localScale = Vector3.one * 0.45f;

            Collider colliderComponent = visual.GetComponent<Collider>();

            if (colliderComponent != null)
            {
                colliderComponent.isTrigger = true;
            }

            Renderer rendererComponent = visual.GetComponent<Renderer>();

            if (rendererComponent != null)
            {
                rendererComponent.material.color = new Color(0.95f, 0.3f, 0.18f, 1f);
            }

            localTrapVisuals[trapId] = visual;
        }

        /// <summary>
        /// Actualiza visualmente una trampa cuando el servidor confirma que fue activada.
        /// </summary>
        /// <param name="trapId">
        /// Identificador autoritativo de la trampa.
        /// </param>
        /// <param name="triggeredClientId">
        /// Cliente afectado por la trampa; se usa para trazas de depuracion.
        /// </param>
        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerTrapVisualClientRpc(int trapId, ulong triggeredClientId)
        {
            if (!localTrapVisuals.TryGetValue(trapId, out GameObject visual) || visual == null)
            {
                return;
            }

            visual.name = $"PrototypeTriggeredTrap_{OwnerClientId}_{trapId}";
            visual.transform.localScale = Vector3.one * 0.25f;

            Renderer rendererComponent = visual.GetComponent<Renderer>();

            if (rendererComponent != null)
            {
                rendererComponent.material.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            }
        }

        /// <summary>
        /// Ordena a todos los clientes borrar visuales de trampas cuando la ronda reinicia.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void ClearTrapVisualsClientRpc()
        {
            ClearLocalTrapVisuals();
        }

        /// <summary>
        /// Destruye todos los visuales locales asociados a este manager de trampas.
        /// </summary>
        private void ClearLocalTrapVisuals()
        {
            foreach (GameObject visual in localTrapVisuals.Values)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }

            localTrapVisuals.Clear();
        }

        /// <summary>
        /// Estado interno de servidor para una trampa colocada por el muneco.
        /// </summary>
        private sealed class ServerTrapState
        {
            /// <summary>
            /// Identificador unico asignado por servidor.
            /// </summary>
            public int TrapId;

            /// <summary>
            /// Tipo de trampa colocada.
            /// </summary>
            public TrapType Type;

            /// <summary>
            /// Posicion mundial validada por servidor.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Indica si la trampa todavia puede activarse.
            /// </summary>
            public bool IsArmed;
        }
    }
}
