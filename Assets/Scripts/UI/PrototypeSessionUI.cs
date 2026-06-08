using System.Collections.Generic;
using System.Text;
using HitoriKakurembo.House;
using HitoriKakurembo.Network;
using HitoriKakurembo.Player;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Seals;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Construye una interfaz UGUI provisional para el loop multijugador del prototipo mientras las pantallas definitivas aun no fueron authoring en escena.
    /// </summary>
    public class PrototypeSessionUI : MonoBehaviour
    {
        /// <summary>
        /// Nombre canonico de la escena de menu principal.
        /// </summary>
        private const string MainMenuSceneName = "MainMenu";

        /// <summary>
        /// Nombre canonico de la escena de lobby.
        /// </summary>
        private const string LobbySceneName = "LobbyScene";

        /// <summary>
        /// Nombre canonico de la escena principal de juego.
        /// </summary>
        private const string GameSceneName = "GameScene";

        /// <summary>
        /// Referencia cacheada al manager de sesion que expone el estado del flujo Relay y NGO.
        /// </summary>
        private RelaySessionManager relaySessionManager;

        /// <summary>
        /// Canvas runtime persistente sobre el que se reconstruyen las distintas pantallas del prototipo.
        /// </summary>
        private Canvas runtimeCanvas;

        /// <summary>
        /// Campo de texto donde el jugador ingresa o corrige su nombre visible.
        /// </summary>
        private InputField playerNameInputField;

        /// <summary>
        /// Campo donde se captura el codigo Relay al unirse a una partida existente.
        /// </summary>
        private InputField joinCodeInputField;

        /// <summary>
        /// Boton que inicia la creacion de una nueva partida como host.
        /// </summary>
        private Button createMatchButton;

        /// <summary>
        /// Boton que inicia la union a una partida existente como cliente.
        /// </summary>
        private Button joinMatchButton;

        /// <summary>
        /// Boton visible en el lobby para ordenar al host que cargue la escena de juego.
        /// </summary>
        private Button startMatchButton;

        /// <summary>
        /// Boton visible en el lobby para que el jugador local alterne su estado listo/no listo.
        /// </summary>
        private Button readyButton;

        /// <summary>
        /// Boton usado para abandonar la sesion actual o volver al menu principal.
        /// </summary>
        private Button leaveSessionButton;

        /// <summary>
        /// Texto que muestra el codigo de union y el estado general del lobby.
        /// </summary>
        private Text lobbyCodeText;

        /// <summary>
        /// Texto que resume cuantos jugadores hay conectados actualmente.
        /// </summary>
        private Text playerCountText;

        /// <summary>
        /// Texto que lista los jugadores visibles en el lobby o partida.
        /// </summary>
        private Text playerListText;

        /// <summary>
        /// Texto que resume cuantos jugadores estan listos y el rango permitido de la sala.
        /// </summary>
        private Text readinessText;

        /// <summary>
        /// Texto que muestra el modelo de personaje elegido por el jugador local en el lobby.
        /// </summary>
        private Text characterModelText;

        /// <summary>
        /// Boton para retroceder en la lista de modelos visuales disponibles.
        /// </summary>
        private Button previousCharacterModelButton;

        /// <summary>
        /// Boton para avanzar en la lista de modelos visuales disponibles.
        /// </summary>
        private Button nextCharacterModelButton;

        /// <summary>
        /// Texto que describe el estado actual de la ronda cuando ya se esta dentro de la escena de juego.
        /// </summary>
        private Text roundStateText;

        /// <summary>
        /// Texto que muestra el progreso sincronizado de los seis sellos rituales.
        /// </summary>
        private Text sealStateText;

        /// <summary>
        /// Texto que resume el rol y estado del jugador local dentro de la partida.
        /// </summary>
        private Text localPlayerStateText;

        /// <summary>
        /// Texto de estado informativo expuesto por el flujo de sesion.
        /// </summary>
        private Text statusText;

        /// <summary>
        /// Texto de error visible cuando una operacion de red falla.
        /// </summary>
        private Text errorText;

        /// <summary>
        /// Nombre de la ultima escena para la que se reconstruyo la interfaz.
        /// </summary>
        private string lastBuiltSceneName = string.Empty;

        /// <summary>
        /// Inicializa el canvas persistente y asegura la existencia de un event system utilizable por la UI runtime.
        /// </summary>
        private void Awake()
        {
            relaySessionManager = FindAnyObjectByType<RelaySessionManager>();
            RuntimeUIFactory.EnsureEventSystem(transform);
            runtimeCanvas = RuntimeUIFactory.CreateCanvas("PrototypeRuntimeCanvas", transform);
        }

        /// <summary>
        /// Se suscribe al refresco de escenas y al estado visible de la sesion.
        /// </summary>
        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (relaySessionManager != null)
            {
                relaySessionManager.SessionStateChanged += HandleSessionStateChanged;
            }
        }

        /// <summary>
        /// Cancela las suscripciones activas cuando la interfaz se desactiva.
        /// </summary>
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (relaySessionManager != null)
            {
                relaySessionManager.SessionStateChanged -= HandleSessionStateChanged;
            }
        }

        /// <summary>
        /// Construye la interfaz inicial para la escena activa en cuanto el flujo arranca.
        /// </summary>
        private void Start()
        {
            RebuildForCurrentScene();
        }

        /// <summary>
        /// Mantiene sincronizado el contenido visible con la escena activa y el estado mutable de la sesion.
        /// </summary>
        private void Update()
        {
            relaySessionManager = relaySessionManager != null ? relaySessionManager : FindAnyObjectByType<RelaySessionManager>();

            if (runtimeCanvas == null)
            {
                return;
            }

            string currentSceneName = SceneManager.GetActiveScene().name;

            if (currentSceneName != lastBuiltSceneName)
            {
                RebuildForScene(currentSceneName);
            }

            RefreshVisibleState(currentSceneName);
        }

        /// <summary>
        /// Reconstruye la pantalla cuando Unity notifica una nueva carga de escena.
        /// </summary>
        /// <param name="scene">
        /// Escena recien cargada.
        /// </param>
        /// <param name="loadSceneMode">
        /// Modo de carga aplicado.
        /// </param>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            RebuildForScene(scene.name);
        }

        /// <summary>
        /// Fuerza un refresco estructural de la pantalla actual cuando cambia el estado de la sesion.
        /// </summary>
        private void HandleSessionStateChanged()
        {
            RefreshVisibleState(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Reconstruye la UI para la escena actualmente activa.
        /// </summary>
        private void RebuildForCurrentScene()
        {
            RebuildForScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Crea la disposicion de widgets correspondiente a la escena indicada.
        /// </summary>
        /// <param name="sceneName">
        /// Nombre de la escena para la que debe construirse la interfaz.
        /// </param>
        private void RebuildForScene(string sceneName)
        {
            if (runtimeCanvas == null)
            {
                return;
            }

            ResetWidgetReferences();
            RuntimeUIFactory.ClearChildren(runtimeCanvas.transform);
            lastBuiltSceneName = sceneName;

            bool isKnownScene = sceneName == MainMenuSceneName
                || sceneName == LobbySceneName
                || sceneName == GameSceneName;

            runtimeCanvas.gameObject.SetActive(isKnownScene);

            if (!isKnownScene)
            {
                return;
            }

            if (sceneName == MainMenuSceneName)
            {
                BuildMainMenuScreen();
                return;
            }

            if (sceneName == LobbySceneName)
            {
                BuildLobbyScreen();
                return;
            }

            BuildGameScreen();
        }

        /// <summary>
        /// Construye la pantalla de menu principal con el flujo de crear o unirse a una partida.
        /// </summary>
        private void BuildMainMenuScreen()
        {
            RectTransform card = RuntimeUIFactory.CreateCenteredCard("MainMenuCard", runtimeCanvas.transform);
            RuntimeUIFactory.CreateText("Title", card, "Hitori Kakurembo", 40, TextAnchor.MiddleCenter, Color.white);
            RuntimeUIFactory.CreateText("Subtitle", card, "Prototipo multijugador con Netcode for GameObjects y Relay.", 20, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f, 1f));

            RuntimeUIFactory.CreateSpacer(card, 10f);
            RuntimeUIFactory.CreateText("PlayerNameLabel", card, "Nombre visible", 18, TextAnchor.MiddleLeft, new Color(0.84f, 0.88f, 0.93f, 1f));
            playerNameInputField = RuntimeUIFactory.CreateInputField("PlayerNameInput", card, "Ingresa tu alias", relaySessionManager != null ? relaySessionManager.LocalPlayerName : string.Empty);

            createMatchButton = RuntimeUIFactory.CreateButton("CreateMatchButton", card, "Crear partida");
            createMatchButton.onClick.AddListener(HandleCreateMatchClicked);

            RuntimeUIFactory.CreateSpacer(card, 8f);
            RuntimeUIFactory.CreateText("JoinCodeLabel", card, "Codigo de partida", 18, TextAnchor.MiddleLeft, new Color(0.84f, 0.88f, 0.93f, 1f));
            joinCodeInputField = RuntimeUIFactory.CreateInputField("JoinCodeInput", card, "Pega aqui el codigo Relay", string.Empty);

            joinMatchButton = RuntimeUIFactory.CreateButton("JoinMatchButton", card, "Unirse a partida");
            joinMatchButton.onClick.AddListener(HandleJoinMatchClicked);

            RuntimeUIFactory.CreateSpacer(card, 8f);
            statusText = RuntimeUIFactory.CreateText("StatusText", card, string.Empty, 18, TextAnchor.MiddleCenter, new Color(0.75f, 0.84f, 0.92f, 1f));
            errorText = RuntimeUIFactory.CreateText("ErrorText", card, string.Empty, 18, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.55f, 1f));
        }

        /// <summary>
        /// Construye la pantalla de lobby donde se ve el codigo compartido, los jugadores conectados y el inicio de partida.
        /// </summary>
        private void BuildLobbyScreen()
        {
            RectTransform card = RuntimeUIFactory.CreateCenteredCard("LobbyCard", runtimeCanvas.transform);
            RuntimeUIFactory.CreateText("Title", card, "Lobby de Espera", 36, TextAnchor.MiddleCenter, Color.white);
            RuntimeUIFactory.CreateText("Subtitle", card, "El host comparte el codigo y, cuando todos entren, inicia la partida.", 18, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f, 1f));

            lobbyCodeText = RuntimeUIFactory.CreateText("LobbyCodeText", card, string.Empty, 24, TextAnchor.MiddleCenter, new Color(0.96f, 0.88f, 0.56f, 1f));
            playerCountText = RuntimeUIFactory.CreateText("PlayerCountText", card, string.Empty, 20, TextAnchor.MiddleCenter, Color.white);
            readinessText = RuntimeUIFactory.CreateText("ReadinessText", card, string.Empty, 18, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.9f, 1f));
            playerListText = RuntimeUIFactory.CreateText("PlayerListText", card, string.Empty, 18, TextAnchor.UpperLeft, new Color(0.86f, 0.9f, 0.94f, 1f));

            RuntimeUIFactory.CreateSpacer(card, 4f);
            RuntimeUIFactory.CreateText("CharacterModelTitle", card, "Modelo de personaje", 18, TextAnchor.MiddleCenter, new Color(0.96f, 0.88f, 0.56f, 1f));
            characterModelText = RuntimeUIFactory.CreateText("CharacterModelText", card, string.Empty, 18, TextAnchor.MiddleCenter, new Color(0.86f, 0.9f, 0.94f, 1f));
            previousCharacterModelButton = RuntimeUIFactory.CreateButton("PreviousCharacterModelButton", card, "Modelo anterior");
            previousCharacterModelButton.onClick.AddListener(HandlePreviousCharacterModelClicked);
            nextCharacterModelButton = RuntimeUIFactory.CreateButton("NextCharacterModelButton", card, "Modelo siguiente");
            nextCharacterModelButton.onClick.AddListener(HandleNextCharacterModelClicked);

            readyButton = RuntimeUIFactory.CreateButton("ReadyButton", card, "Estoy listo");
            readyButton.onClick.AddListener(HandleReadyClicked);

            startMatchButton = RuntimeUIFactory.CreateButton("StartMatchButton", card, "Iniciar partida");
            startMatchButton.onClick.AddListener(HandleStartMatchClicked);

            leaveSessionButton = RuntimeUIFactory.CreateButton("LeaveSessionButton", card, "Salir al menu");
            leaveSessionButton.onClick.AddListener(HandleLeaveSessionClicked);

            statusText = RuntimeUIFactory.CreateText("StatusText", card, string.Empty, 18, TextAnchor.MiddleCenter, new Color(0.75f, 0.84f, 0.92f, 1f));
            errorText = RuntimeUIFactory.CreateText("ErrorText", card, string.Empty, 18, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.55f, 1f));
        }

        /// <summary>
        /// Construye un HUD de apoyo para la escena de juego con informacion del jugador local, ronda y participantes.
        /// </summary>
        private void BuildGameScreen()
        {
            RectTransform card = RuntimeUIFactory.CreateCenteredCard("GameHudCard", runtimeCanvas.transform);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(24f, -24f);
            card.sizeDelta = new Vector2(520f, 0f);

            RuntimeUIFactory.CreateText("Title", card, "Estado de Partida", 30, TextAnchor.MiddleLeft, Color.white);
            localPlayerStateText = RuntimeUIFactory.CreateText("LocalPlayerStateText", card, string.Empty, 18, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 0.94f, 1f));
            roundStateText = RuntimeUIFactory.CreateText("RoundStateText", card, string.Empty, 18, TextAnchor.MiddleLeft, new Color(0.96f, 0.88f, 0.56f, 1f));
            sealStateText = RuntimeUIFactory.CreateText("SealStateText", card, string.Empty, 18, TextAnchor.UpperLeft, new Color(0.74f, 1f, 0.84f, 1f));
            playerListText = RuntimeUIFactory.CreateText("PlayerListText", card, string.Empty, 18, TextAnchor.UpperLeft, new Color(0.86f, 0.9f, 0.94f, 1f));

            leaveSessionButton = RuntimeUIFactory.CreateButton("LeaveSessionButton", card, "Cerrar sesion");
            leaveSessionButton.onClick.AddListener(HandleLeaveSessionClicked);

            statusText = RuntimeUIFactory.CreateText("StatusText", card, string.Empty, 16, TextAnchor.MiddleLeft, new Color(0.75f, 0.84f, 0.92f, 1f));
            errorText = RuntimeUIFactory.CreateText("ErrorText", card, string.Empty, 16, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.55f, 1f));
        }

        /// <summary>
        /// Refresca el contenido textual y el estado interactivo de la pantalla actualmente visible.
        /// </summary>
        /// <param name="sceneName">
        /// Nombre de la escena cuya pantalla esta activa.
        /// </param>
        private void RefreshVisibleState(string sceneName)
        {
            if (relaySessionManager == null)
            {
                return;
            }

            RefreshStatusArea();

            if (sceneName == MainMenuSceneName)
            {
                RefreshMainMenuState();
                return;
            }

            if (sceneName == LobbySceneName)
            {
                RefreshLobbyState();
                return;
            }

            if (sceneName == GameSceneName)
            {
                RefreshGameState();
            }
        }

        /// <summary>
        /// Actualiza el estado interactivo del menu principal segun la disponibilidad actual de la sesion.
        /// </summary>
        private void RefreshMainMenuState()
        {
            bool isBusy = relaySessionManager.IsBusy;

            if (createMatchButton != null)
            {
                createMatchButton.interactable = !isBusy;
            }

            if (joinMatchButton != null)
            {
                joinMatchButton.interactable = !isBusy;
            }

            if (playerNameInputField != null && !playerNameInputField.isFocused && string.IsNullOrWhiteSpace(playerNameInputField.text))
            {
                playerNameInputField.text = relaySessionManager.LocalPlayerName;
            }
        }

        /// <summary>
        /// Refresca el lobby con codigo de union, conteo de jugadores y disponibilidad del boton de inicio.
        /// </summary>
        private void RefreshLobbyState()
        {
            NetworkPlayer localPlayer = relaySessionManager.GetLocalPlayer();

            if (lobbyCodeText != null)
            {
                string displayedCode = string.IsNullOrWhiteSpace(relaySessionManager.JoinCode)
                    ? "Esperando codigo Relay..."
                    : $"Codigo de union: {relaySessionManager.JoinCode}";
                lobbyCodeText.text = displayedCode;
            }

            if (playerCountText != null)
            {
                playerCountText.text = $"Jugadores conectados: {relaySessionManager.GetConnectedPlayerCount()}";
            }

            if (playerListText != null)
            {
                playerListText.text = BuildPlayerListText(relaySessionManager.GetLobbyPlayerDisplayNames());
            }

            if (readinessText != null)
            {
                readinessText.text = relaySessionManager.GetLobbyReadinessSummary();
            }

            if (characterModelText != null)
            {
                characterModelText.text = localPlayer == null
                    ? "Modelo: esperando jugador local..."
                    : $"Modelo actual: {PlayerCharacterModelCatalog.GetDisplayName(localPlayer.SelectedCharacterModelIndex)}";
            }

            bool canChangeCharacterModel = localPlayer != null && relaySessionManager.CanToggleLocalReady();

            if (previousCharacterModelButton != null)
            {
                previousCharacterModelButton.interactable = canChangeCharacterModel;
            }

            if (nextCharacterModelButton != null)
            {
                nextCharacterModelButton.interactable = canChangeCharacterModel;
            }

            if (readyButton != null)
            {
                readyButton.interactable = relaySessionManager.CanToggleLocalReady();
                Text readyButtonText = readyButton.GetComponentInChildren<Text>();

                if (readyButtonText != null)
                {
                    readyButtonText.text = relaySessionManager.IsLocalPlayerReady()
                        ? "No estoy listo"
                        : "Estoy listo";
                }
            }

            if (startMatchButton != null)
            {
                startMatchButton.interactable = relaySessionManager.CanStartMatch();
                Text startButtonText = startMatchButton.GetComponentInChildren<Text>();

                if (startButtonText != null)
                {
                    if (!relaySessionManager.IsHost)
                    {
                        startButtonText.text = "Solo host inicia";
                    }
                    else
                    {
                        startButtonText.text = relaySessionManager.CanStartMatch()
                            ? "Iniciar partida"
                            : "Esperando listos";
                    }
                }
            }

            if (leaveSessionButton != null)
            {
                leaveSessionButton.interactable = !relaySessionManager.IsBusy;
            }
        }

        /// <summary>
        /// Refresca el HUD de partida con el jugador local, estado de ronda y lista de participantes.
        /// </summary>
        private void RefreshGameState()
        {
            if (localPlayerStateText != null)
            {
                NetworkPlayer localPlayer = relaySessionManager.GetLocalPlayer();
                localPlayerStateText.text = localPlayer == null
                    ? "Jugador local: esperando sincronizacion..."
                    : localPlayer.IsDoll
                        ? $"Jugador local: {localPlayer.PlayerName} | Equipo: Muneco | Estado: {(localPlayer.IsAlive ? "Vivo" : "Fuera")} | Puntos: {localPlayer.CurrentScore} (+{localPlayer.CurrentRoundScore})"
                        : $"Jugador local: {localPlayer.PlayerName} | Rol: {localPlayer.CurrentRole} | Estado: {(localPlayer.IsAlive ? "Vivo" : "Fuera")} | Puntos: {localPlayer.CurrentScore} (+{localPlayer.CurrentRoundScore})";
            }

            if (roundStateText != null)
            {
                RoundManager roundManager = FindAnyObjectByType<RoundManager>();
                roundStateText.text = roundManager == null ? "Ronda: esperando inicializacion..." : BuildRoundStateText(roundManager);
            }

            if (playerListText != null)
            {
                playerListText.text = BuildGamePlayerListText();
            }

            if (sealStateText != null)
            {
                SealManager sealManager = FindAnyObjectByType<SealManager>();
                RoundManager roundManager = FindAnyObjectByType<RoundManager>();
                NetworkPlayer localPlayer = relaySessionManager.GetLocalPlayer();
                string controlHint = BuildGameplayControlHint(localPlayer, roundManager);
                sealStateText.text = sealManager == null
                    ? "Sellos: esperando inicializacion..."
                    : $"{sealManager.GetSealProgressSummary()}\n{controlHint}\n{sealManager.GetSealStatusList()}";
            }

            if (leaveSessionButton != null)
            {
                leaveSessionButton.interactable = !relaySessionManager.IsBusy;
            }
        }

        /// <summary>
        /// Actualiza el bloque comun de estado y error visible en cualquier pantalla del flujo.
        /// </summary>
        private void RefreshStatusArea()
        {
            if (statusText != null)
            {
                statusText.text = relaySessionManager.LastStatusMessage;
            }

            if (errorText != null)
            {
                errorText.text = relaySessionManager.LastErrorMessage;
            }
        }

        /// <summary>
        /// Construye una cadena multilina amigable para presentar la lista visible de jugadores.
        /// </summary>
        /// <param name="playerNames">
        /// Coleccion de nombres ya preparados por el manager de sesion.
        /// </param>
        /// <returns>
        /// Texto final listo para mostrarse en UI.
        /// </returns>
        private static string BuildPlayerListText(IReadOnlyList<string> playerNames)
        {
            if (playerNames == null || playerNames.Count == 0)
            {
                return "Aun no hay jugadores sincronizados en la sesion.";
            }

            StringBuilder builder = new StringBuilder();

            for (int playerIndex = 0; playerIndex < playerNames.Count; playerIndex++)
            {
                builder.Append(playerIndex + 1);
                builder.Append(". ");
                builder.Append(playerNames[playerIndex]);

                if (playerIndex < playerNames.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Construye la lista de jugadores durante la partida usando roles sincronizados por Netcode.
        /// </summary>
        /// <returns>
        /// Texto listo para mostrarse en el HUD de juego.
        /// </returns>
        private static string BuildGamePlayerListText()
        {
            NetworkGameManager networkGameManager = FindAnyObjectByType<NetworkGameManager>();

            if (networkGameManager == null)
            {
                return "Jugadores: esperando sincronizacion...";
            }

            IReadOnlyList<NetworkPlayer> players = networkGameManager.GetPlayersSnapshot();

            if (players == null || players.Count == 0)
            {
                return "Jugadores: esperando sincronizacion...";
            }

            RoundManager roundManager = FindAnyObjectByType<RoundManager>();

            if (roundManager != null && roundManager.CurrentState == RoundState.Completed)
            {
                return BuildFinalRankingText(players);
            }

            StringBuilder builder = new StringBuilder();

            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                NetworkPlayer player = players[playerIndex];

                if (player == null)
                {
                    continue;
                }

                string roleText = player.IsDoll ? "Muneco" : player.CurrentRole.ToString();
                builder.Append(playerIndex + 1);
                builder.Append(". ");
                builder.Append(string.IsNullOrWhiteSpace(player.PlayerName) ? $"Jugador {player.OwnerClientId}" : player.PlayerName);
                builder.Append(" | ");
                builder.Append(roleText);
                builder.Append(" | ");
                builder.Append(player.IsAlive ? "Vivo" : "Fuera");
                builder.Append(" | Total: ");
                builder.Append(player.CurrentScore);
                builder.Append(" | Ronda: +");
                builder.Append(player.CurrentRoundScore);

                if (playerIndex < players.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Construye el texto de estado de ronda incluyendo resultado cuando el servidor ya cerro la ronda.
        /// </summary>
        /// <param name="roundManager">
        /// Manager de ronda desde el que se leen los datos sincronizados.
        /// </param>
        /// <returns>
        /// Texto compacto listo para mostrarse en el HUD.
        /// </returns>
        private static string BuildRoundStateText(RoundManager roundManager)
        {
            string baseText = $"Ronda {roundManager.CurrentRoundNumber}/{roundManager.TotalRounds} | Estado: {roundManager.CurrentState} | Fase: {roundManager.CurrentRitualPhase} | Tiempo: {FormatRoundTime(roundManager.RemainingPhaseTime)} | Muneco: {roundManager.GetCurrentDollDisplayName()}";
            string outcomeText = roundManager.GetCurrentOutcomeDisplayText();
            string safeZoneText = BuildSafeZoneText();

            if (roundManager.CurrentState == RoundState.Completed)
            {
                return string.IsNullOrWhiteSpace(outcomeText)
                    ? $"{baseText}\n{safeZoneText}\nPartida finalizada. Revisa el ranking final."
                    : $"{baseText}\n{safeZoneText}\nResultado final de ronda: {outcomeText}\nPartida finalizada. Revisa el ranking final.";
            }

            if (roundManager.CurrentOutcome == RoundOutcome.None && roundManager.IsDollVulnerable)
            {
                return $"{baseText}\n{safeZoneText}\nObjetivo: muneco vulnerable. Un superviviente debe acercarse y presionar F para completar el ritual final.";
            }

            return string.IsNullOrWhiteSpace(outcomeText)
                ? $"{baseText}\n{safeZoneText}"
                : $"{baseText}\n{safeZoneText}\nResultado: {outcomeText}";
        }

        /// <summary>
        /// Obtiene un resumen compacto de la zona visible sincronizada para el HUD de partida.
        /// </summary>
        /// <returns>
        /// Texto de zona visible o un mensaje de espera si el manager aun no existe.
        /// </returns>
        private static string BuildSafeZoneText()
        {
            SafeZoneManager safeZoneManager = FindAnyObjectByType<SafeZoneManager>();
            return safeZoneManager == null
                ? "Zona visible: esperando sincronizacion..."
                : safeZoneManager.GetVisibleZoneSummary();
        }

        /// <summary>
        /// Construye la pista de controles segun el rol local y el estado sincronizado de la ronda.
        /// </summary>
        /// <param name="localPlayer">
        /// Jugador local usado para decidir si se muestran acciones de muneco o superviviente.
        /// </param>
        /// <param name="roundManager">
        /// Manager de ronda que indica si el muneco ya es vulnerable.
        /// </param>
        /// <returns>
        /// Texto corto de ayuda para pruebas multijugador.
        /// </returns>
        private static string BuildGameplayControlHint(NetworkPlayer localPlayer, RoundManager roundManager)
        {
            if (localPlayer != null && localPlayer.IsDoll)
            {
                return "Muneco: F elimina cerca, Q usa espejo cercano, T coloca trampa de voz.";
            }

            if (roundManager != null && roundManager.IsDollVulnerable && roundManager.CurrentOutcome == RoundOutcome.None)
            {
                return "Superviviente: los 6 sellos estan completos. Presiona F cerca del muneco para completar el ritual final.";
            }

            return "Superviviente: presiona E cerca de un sello para activarlo.";
        }

        /// <summary>
        /// Construye el ranking final ordenado por puntuacion acumulada cuando la partida ya completo todas sus rondas.
        /// </summary>
        /// <param name="players">
        /// Jugadores sincronizados que participaron en la partida.
        /// </param>
        /// <returns>
        /// Texto multilina listo para mostrar en el HUD final.
        /// </returns>
        private static string BuildFinalRankingText(IReadOnlyList<NetworkPlayer> players)
        {
            List<NetworkPlayer> rankedPlayers = new List<NetworkPlayer>(players);
            rankedPlayers.Sort((left, right) =>
            {
                int scoreComparison = right.CurrentScore.CompareTo(left.CurrentScore);
                return scoreComparison != 0 ? scoreComparison : left.OwnerClientId.CompareTo(right.OwnerClientId);
            });

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Ranking final");

            for (int playerIndex = 0; playerIndex < rankedPlayers.Count; playerIndex++)
            {
                NetworkPlayer player = rankedPlayers[playerIndex];

                if (player == null)
                {
                    continue;
                }

                builder.Append(playerIndex + 1);
                builder.Append(". ");
                builder.Append(string.IsNullOrWhiteSpace(player.PlayerName) ? $"Jugador {player.OwnerClientId}" : player.PlayerName);
                builder.Append(" | Total: ");
                builder.Append(player.CurrentScore);
                builder.Append(" | Ultima ronda: +");
                builder.Append(player.CurrentRoundScore);

                if (playerIndex < rankedPlayers.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formatea segundos de ronda en un texto compacto de minutos y segundos.
        /// </summary>
        /// <param name="seconds">
        /// Tiempo restante en segundos.
        /// </param>
        /// <returns>
        /// Cadena en formato MM:SS.
        /// </returns>
        private static string FormatRoundTime(float seconds)
        {
            int safeSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = safeSeconds / 60;
            int remainingSeconds = safeSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        /// <summary>
        /// Ejecuta el flujo de creacion de partida usando los datos capturados desde la UI runtime.
        /// </summary>
        private async void HandleCreateMatchClicked()
        {
            if (relaySessionManager == null)
            {
                return;
            }

            string playerName = playerNameInputField != null ? playerNameInputField.text : string.Empty;
            await relaySessionManager.CreateRelaySessionAsync(playerName);
        }

        /// <summary>
        /// Ejecuta el flujo de union a partida usando el codigo Relay capturado en la UI runtime.
        /// </summary>
        private async void HandleJoinMatchClicked()
        {
            if (relaySessionManager == null)
            {
                return;
            }

            string playerName = playerNameInputField != null ? playerNameInputField.text : string.Empty;
            string joinCode = joinCodeInputField != null ? joinCodeInputField.text : string.Empty;
            await relaySessionManager.JoinRelaySessionAsync(playerName, joinCode);
        }

        /// <summary>
        /// Ordena al host iniciar la escena de juego desde el lobby.
        /// </summary>
        private void HandleStartMatchClicked()
        {
            relaySessionManager?.StartGameFromLobby();
        }

        /// <summary>
        /// Alterna el estado listo/no listo del jugador local desde el lobby.
        /// </summary>
        private void HandleReadyClicked()
        {
            relaySessionManager?.ToggleLocalReadyState();
        }

        /// <summary>
        /// Pide seleccionar el modelo anterior disponible para el jugador local.
        /// </summary>
        private void HandlePreviousCharacterModelClicked()
        {
            ChangeLocalCharacterModel(-1);
        }

        /// <summary>
        /// Pide seleccionar el siguiente modelo disponible para el jugador local.
        /// </summary>
        private void HandleNextCharacterModelClicked()
        {
            ChangeLocalCharacterModel(1);
        }

        /// <summary>
        /// Calcula un nuevo indice de modelo y lo envia al <see cref="NetworkPlayer"/> local para que el servidor lo replique.
        /// </summary>
        /// <param name="direction">
        /// Direccion de cambio: positivo avanza, negativo retrocede.
        /// </param>
        private void ChangeLocalCharacterModel(int direction)
        {
            if (relaySessionManager == null)
            {
                return;
            }

            NetworkPlayer localPlayer = relaySessionManager.GetLocalPlayer();

            if (localPlayer == null)
            {
                return;
            }

            int nextModelIndex = PlayerCharacterModelCatalog.GetWrappedIndex(localPlayer.SelectedCharacterModelIndex, direction);
            localPlayer.SubmitCharacterModelIndex(nextModelIndex);
        }

        /// <summary>
        /// Solicita abandonar la sesion actual y volver al menu principal.
        /// </summary>
        private void HandleLeaveSessionClicked()
        {
            relaySessionManager?.LeaveSessionAndReturnToMenu();
        }

        /// <summary>
        /// Limpia todas las referencias de widgets para evitar reutilizar objetos destruidos despues de reconstruir una pantalla.
        /// </summary>
        private void ResetWidgetReferences()
        {
            playerNameInputField = null;
            joinCodeInputField = null;
            createMatchButton = null;
            joinMatchButton = null;
            startMatchButton = null;
            readyButton = null;
            leaveSessionButton = null;
            lobbyCodeText = null;
            playerCountText = null;
            playerListText = null;
            readinessText = null;
            characterModelText = null;
            previousCharacterModelButton = null;
            nextCharacterModelButton = null;
            roundStateText = null;
            sealStateText = null;
            localPlayerStateText = null;
            statusText = null;
            errorText = null;
        }
    }
}
