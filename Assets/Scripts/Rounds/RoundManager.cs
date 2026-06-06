using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Core;
using HitoriKakurembo.Doll;
using HitoriKakurembo.Network;
using HitoriKakurembo.Ritual;
using HitoriKakurembo.Roles;
using HitoriKakurembo.Seals;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Rounds
{
    /// <summary>
    /// Coordina el loop inicial de rondas desde el servidor y replica snapshots simples a todos los clientes.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        /// <summary>
        /// Mensaje NGO usado por el servidor para publicar el estado actual de ronda.
        /// </summary>
        private const string RoundSnapshotMessageName = "HK_RoundSnapshot";

        /// <summary>
        /// Mensaje NGO usado por un cliente para pedir al servidor una copia actualizada del estado de ronda.
        /// </summary>
        private const string RoundSnapshotRequestMessageName = "HK_RoundSnapshotRequest";

        /// <summary>
        /// Mensaje NGO usado por el muneco para pedir al servidor eliminar a un superviviente cercano.
        /// </summary>
        private const string DollAttackRequestMessageName = "HK_DollAttackRequest";

        /// <summary>
        /// Mensaje NGO usado por supervivientes para pedir al servidor eliminar al muneco vulnerable.
        /// </summary>
        private const string DollExorcismRequestMessageName = "HK_DollExorcismRequest";

        /// <summary>
        /// Capacidad maxima del buffer de snapshot de ronda.
        /// </summary>
        private const int RoundSnapshotWriterCapacity = 192;

        /// <summary>
        /// Frecuencia con la que el servidor publica el temporizador de ronda a los clientes.
        /// </summary>
        private const float RoundSnapshotSendInterval = 0.25f;

        /// <summary>
        /// Frecuencia con la que un cliente recien cargado pide el estado si aun no recibio snapshot.
        /// </summary>
        private const float RoundSnapshotRequestInterval = 1f;

        /// <summary>
        /// Cantidad minima de jugadores conectados necesaria para preparar una ronda valida.
        /// </summary>
        [SerializeField] private int minimumPlayers = 2;

        /// <summary>
        /// Duracion provisional de la fase de preparacion, en segundos.
        /// </summary>
        [SerializeField] private float preparationDuration = 10f;

        /// <summary>
        /// Duracion provisional de la fase ritual, en segundos.
        /// </summary>
        [SerializeField] private float ritualDuration = 10f;

        /// <summary>
        /// Duracion provisional de la fase de caceria, en segundos.
        /// </summary>
        [SerializeField] private float huntDuration = 30f;

        /// <summary>
        /// Duracion provisional del cierre de ronda antes de avanzar a la siguiente.
        /// </summary>
        [SerializeField] private float scoringDuration = 6f;

        /// <summary>
        /// Distancia maxima validada por servidor para aceptar una eliminacion del muneco.
        /// </summary>
        [SerializeField] private float dollAttackRange = 2.4f;

        /// <summary>
        /// Distancia maxima validada por servidor para aceptar el exorcismo final contra el muneco.
        /// </summary>
        [SerializeField] private float dollExorcismRange = 2.8f;

        /// <summary>
        /// Puntos otorgados al muneco por cada superviviente eliminado durante la caceria.
        /// </summary>
        [SerializeField] private int pointsPerSurvivorEliminated = 10;

        /// <summary>
        /// Puntos otorgados al superviviente que completa la accion final contra el muneco.
        /// </summary>
        [SerializeField] private int pointsForDollExorcism = 15;

        /// <summary>
        /// Puntos extra otorgados al muneco cuando gana la ronda.
        /// </summary>
        [SerializeField] private int dollVictoryPoints = 25;

        /// <summary>
        /// Puntos otorgados a cada superviviente vivo cuando el equipo activa todos los sellos.
        /// </summary>
        [SerializeField] private int survivorVictoryPoints = 20;

        /// <summary>
        /// Lista cacheada de jugadores participantes en la ronda actualmente preparada.
        /// </summary>
        private readonly List<NetworkPlayer> activeRoundPlayers = new List<NetworkPlayer>();

        /// <summary>
        /// Orden autoritativo de jugadores que actuaran como muneco durante la partida.
        /// </summary>
        private readonly List<ulong> dollRotationOrder = new List<ulong>();

        /// <summary>
        /// Referencia cacheada al manager de red usado para consultar jugadores conectados.
        /// </summary>
        private NetworkGameManager networkGameManager;

        /// <summary>
        /// Referencia cacheada al manager ritual local para reflejar la fase actual en sistemas no sincronizados todavia.
        /// </summary>
        private RitualManager ritualManager;

        /// <summary>
        /// Referencia cacheada al manager de sellos para reiniciar objetivos al comenzar cada ronda.
        /// </summary>
        private SealManager sealManager;

        /// <summary>
        /// Referencia cacheada al manager de puntuacion; solo el servidor debe modificar puntos.
        /// </summary>
        private ScoreManager scoreManager;

        /// <summary>
        /// Instancia de mensajeria NGO sobre la que se registraron los handlers de ronda.
        /// </summary>
        private CustomMessagingManager registeredMessagingManager;

        /// <summary>
        /// Indice actual dentro del orden de rotacion de munecos.
        /// </summary>
        private int currentDollRotationIndex = -1;

        /// <summary>
        /// Indica si los mensajes personalizados de ronda ya fueron registrados.
        /// </summary>
        private bool roundMessageHandlersRegistered;

        /// <summary>
        /// Proximo tiempo local en el que el servidor enviara un snapshot de temporizador.
        /// </summary>
        private float nextRoundSnapshotSendTime;

        /// <summary>
        /// Proximo tiempo local en el que el cliente pedira un snapshot si aun no tiene estado valido.
        /// </summary>
        private float nextRoundSnapshotRequestTime;

        /// <summary>
        /// Indica si esta instancia cliente ya recibio al menos un snapshot de ronda desde el servidor.
        /// </summary>
        private bool hasReceivedRoundSnapshot;

        /// <summary>
        /// Obtiene el numero de ronda actual, comenzando en 1 cuando la partida inicia.
        /// </summary>
        public int CurrentRoundNumber { get; private set; }

        /// <summary>
        /// Obtiene la cantidad total de rondas planificadas para la partida actual.
        /// </summary>
        public int TotalRounds { get; private set; }

        /// <summary>
        /// Obtiene el estado de alto nivel de la ronda actual.
        /// </summary>
        public RoundState CurrentState { get; private set; } = RoundState.WaitingForPlayers;

        /// <summary>
        /// Obtiene la fase ritual sincronizada asociada al estado actual de la ronda.
        /// </summary>
        public RitualPhase CurrentRitualPhase { get; private set; } = RitualPhase.Preparation;

        /// <summary>
        /// Obtiene el client id del jugador actualmente asignado como muneco.
        /// </summary>
        public ulong CurrentDollPlayerId { get; private set; } = ulong.MaxValue;

        /// <summary>
        /// Obtiene el tiempo restante de la fase actual en segundos.
        /// </summary>
        public float RemainingPhaseTime { get; private set; }

        /// <summary>
        /// Obtiene la duracion total configurada para la fase actual.
        /// </summary>
        public float CurrentPhaseDuration { get; private set; }

        /// <summary>
        /// Obtiene el resultado sincronizado de la ronda actual.
        /// </summary>
        public RoundOutcome CurrentOutcome { get; private set; } = RoundOutcome.None;

        /// <summary>
        /// Obtiene la causa sincronizada que explica por que termino la ronda actual.
        /// </summary>
        public RoundEndReason CurrentEndReason { get; private set; } = RoundEndReason.None;

        /// <summary>
        /// Obtiene si los seis sellos ya hicieron vulnerable al muneco para la accion ritual final.
        /// </summary>
        public bool IsDollVulnerable { get; private set; }

        /// <summary>
        /// Obtiene la lista de solo lectura de jugadores incluidos en la ronda actual.
        /// </summary>
        public IReadOnlyList<NetworkPlayer> ActiveRoundPlayers => activeRoundPlayers;

        /// <summary>
        /// Registra el manager en el localizador de servicios y resuelve dependencias iniciales.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<RoundManager>(this);
            ResolveDependencies();
        }

        /// <summary>
        /// Se suscribe a la mensajeria de Netcode mientras el componente esta activo.
        /// </summary>
        private void OnEnable()
        {
            RegisterRoundMessageHandlers();
        }

        /// <summary>
        /// Cancela los handlers de mensajes de ronda cuando el componente se desactiva.
        /// </summary>
        private void OnDisable()
        {
            UnregisterRoundMessageHandlers();
        }

        /// <summary>
        /// Avanza el temporizador en servidor y mantiene a los clientes sincronizados con snapshots ligeros.
        /// </summary>
        private void Update()
        {
            RegisterRoundMessageHandlers();

            if (IsServerActive())
            {
                TickServerRoundTimer();
                return;
            }

            if (ShouldRequestRoundSnapshot())
            {
                nextRoundSnapshotRequestTime = Time.unscaledTime + RoundSnapshotRequestInterval;
                SendRoundSnapshotRequestToServer();
            }
        }

        /// <summary>
        /// Prepara la siguiente ronda recopilando jugadores, seleccionando al muneco y asignando estados iniciales.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando la ronda pudo prepararse; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool PrepareRound()
        {
            if (!IsServerActive())
            {
                return false;
            }

            ResolveDependencies();

            if (!RefreshActiveRoundPlayers())
            {
                SetWaitingForPlayersState();
                return false;
            }

            EnsureDollRotationOrder();
            return true;
        }

        /// <summary>
        /// Inicia la siguiente ronda de la partida y publica el estado inicial a todos los clientes.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando la ronda se preparo e inicio correctamente; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool StartNextRound()
        {
            if (!PrepareRound())
            {
                return false;
            }

            if (TotalRounds > 0 && CurrentRoundNumber >= TotalRounds)
            {
                CompleteMatch();
                return false;
            }

            NetworkPlayer selectedDoll = SelectNextDoll(activeRoundPlayers);

            if (selectedDoll == null)
            {
                SetWaitingForPlayersState();
                return false;
            }

            CurrentRoundNumber++;
            CurrentState = RoundState.Preparing;
            CurrentRitualPhase = RitualPhase.Preparation;
            CurrentDollPlayerId = selectedDoll.OwnerClientId;
            CurrentOutcome = RoundOutcome.None;
            CurrentEndReason = RoundEndReason.None;
            IsDollVulnerable = false;
            SetPhaseTimer(preparationDuration);
            AssignRoundRoles(selectedDoll);
            sealManager?.ResetAllSeals();
            ApplyRitualPhaseLocally();
            SendRoundSnapshotToAllClients();
            return true;
        }

        /// <summary>
        /// Fuerza el paso a caceria desde servidor cuando otro sistema valide que el ritual debe avanzar.
        /// </summary>
        public void StartHuntPhase()
        {
            if (!IsServerActive())
            {
                return;
            }

            EnterHuntPhase();
        }

        /// <summary>
        /// Marca la ronda actual como completada e inicia el cierre de puntuacion provisional.
        /// </summary>
        public void CompleteRound()
        {
            if (!IsServerActive())
            {
                return;
            }

            CompleteRoundWithOutcome(RoundOutcome.Draw, RoundEndReason.Manual);
        }

        /// <summary>
        /// Notifica al servidor que todos los sellos requeridos fueron activados y que el muneco puede ser eliminado por el ritual final.
        /// </summary>
        public void NotifyAllSealsActivatedOnServer()
        {
            if (!IsServerActive() || CurrentState != RoundState.Playing || CurrentOutcome != RoundOutcome.None)
            {
                return;
            }

            IsDollVulnerable = true;
            SendRoundSnapshotToAllClients();
        }

        /// <summary>
        /// Solicita desde el jugador local que el muneco elimine a un objetivo cercano; el servidor conserva la validacion real.
        /// </summary>
        /// <param name="targetClientId">
        /// Client id del jugador objetivo seleccionado localmente.
        /// </param>
        public void RequestDollAttackFromLocalPlayer(ulong targetClientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (targetClientId == ulong.MaxValue || networkManager == null || !networkManager.IsListening || !networkManager.IsClient)
            {
                return;
            }

            if (networkManager.IsServer)
            {
                TryEliminateSurvivorOnServer(networkManager.LocalClientId, targetClientId);
                return;
            }

            if (networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp))
            {
                writer.WriteValueSafe(targetClientId);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    DollAttackRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Solicita desde un superviviente local ejecutar la accion ritual final contra el muneco vulnerable.
        /// </summary>
        /// <param name="targetDollClientId">
        /// Client id del muneco que el superviviente intenta eliminar.
        /// </param>
        public void RequestDollExorcismFromLocalPlayer(ulong targetDollClientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (targetDollClientId == ulong.MaxValue || networkManager == null || !networkManager.IsListening || !networkManager.IsClient)
            {
                return;
            }

            if (networkManager.IsServer)
            {
                TryExorciseDollOnServer(networkManager.LocalClientId, targetDollClientId);
                return;
            }

            if (networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp))
            {
                writer.WriteValueSafe(targetDollClientId);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    DollExorcismRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Devuelve una descripcion legible del resultado actual para HUD y depuracion.
        /// </summary>
        /// <returns>
        /// Texto corto que explica el resultado o una cadena vacia si la ronda sigue sin cierre.
        /// </returns>
        public string GetCurrentOutcomeDisplayText()
        {
            if (CurrentOutcome == RoundOutcome.None)
            {
                return string.Empty;
            }

            switch (CurrentEndReason)
            {
                case RoundEndReason.AllSealsActivated:
                    return "Los 6 sellos fueron activados.";
                case RoundEndReason.DollExorcised:
                    return "Supervivientes ganan: el muneco fue eliminado con el ritual final.";
                case RoundEndReason.AllSurvivorsEliminated:
                    return "Muneco gana: todos los supervivientes fueron eliminados.";
                case RoundEndReason.HuntTimerExpired:
                    return "Muneco gana: la caceria termino sin completar los sellos.";
                case RoundEndReason.Manual:
                    return "Ronda cerrada manualmente.";
                default:
                    return CurrentOutcome == RoundOutcome.SurvivorsWin
                        ? "Supervivientes ganan la ronda."
                        : CurrentOutcome == RoundOutcome.DollWin
                            ? "Muneco gana la ronda."
                            : "Ronda empatada.";
            }
        }

        /// <summary>
        /// Resuelve el jugador actualmente asignado como muneco dentro de la lista activa de la ronda.
        /// </summary>
        /// <returns>
        /// Jugador asignado como muneco, o <see langword="null"/> cuando aun no existe asignacion valida.
        /// </returns>
        public NetworkPlayer GetCurrentDoll()
        {
            ResolveDependencies();

            return activeRoundPlayers.FirstOrDefault(player => player != null && player.OwnerClientId == CurrentDollPlayerId)
                ?? networkGameManager?.GetPlayer(CurrentDollPlayerId);
        }

        /// <summary>
        /// Obtiene un nombre visible para el muneco actual usando los jugadores sincronizados disponibles localmente.
        /// </summary>
        /// <returns>
        /// Nombre del muneco actual o texto de espera si aun no se resolvio.
        /// </returns>
        public string GetCurrentDollDisplayName()
        {
            NetworkPlayer doll = GetCurrentDoll();

            if (doll != null && !string.IsNullOrWhiteSpace(doll.PlayerName))
            {
                return doll.PlayerName;
            }

            return CurrentDollPlayerId == ulong.MaxValue
                ? "Sin asignar"
                : $"Jugador {CurrentDollPlayerId}";
        }

        /// <summary>
        /// Resuelve dependencias locales que pueden aparecer despues de cargar la escena de juego.
        /// </summary>
        private void ResolveDependencies()
        {
            networkGameManager = networkGameManager != null
                ? networkGameManager
                : ServiceLocator.Resolve<NetworkGameManager>() ?? FindAnyObjectByType<NetworkGameManager>();
            ritualManager = ritualManager != null
                ? ritualManager
                : ServiceLocator.Resolve<RitualManager>() ?? FindAnyObjectByType<RitualManager>();
            sealManager = sealManager != null
                ? sealManager
                : ServiceLocator.Resolve<SealManager>() ?? FindAnyObjectByType<SealManager>();
            scoreManager = scoreManager != null
                ? scoreManager
                : ServiceLocator.Resolve<ScoreManager>() ?? FindAnyObjectByType<ScoreManager>();
        }

        /// <summary>
        /// Registra los handlers de snapshots de ronda sobre el CustomMessagingManager activo.
        /// </summary>
        private void RegisterRoundMessageHandlers()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            CustomMessagingManager messagingManager = networkManager.CustomMessagingManager;

            if (roundMessageHandlersRegistered && registeredMessagingManager == messagingManager)
            {
                return;
            }

            UnregisterRoundMessageHandlers();
            messagingManager.RegisterNamedMessageHandler(RoundSnapshotMessageName, HandleRoundSnapshotMessage);
            messagingManager.RegisterNamedMessageHandler(RoundSnapshotRequestMessageName, HandleRoundSnapshotRequestMessage);
            messagingManager.RegisterNamedMessageHandler(DollAttackRequestMessageName, HandleDollAttackRequestMessage);
            messagingManager.RegisterNamedMessageHandler(DollExorcismRequestMessageName, HandleDollExorcismRequestMessage);
            registeredMessagingManager = messagingManager;
            roundMessageHandlersRegistered = true;
        }

        /// <summary>
        /// Cancela los handlers registrados para evitar callbacks duplicados al cambiar de escena o cerrar sesion.
        /// </summary>
        private void UnregisterRoundMessageHandlers()
        {
            if (!roundMessageHandlersRegistered || registeredMessagingManager == null)
            {
                return;
            }

            registeredMessagingManager.UnregisterNamedMessageHandler(RoundSnapshotMessageName);
            registeredMessagingManager.UnregisterNamedMessageHandler(RoundSnapshotRequestMessageName);
            registeredMessagingManager.UnregisterNamedMessageHandler(DollAttackRequestMessageName);
            registeredMessagingManager.UnregisterNamedMessageHandler(DollExorcismRequestMessageName);
            registeredMessagingManager = null;
            roundMessageHandlersRegistered = false;
        }

        /// <summary>
        /// Actualiza el temporizador de la fase actual y avanza automaticamente cuando llega a cero.
        /// </summary>
        private void TickServerRoundTimer()
        {
            if (CurrentState != RoundState.Preparing && CurrentState != RoundState.Playing && CurrentState != RoundState.Scoring)
            {
                return;
            }

            RemainingPhaseTime = Mathf.Max(0f, RemainingPhaseTime - Time.deltaTime);

            if (Time.unscaledTime >= nextRoundSnapshotSendTime)
            {
                nextRoundSnapshotSendTime = Time.unscaledTime + RoundSnapshotSendInterval;
                SendRoundSnapshotToAllClients();
            }

            if (RemainingPhaseTime <= 0f)
            {
                AdvanceServerPhase();
            }
        }

        /// <summary>
        /// Decide la siguiente fase del flujo de ronda segun el estado actual.
        /// </summary>
        private void AdvanceServerPhase()
        {
            if (CurrentState == RoundState.Preparing)
            {
                EnterRitualPhase();
                return;
            }

            if (CurrentState == RoundState.Playing && CurrentRitualPhase == RitualPhase.Ritual)
            {
                EnterHuntPhase();
                return;
            }

            if (CurrentState == RoundState.Playing && CurrentRitualPhase == RitualPhase.Hunt)
            {
                CompleteRoundWithOutcome(RoundOutcome.DollWin, RoundEndReason.HuntTimerExpired);
                return;
            }

            if (CurrentState == RoundState.Scoring)
            {
                FinishScoringPhase();
            }
        }

        /// <summary>
        /// Entra en fase ritual y publica la transicion a todos los clientes.
        /// </summary>
        private void EnterRitualPhase()
        {
            CurrentState = RoundState.Playing;
            CurrentRitualPhase = RitualPhase.Ritual;
            SetPhaseTimer(ritualDuration);
            ApplyRitualPhaseLocally();
            SendRoundSnapshotToAllClients();
        }

        /// <summary>
        /// Entra en fase de caceria y publica la transicion a todos los clientes.
        /// </summary>
        private void EnterHuntPhase()
        {
            CurrentState = RoundState.Playing;
            CurrentRitualPhase = RitualPhase.Hunt;
            SetPhaseTimer(huntDuration);
            ApplyRitualPhaseLocally();
            SendRoundSnapshotToAllClients();
        }

        /// <summary>
        /// Entra en cierre de ronda, dejando una ventana corta para mostrar resultados provisionales.
        /// </summary>
        private void EnterScoringPhase()
        {
            CurrentState = RoundState.Scoring;
            SetPhaseTimer(scoringDuration);
            SendRoundSnapshotToAllClients();
        }

        /// <summary>
        /// Cierra la ronda de forma autoritativa, asigna puntos segun el resultado y publica el estado de scoring.
        /// </summary>
        /// <param name="outcome">
        /// Resultado final validado por servidor.
        /// </param>
        /// <param name="endReason">
        /// Causa especifica que activo el cierre de la ronda.
        /// </param>
        private void CompleteRoundWithOutcome(RoundOutcome outcome, RoundEndReason endReason)
        {
            if (!IsServerActive() || CurrentOutcome != RoundOutcome.None || CurrentState == RoundState.Scoring || CurrentState == RoundState.Completed)
            {
                return;
            }

            CurrentOutcome = outcome;
            CurrentEndReason = endReason;
            AwardRoundOutcomePoints(outcome);
            EnterScoringPhase();
        }

        /// <summary>
        /// Otorga puntos de cierre de ronda desde servidor segun el equipo ganador.
        /// </summary>
        /// <param name="outcome">
        /// Resultado final de la ronda.
        /// </param>
        private void AwardRoundOutcomePoints(RoundOutcome outcome)
        {
            ResolveDependencies();

            if (scoreManager == null)
            {
                return;
            }

            if (outcome == RoundOutcome.DollWin)
            {
                scoreManager.AddPointsToDoll(dollVictoryPoints);
                return;
            }

            if (outcome == RoundOutcome.SurvivorsWin)
            {
                scoreManager.AddPointsToAliveSurvivors(survivorVictoryPoints);
            }
        }

        /// <summary>
        /// Valida y aplica una eliminacion solicitada por el muneco usando autoridad de servidor.
        /// </summary>
        /// <param name="attackerClientId">
        /// Client id del jugador que intenta eliminar.
        /// </param>
        /// <param name="targetClientId">
        /// Client id del superviviente objetivo.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la eliminacion fue aceptada y sincronizada.
        /// </returns>
        private bool TryEliminateSurvivorOnServer(ulong attackerClientId, ulong targetClientId)
        {
            if (!CanProcessDollAttack())
            {
                return false;
            }

            ResolveDependencies();

            NetworkPlayer attacker = networkGameManager?.GetPlayer(attackerClientId);
            NetworkPlayer target = networkGameManager?.GetPlayer(targetClientId);

            if (!CanDollEliminateTarget(attacker, target))
            {
                return false;
            }

            target.SetAliveState(false);
            scoreManager?.AddPointsToPlayer(attacker.OwnerClientId, pointsPerSurvivorEliminated);

            if (AreAllSurvivorsEliminated())
            {
                CompleteRoundWithOutcome(RoundOutcome.DollWin, RoundEndReason.AllSurvivorsEliminated);
                return true;
            }

            SendRoundSnapshotToAllClients();
            return true;
        }

        /// <summary>
        /// Valida y aplica la eliminacion final del muneco cuando los sellos ya lo hicieron vulnerable.
        /// </summary>
        /// <param name="survivorClientId">
        /// Client id del superviviente que intenta completar el ritual final.
        /// </param>
        /// <param name="targetDollClientId">
        /// Client id del muneco objetivo.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el exorcismo fue aceptado y la ronda se cerro a favor de supervivientes.
        /// </returns>
        private bool TryExorciseDollOnServer(ulong survivorClientId, ulong targetDollClientId)
        {
            if (!CanProcessDollExorcism())
            {
                return false;
            }

            ResolveDependencies();

            NetworkPlayer survivor = networkGameManager?.GetPlayer(survivorClientId);
            NetworkPlayer doll = networkGameManager?.GetPlayer(targetDollClientId);

            if (!CanSurvivorExorciseDoll(survivor, doll))
            {
                return false;
            }

            doll.SetAliveState(false);
            scoreManager?.AddPointsToPlayer(survivor.OwnerClientId, pointsForDollExorcism);
            CompleteRoundWithOutcome(RoundOutcome.SurvivorsWin, RoundEndReason.DollExorcised);
            return true;
        }

        /// <summary>
        /// Determina si el estado actual permite procesar ataques del muneco.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando la ronda esta en caceria y aun no tiene resultado.
        /// </returns>
        private bool CanProcessDollAttack()
        {
            return IsServerActive()
                && CurrentState == RoundState.Playing
                && CurrentRitualPhase == RitualPhase.Hunt
                && CurrentOutcome == RoundOutcome.None;
        }

        /// <summary>
        /// Determina si el estado actual permite procesar la accion final contra el muneco.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando la ronda esta en caceria, sin resultado y con los sellos completos.
        /// </returns>
        private bool CanProcessDollExorcism()
        {
            return IsServerActive()
                && CurrentState == RoundState.Playing
                && CurrentRitualPhase == RitualPhase.Hunt
                && CurrentOutcome == RoundOutcome.None
                && IsDollVulnerable;
        }

        /// <summary>
        /// Valida que el atacante sea el muneco vivo, que el objetivo sea superviviente vivo y que ambos esten en rango.
        /// </summary>
        /// <param name="attacker">
        /// Jugador que solicita la eliminacion.
        /// </param>
        /// <param name="target">
        /// Jugador objetivo que podria quedar fuera de la ronda.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la eliminacion cumple las reglas actuales.
        /// </returns>
        private bool CanDollEliminateTarget(NetworkPlayer attacker, NetworkPlayer target)
        {
            if (attacker == null || target == null || attacker == target)
            {
                return false;
            }

            if (!attacker.IsDoll || !attacker.IsAlive || target.IsDoll || !target.IsAlive)
            {
                return false;
            }

            float distance = Vector3.Distance(attacker.transform.position, target.transform.position);
            return distance <= dollAttackRange;
        }

        /// <summary>
        /// Valida que un superviviente vivo pueda eliminar al muneco vulnerable desde una distancia permitida.
        /// </summary>
        /// <param name="survivor">
        /// Jugador superviviente que intenta completar la accion final.
        /// </param>
        /// <param name="doll">
        /// Jugador que actualmente debe ser el muneco de la ronda.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el ritual final cumple las reglas de servidor.
        /// </returns>
        private bool CanSurvivorExorciseDoll(NetworkPlayer survivor, NetworkPlayer doll)
        {
            if (survivor == null || doll == null || survivor == doll)
            {
                return false;
            }

            if (survivor.IsDoll || !survivor.IsAlive || !doll.IsDoll || !doll.IsAlive || doll.OwnerClientId != CurrentDollPlayerId)
            {
                return false;
            }

            float distance = Vector3.Distance(survivor.transform.position, doll.transform.position);
            return distance <= dollExorcismRange;
        }

        /// <summary>
        /// Comprueba si todos los supervivientes de la ronda actual ya fueron eliminados.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando no queda ningun superviviente vivo en la lista activa.
        /// </returns>
        private bool AreAllSurvivorsEliminated()
        {
            RefreshActiveRoundPlayers();

            return activeRoundPlayers
                .Where(player => player != null && !player.IsDoll)
                .All(player => !player.IsAlive);
        }

        /// <summary>
        /// Finaliza el cierre de ronda y decide si inicia otra ronda o termina la partida.
        /// </summary>
        private void FinishScoringPhase()
        {
            if (CurrentRoundNumber < TotalRounds)
            {
                StartNextRound();
                return;
            }

            CompleteMatch();
        }

        /// <summary>
        /// Marca la partida como completada tras ejecutar una ronda por cada jugador participante.
        /// </summary>
        private void CompleteMatch()
        {
            CurrentState = RoundState.Completed;
            RemainingPhaseTime = 0f;
            CurrentPhaseDuration = 0f;
            SendRoundSnapshotToAllClients();
        }

        /// <summary>
        /// Actualiza el temporizador visible de la fase actual.
        /// </summary>
        /// <param name="duration">
        /// Duracion en segundos que debe tener la fase.
        /// </param>
        private void SetPhaseTimer(float duration)
        {
            CurrentPhaseDuration = Mathf.Max(0f, duration);
            RemainingPhaseTime = CurrentPhaseDuration;
            nextRoundSnapshotSendTime = 0f;
        }

        /// <summary>
        /// Recopila los jugadores conectados que participaran en la ronda.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando hay suficientes jugadores para continuar.
        /// </returns>
        private bool RefreshActiveRoundPlayers()
        {
            ResolveDependencies();
            activeRoundPlayers.Clear();
            activeRoundPlayers.AddRange(networkGameManager != null
                ? networkGameManager.GetPlayersSnapshot()
                    .Where(player => player != null)
                    .OrderBy(player => player.OwnerClientId)
                : Enumerable.Empty<NetworkPlayer>());

            return activeRoundPlayers.Count >= minimumPlayers;
        }

        /// <summary>
        /// Inicializa el orden de munecos usando los jugadores conectados al comienzo de la partida.
        /// </summary>
        private void EnsureDollRotationOrder()
        {
            if (dollRotationOrder.Count > 0)
            {
                return;
            }

            dollRotationOrder.Clear();
            dollRotationOrder.AddRange(activeRoundPlayers
                .Where(player => player != null)
                .Select(player => player.OwnerClientId)
                .OrderBy(clientId => clientId));
            TotalRounds = dollRotationOrder.Count;
            currentDollRotationIndex = -1;
        }

        /// <summary>
        /// Selecciona el siguiente muneco aplicando una rotacion deterministica sin repetir hasta completar el ciclo.
        /// </summary>
        /// <param name="players">
        /// Jugadores elegibles actualmente conectados.
        /// </param>
        /// <returns>
        /// Jugador seleccionado para actuar como muneco.
        /// </returns>
        private NetworkPlayer SelectNextDoll(IReadOnlyList<NetworkPlayer> players)
        {
            if (players == null || players.Count == 0)
            {
                return null;
            }

            if (dollRotationOrder.Count == 0)
            {
                EnsureDollRotationOrder();
            }

            for (int attempt = 0; attempt < dollRotationOrder.Count; attempt++)
            {
                currentDollRotationIndex = (currentDollRotationIndex + 1) % dollRotationOrder.Count;
                ulong candidateClientId = dollRotationOrder[currentDollRotationIndex];
                NetworkPlayer candidate = players.FirstOrDefault(player => player != null && player.OwnerClientId == candidateClientId);

                if (candidate != null)
                {
                    return candidate;
                }
            }

            return players.Where(player => player != null).OrderBy(player => player.OwnerClientId).FirstOrDefault();
        }

        /// <summary>
        /// Aplica la asignacion de muneco y el rol superviviente por defecto al resto de participantes.
        /// </summary>
        /// <param name="selectedDoll">
        /// Jugador que debe recibir el estado de muneco.
        /// </param>
        private void AssignRoundRoles(NetworkPlayer selectedDoll)
        {
            foreach (NetworkPlayer player in activeRoundPlayers)
            {
                if (player == null)
                {
                    continue;
                }

                bool isDoll = player == selectedDoll;
                player.SetDollState(isDoll);
                player.SetRole(isDoll ? PlayerRoleType.None : PlayerRoleType.Survivor);
                player.SetAliveState(true);
                player.ResetCurrentRoundScore();
                ResetAdvancedDollSystems(player);
            }
        }

        /// <summary>
        /// Reinicia sistemas avanzados asociados al jugador para evitar que espejos o trampas arrastren estado entre rondas.
        /// </summary>
        /// <param name="player">
        /// Jugador cuyo estado avanzado debe limpiarse desde servidor.
        /// </param>
        private static void ResetAdvancedDollSystems(NetworkPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.GetComponent<DollMirrorTeleport>()?.ResetMirrorStateOnServer();
            player.GetComponent<DollTrapManager>()?.ClearAllNetworkTrapsOnServer();
        }

        /// <summary>
        /// Coloca el estado en espera cuando no hay suficientes jugadores para iniciar una ronda.
        /// </summary>
        private void SetWaitingForPlayersState()
        {
            CurrentState = RoundState.WaitingForPlayers;
            CurrentRitualPhase = RitualPhase.Preparation;
            CurrentOutcome = RoundOutcome.None;
            CurrentEndReason = RoundEndReason.None;
            IsDollVulnerable = false;
            RemainingPhaseTime = 0f;
            CurrentPhaseDuration = 0f;
            SendRoundSnapshotToAllClients();
        }

        /// <summary>
        /// Publica el snapshot actual de ronda a todos los clientes conectados.
        /// </summary>
        private void SendRoundSnapshotToAllClients()
        {
            if (!CanSendRoundMessages())
            {
                return;
            }

            using (FastBufferWriter writer = CreateRoundSnapshotWriter())
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(
                    RoundSnapshotMessageName,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Publica el snapshot actual de ronda a un cliente especifico.
        /// </summary>
        /// <param name="clientId">
        /// Cliente que debe recibir el snapshot.
        /// </param>
        private void SendRoundSnapshotToClient(ulong clientId)
        {
            if (!CanSendRoundMessages())
            {
                return;
            }

            using (FastBufferWriter writer = CreateRoundSnapshotWriter())
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    RoundSnapshotMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Construye el payload serializado del estado actual de ronda.
        /// </summary>
        /// <returns>
        /// Writer temporal que debe liberarse despues del envio.
        /// </returns>
        private FastBufferWriter CreateRoundSnapshotWriter()
        {
            FastBufferWriter writer = new FastBufferWriter(RoundSnapshotWriterCapacity, Allocator.Temp);
            writer.WriteValueSafe(CurrentRoundNumber);
            writer.WriteValueSafe(TotalRounds);
            writer.WriteValueSafe((int)CurrentState);
            writer.WriteValueSafe((int)CurrentRitualPhase);
            writer.WriteValueSafe(CurrentDollPlayerId);
            writer.WriteValueSafe(RemainingPhaseTime);
            writer.WriteValueSafe(CurrentPhaseDuration);
            writer.WriteValueSafe((int)CurrentOutcome);
            writer.WriteValueSafe((int)CurrentEndReason);
            writer.WriteValueSafe(IsDollVulnerable);
            return writer;
        }

        /// <summary>
        /// Procesa un snapshot de ronda recibido desde el servidor.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que envio el mensaje; en este flujo debe ser el servidor.
        /// </param>
        /// <param name="reader">
        /// Buffer con los datos serializados de ronda.
        /// </param>
        private void HandleRoundSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (IsServerActive())
            {
                return;
            }

            reader.ReadValueSafe(out int roundNumber);
            reader.ReadValueSafe(out int totalRounds);
            reader.ReadValueSafe(out int stateValue);
            reader.ReadValueSafe(out int ritualPhaseValue);
            reader.ReadValueSafe(out ulong dollClientId);
            reader.ReadValueSafe(out float remainingTime);
            reader.ReadValueSafe(out float phaseDuration);
            reader.ReadValueSafe(out int outcomeValue);
            reader.ReadValueSafe(out int endReasonValue);
            reader.ReadValueSafe(out bool isDollVulnerable);

            CurrentRoundNumber = roundNumber;
            TotalRounds = totalRounds;
            CurrentState = (RoundState)stateValue;
            CurrentRitualPhase = (RitualPhase)ritualPhaseValue;
            CurrentDollPlayerId = dollClientId;
            RemainingPhaseTime = Mathf.Max(0f, remainingTime);
            CurrentPhaseDuration = Mathf.Max(0f, phaseDuration);
            CurrentOutcome = (RoundOutcome)outcomeValue;
            CurrentEndReason = (RoundEndReason)endReasonValue;
            IsDollVulnerable = isDollVulnerable;
            hasReceivedRoundSnapshot = true;
            ApplyRitualPhaseLocally();
        }

        /// <summary>
        /// Atiende la solicitud de snapshot enviada por un cliente recien cargado.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que solicita el estado actual.
        /// </param>
        /// <param name="reader">
        /// Buffer sin datos adicionales; se mantiene por compatibilidad con la firma de NGO.
        /// </param>
        private void HandleRoundSnapshotRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerActive())
            {
                return;
            }

            SendRoundSnapshotToClient(senderClientId);
        }

        /// <summary>
        /// Atiende una solicitud puntual de ataque enviada por el cliente que controla al muneco.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que pide ejecutar la eliminacion.
        /// </param>
        /// <param name="reader">
        /// Buffer con el client id del objetivo seleccionado por el cliente.
        /// </param>
        private void HandleDollAttackRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerActive())
            {
                return;
            }

            reader.ReadValueSafe(out ulong targetClientId);
            TryEliminateSurvivorOnServer(senderClientId, targetClientId);
        }

        /// <summary>
        /// Atiende una solicitud puntual de exorcismo enviada por un superviviente contra el muneco vulnerable.
        /// </summary>
        /// <param name="senderClientId">
        /// Cliente que pide completar el ritual final.
        /// </param>
        /// <param name="reader">
        /// Buffer con el client id del muneco objetivo.
        /// </param>
        private void HandleDollExorcismRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServerActive())
            {
                return;
            }

            reader.ReadValueSafe(out ulong targetDollClientId);
            TryExorciseDollOnServer(senderClientId, targetDollClientId);
        }

        /// <summary>
        /// Solicita al servidor un snapshot actualizado de la ronda.
        /// </summary>
        private void SendRoundSnapshotRequestToServer()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient || NetworkManager.Singleton.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(1, Allocator.Temp))
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                    RoundSnapshotRequestMessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>
        /// Sincroniza la fase ritual local con la fase replicada por la ronda.
        /// </summary>
        private void ApplyRitualPhaseLocally()
        {
            ResolveDependencies();

            if (ritualManager == null)
            {
                return;
            }

            switch (CurrentRitualPhase)
            {
                case RitualPhase.Preparation:
                    ritualManager.SetPreparationPhase();
                    break;
                case RitualPhase.Ritual:
                    ritualManager.SetRitualPhase();
                    break;
                case RitualPhase.Hunt:
                    ritualManager.SetHuntPhase();
                    break;
            }
        }

        /// <summary>
        /// Determina si esta instancia puede enviar mensajes de ronda como servidor.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando NGO esta escuchando y la instancia local es servidor.
        /// </returns>
        private static bool CanSendRoundMessages()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer
                && NetworkManager.Singleton.CustomMessagingManager != null;
        }

        /// <summary>
        /// Determina si la instancia local actua actualmente como servidor de NGO.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando hay un servidor activo local.
        /// </returns>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer;
        }

        /// <summary>
        /// Determina si el cliente necesita pedir un snapshot inicial de ronda.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando conviene pedir estado al servidor.
        /// </returns>
        private bool ShouldRequestRoundSnapshot()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsClient
                && !NetworkManager.Singleton.IsServer
                && !hasReceivedRoundSnapshot
                && Time.unscaledTime >= nextRoundSnapshotRequestTime;
        }
    }
}
