using System.Text;
using TestMultiplayer.Data;
using TestMultiplayer.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestMultiplayer.UI
{
    public class TestMultiplayerMainMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TestMultiplayerSessionManager sessionManager;

        [Header("Reusable Prefabs")]
        [SerializeField] private Button buttonPrefab;

        private CharacterProfile profile;
        private Canvas canvas;
        private RectTransform mainWindow;
        private RectTransform sessionWindow;
        private RectTransform customizationWindow;
        private RectTransform lobbyWindow;
        private RectTransform connectedPlayersHud;
        private InputField playerNameInput;
        private InputField joinCodeInput;
        private Text statusText;
        private Text sessionStatusText;
        private Text lobbyText;
        private Text hudText;
        private RectTransform hudRowsRoot;
        private Button readyButton;
        private Button startButton;

        private void Awake()
        {
            profile = CharacterProfileStore.Load();
            ResolveSessionManager();
            Build();
        }

        private void OnEnable()
        {
            ResolveSessionManager();
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (sessionManager != null)
            {
                sessionManager.StateChanged += RefreshAll;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (sessionManager != null)
            {
                sessionManager.StateChanged -= RefreshAll;
            }
        }

        private void Build()
        {
            TestMultiplayerUIFactory.EnsureEventSystem();
            canvas = TestMultiplayerUIFactory.CreateCanvas("TestMultiplayerUICanvas", transform);

            mainWindow = TestMultiplayerUIFactory.CreatePanel("MainWindow", canvas.transform);
            sessionWindow = TestMultiplayerUIFactory.CreatePanel("SessionWindow", canvas.transform);
            customizationWindow = TestMultiplayerUIFactory.CreatePanel("CustomizationWindow", canvas.transform);
            lobbyWindow = TestMultiplayerUIFactory.CreatePanel("LobbyWindow", canvas.transform);
            connectedPlayersHud = CreateHudPanel("ConnectedPlayersHud", canvas.transform);

            BuildMainWindow();
            BuildSessionWindow();
            BuildCustomizationWindow();
            BuildLobbyWindow();
            BuildHud();
            ShowMain();
        }

        private void BuildMainWindow()
        {
            Clear(mainWindow);
            TestMultiplayerUIFactory.Text("Title", mainWindow, "Test Multiplayer", 34, TextAnchor.MiddleCenter);
            TestMultiplayerUIFactory.Text("Profile", mainWindow, $"Perfil: {profile.PlayerName} | {profile.Appearance}", 18, TextAnchor.MiddleCenter);
            CreateButton("StartButton", mainWindow, "Iniciar juego").onClick.AddListener(ShowSessionOptions);
            CreateButton("CustomizeButton", mainWindow, "Personalizar personaje").onClick.AddListener(ShowCustomization);
            statusText = TestMultiplayerUIFactory.Text("Status", mainWindow, string.Empty, 16, TextAnchor.MiddleCenter);
        }

        private void BuildSessionWindow()
        {
            Clear(sessionWindow);
            TestMultiplayerUIFactory.Text("Title", sessionWindow, "Sala multiplayer", 30, TextAnchor.MiddleCenter);
            playerNameInput = TestMultiplayerUIFactory.Input("PlayerNameInput", sessionWindow, "Nombre", profile.PlayerName);
            joinCodeInput = TestMultiplayerUIFactory.Input("JoinCodeInput", sessionWindow, "Codigo de acceso", string.Empty);
            CreateButton("CreateLobbyButton", sessionWindow, "Crear partida").onClick.AddListener(CreateLobby);
            CreateButton("JoinLobbyButton", sessionWindow, "Unirse a partida").onClick.AddListener(JoinLobby);
            CreateButton("BackButton", sessionWindow, "Volver").onClick.AddListener(ShowMain);
            sessionStatusText = TestMultiplayerUIFactory.Text("Status", sessionWindow, string.Empty, 16, TextAnchor.MiddleCenter);
        }

        private void BuildCustomizationWindow()
        {
            Clear(customizationWindow);
            TestMultiplayerUIFactory.Text("Title", customizationWindow, "Personaje", 30, TextAnchor.MiddleCenter);
            playerNameInput = TestMultiplayerUIFactory.Input("CharacterNameInput", customizationWindow, "Nombre", profile.PlayerName);
            AddPartInput("Cabeza", profile.Appearance.Head, value => profile.Appearance.Head = value);
            AddPartInput("Cabello", profile.Appearance.Hair, value => profile.Appearance.Hair = value);
            AddPartInput("Parte superior", profile.Appearance.UpperBody, value => profile.Appearance.UpperBody = value);
            AddPartInput("Parte inferior", profile.Appearance.LowerBody, value => profile.Appearance.LowerBody = value);
            AddPartInput("Ojos", profile.Appearance.Eyes, value => profile.Appearance.Eyes = value);
            CreateButton("SaveButton", customizationWindow, "Guardar").onClick.AddListener(SaveCustomization);
            CreateButton("BackButton", customizationWindow, "Volver").onClick.AddListener(ShowMain);
        }

        private void BuildLobbyWindow()
        {
            Clear(lobbyWindow);
            TestMultiplayerUIFactory.Text("Title", lobbyWindow, "Lobby", 30, TextAnchor.MiddleCenter);
            lobbyText = TestMultiplayerUIFactory.Text("LobbyState", lobbyWindow, string.Empty, 18, TextAnchor.UpperLeft);
            readyButton = CreateButton("ReadyButton", lobbyWindow, "Listo");
            readyButton.onClick.AddListener(ToggleReady);
            startButton = CreateButton("StartButton", lobbyWindow, "Arrancar partida");
            startButton.onClick.AddListener(() => sessionManager?.StartGame());
            CreateButton("LeaveButton", lobbyWindow, "Salir").onClick.AddListener(() => sessionManager?.LeaveSession());
        }

        private void BuildHud()
        {
            Clear(connectedPlayersHud);
            hudText = TestMultiplayerUIFactory.Text("ConnectedPlayersTitle", connectedPlayersHud, "Jugadores conectados: 0", 16, TextAnchor.UpperLeft);

            GameObject rowsObject = new GameObject("PlayerRows", typeof(VerticalLayoutGroup));
            rowsObject.transform.SetParent(connectedPlayersHud, false);
            hudRowsRoot = rowsObject.GetComponent<RectTransform>();

            VerticalLayoutGroup rowsLayout = rowsObject.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 8f;
            rowsLayout.childControlHeight = true;
            rowsLayout.childControlWidth = true;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childForceExpandWidth = true;
        }

        private RectTransform CreateHudPanel(string name, Transform parent)
        {
            GameObject panelObject = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup));
            panelObject.transform.SetParent(parent, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(360f, 220f);

            panelObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.82f);

            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private Button CreateButton(string name, Transform parent, string label)
        {
            return TestMultiplayerUIFactory.ButtonFromPrefab(buttonPrefab, name, parent, label);
        }

        private void ShowMain()
        {
            SaveNameFromInput();
            SetWindow(mainWindow);
        }

        private void ShowSessionOptions()
        {
            BuildSessionWindow();
            SetWindow(sessionWindow);
            RefreshAll();
        }

        private void ShowCustomization()
        {
            BuildCustomizationWindow();
            SetWindow(customizationWindow);
        }

        private void ShowLobby()
        {
            SetWindow(lobbyWindow);
            RefreshAll();
        }

        private void HideCreationFlow()
        {
            mainWindow.gameObject.SetActive(false);
            sessionWindow.gameObject.SetActive(false);
            customizationWindow.gameObject.SetActive(false);
            lobbyWindow.gameObject.SetActive(false);
            connectedPlayersHud.gameObject.SetActive(ResolveSessionManager() && sessionManager.IsInSession);
        }

        private void SetWindow(RectTransform activeWindow)
        {
            bool inSession = ResolveSessionManager() && sessionManager.IsInSession;
            mainWindow.gameObject.SetActive(activeWindow == mainWindow && !inSession);
            sessionWindow.gameObject.SetActive(activeWindow == sessionWindow && !inSession);
            customizationWindow.gameObject.SetActive(activeWindow == customizationWindow && !inSession);
            lobbyWindow.gameObject.SetActive(activeWindow == lobbyWindow && inSession);
            connectedPlayersHud.gameObject.SetActive(inSession);
        }

        private void AddPartInput(string label, int currentValue, System.Action<int> apply)
        {
            InputField input = TestMultiplayerUIFactory.Input($"{label}Input", customizationWindow, label, currentValue.ToString());
            input.contentType = InputField.ContentType.IntegerNumber;
            input.onEndEdit.AddListener(value =>
            {
                if (int.TryParse(value, out int parsed))
                {
                    apply(Mathf.Max(0, parsed));
                }
            });
        }

        private async void CreateLobby()
        {
            if (!ResolveSessionManager())
            {
                ShowLocalStatus("No se encontro TestMultiplayerSessionManager en la escena.");
                return;
            }

            SaveNameFromInput();
            CharacterProfileStore.Save(profile);

            bool created = await sessionManager.CreateLobbyAsync(profile);

            if (created)
            {
                ShowLobby();
            }
        }

        private async void JoinLobby()
        {
            if (!ResolveSessionManager())
            {
                ShowLocalStatus("No se encontro TestMultiplayerSessionManager en la escena.");
                return;
            }

            SaveNameFromInput();
            CharacterProfileStore.Save(profile);

            bool joined = await sessionManager.JoinLobbyAsync(profile, joinCodeInput != null ? joinCodeInput.text : string.Empty);

            if (joined)
            {
                ShowLobby();
            }
        }

        private void ToggleReady()
        {
            TestMultiplayerPlayerBrain localBrain = FindLocalBrain();
            localBrain?.SubmitReady(!localBrain.IsReady);
        }

        private void SaveCustomization()
        {
            SaveNameFromInput();
            CharacterProfileStore.Save(profile);
            BuildMainWindow();
            ShowMain();
        }

        private void SaveNameFromInput()
        {
            if (playerNameInput != null)
            {
                profile.PlayerName = playerNameInput.text;
            }

            profile = CharacterProfileStore.Sanitize(profile);
        }

        private void RefreshAll()
        {
            ResolveSessionManager();
            RefreshStatus();
            RefreshLobby();
            RefreshHud();

            if (sessionManager == null)
            {
                return;
            }

            if (!sessionManager.IsInSession)
            {
                connectedPlayersHud.gameObject.SetActive(false);

                if (lobbyWindow.gameObject.activeSelf)
                {
                    ShowMain();
                }

                return;
            }

            connectedPlayersHud.gameObject.SetActive(true);

            if (SceneManager.GetActiveScene().name == "TestMultiplayerGame")
            {
                HideCreationFlow();
            }
            else if (!lobbyWindow.gameObject.activeSelf)
            {
                ShowLobby();
            }
        }

        private void RefreshStatus()
        {
            if (statusText == null || sessionManager == null)
            {
                return;
            }

            statusText.text = string.IsNullOrWhiteSpace(sessionManager.ErrorMessage)
                ? sessionManager.StatusMessage
                : sessionManager.ErrorMessage;

            if (sessionStatusText != null)
            {
                sessionStatusText.text = statusText.text;
            }
        }

        private void RefreshLobby()
        {
            if (lobbyText == null || sessionManager == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(string.IsNullOrWhiteSpace(sessionManager.JoinCode) ? "Codigo: -" : $"Codigo: {sessionManager.JoinCode}");
            builder.AppendLine();

            foreach (TestMultiplayerPlayerBrain brain in sessionManager.Brains)
            {
                if (brain == null)
                {
                    continue;
                }

                builder.AppendLine($"{brain.PlayerName} [{(brain.IsReady ? "Listo" : "Esperando")}]");
            }

            if (!string.IsNullOrWhiteSpace(sessionManager.StatusMessage))
            {
                builder.AppendLine();
                builder.AppendLine(sessionManager.StatusMessage);
            }

            lobbyText.text = builder.ToString();

            TestMultiplayerPlayerBrain localBrain = FindLocalBrain();
            TestMultiplayerUIFactory.SetButtonLabel(readyButton, localBrain != null && localBrain.IsReady ? "No listo" : "Listo");
            startButton.interactable = sessionManager.IsHost;
        }

        private void RefreshHud()
        {
            if (hudText == null || sessionManager == null)
            {
                return;
            }

            Clear(hudRowsRoot);
            hudText.text = $"Jugadores conectados: {sessionManager.Brains.Count}";

            foreach (TestMultiplayerPlayerBrain brain in sessionManager.Brains)
            {
                if (brain == null)
                {
                    continue;
                }

                string localMarker = brain.IsOwner ? " (tu)" : string.Empty;
                CreateHudEntry($"{brain.PlayerName}{localMarker}", GetDisplayPingText(brain), hudRowsRoot);
            }
        }

        private static void CreateHudEntry(string playerName, string pingText, Transform parent)
        {
            GameObject rowObject = new GameObject($"PlayerRow_{playerName}", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);
            rowObject.GetComponent<LayoutElement>().minHeight = 48f;

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;

            GameObject pictureObject = new GameObject("ProfilePicture", typeof(Image), typeof(LayoutElement));
            pictureObject.transform.SetParent(rowObject.transform, false);
            pictureObject.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.26f, 1f);
            LayoutElement pictureLayout = pictureObject.GetComponent<LayoutElement>();
            pictureLayout.minWidth = 42f;
            pictureLayout.minHeight = 42f;
            pictureLayout.preferredWidth = 42f;
            pictureLayout.preferredHeight = 42f;

            Text playerText = TestMultiplayerUIFactory.Text("NameAndPing", rowObject.transform, $"{playerName}\nPing: {pingText}", 14, TextAnchor.MiddleLeft);
            LayoutElement textLayout = playerText.GetComponent<LayoutElement>();
            textLayout.minWidth = 240f;
            textLayout.minHeight = 42f;
        }

        private static string GetDisplayPingText(TestMultiplayerPlayerBrain brain)
        {
            return brain != null && brain.IsOwner ? "0 ms" : "--";
        }

        private TestMultiplayerPlayerBrain FindLocalBrain()
        {
            if (!ResolveSessionManager())
            {
                return null;
            }

            foreach (TestMultiplayerPlayerBrain brain in sessionManager.Brains)
            {
                if (brain != null && brain.IsOwner)
                {
                    return brain;
                }
            }

            return null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshAll();
        }

        private bool ResolveSessionManager()
        {
            if (sessionManager != null)
            {
                return true;
            }

            sessionManager = TestMultiplayerSessionManager.Instance;
            sessionManager = sessionManager != null ? sessionManager : GetComponent<TestMultiplayerSessionManager>();
            sessionManager = sessionManager != null ? sessionManager : FindAnyObjectByType<TestMultiplayerSessionManager>();
            return sessionManager != null;
        }

        private void ShowLocalStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }

            Debug.LogWarning(message);
        }

        private static void Clear(Transform target)
        {
            if (target == null)
            {
                return;
            }

            for (int i = target.childCount - 1; i >= 0; i--)
            {
                Destroy(target.GetChild(i).gameObject);
            }
        }
    }
}
