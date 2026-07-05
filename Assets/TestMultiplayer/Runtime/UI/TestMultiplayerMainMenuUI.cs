using System.Text;
using TestMultiplayer.Data;
using TestMultiplayer.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestMultiplayer.UI
{
    public class TestMultiplayerMainMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TestMultiplayerSessionManager sessionManager;

        [Header("UI Prefab")]
        [SerializeField] private TestMultiplayerUIReferences uiRootPrefab;

        private CharacterProfile profile;
        private TestMultiplayerUIReferences ui;

        private void Awake()
        {
            profile = CharacterProfileStore.Load();
            ResolveSessionManager();
            TestMultiplayerUIFactory.EnsurePersistentEventSystem();
            SpawnUiPrefab();
            WireUi();
            ShowMain();
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

        private void SpawnUiPrefab()
        {
            if (ui != null)
            {
                return;
            }

            if (uiRootPrefab == null)
            {
                Debug.LogError("TestMultiplayerMainMenuUI necesita un prefab TestMultiplayerUIReferences asignado.");
                return;
            }

            ui = Instantiate(uiRootPrefab, transform);
            ui.name = uiRootPrefab.name;
        }

        private void WireUi()
        {
            if (ui == null)
            {
                return;
            }

            ui.openSessionButton?.onClick.AddListener(ShowSessionOptions);
            ui.openCustomizationButton?.onClick.AddListener(ShowCustomization);
            ui.createLobbyButton?.onClick.AddListener(CreateLobby);
            ui.joinLobbyButton?.onClick.AddListener(JoinLobby);
            ui.sessionBackButton?.onClick.AddListener(ShowMain);
            ui.saveCustomizationButton?.onClick.AddListener(SaveCustomization);
            ui.customizationBackButton?.onClick.AddListener(ShowMain);
            ui.readyButton?.onClick.AddListener(ToggleReady);
            ui.startGameButton?.onClick.AddListener(() => sessionManager?.StartGame());
            ui.leaveButton?.onClick.AddListener(() => sessionManager?.LeaveSession());
        }

        private void ShowMain()
        {
            SaveNameFromInputs();
            RefreshProfileFields();
            SetWindow(ui != null ? ui.mainWindow : null);
        }

        private void ShowSessionOptions()
        {
            RefreshSessionFields();
            SetWindow(ui != null ? ui.sessionWindow : null);
            RefreshAll();
        }

        private void ShowCustomization()
        {
            RefreshCustomizationFields();
            SetWindow(ui != null ? ui.customizationWindow : null);
        }

        private void ShowLobby()
        {
            SetWindow(ui != null ? ui.lobbyWindow : null);
            RefreshAll();
        }

        private void HideCreationFlow()
        {
            if (ui == null)
            {
                return;
            }

            SetActive(ui.mainWindow, false);
            SetActive(ui.sessionWindow, false);
            SetActive(ui.customizationWindow, false);
            SetActive(ui.lobbyWindow, false);
            SetActive(ui.connectedPlayersHud, ResolveSessionManager() && sessionManager.IsInSession);
        }

        private void SetWindow(GameObject activeWindow)
        {
            if (ui == null)
            {
                return;
            }

            bool inSession = ResolveSessionManager() && sessionManager.IsInSession;
            SetActive(ui.mainWindow, activeWindow == ui.mainWindow && !inSession);
            SetActive(ui.sessionWindow, activeWindow == ui.sessionWindow && !inSession);
            SetActive(ui.customizationWindow, activeWindow == ui.customizationWindow && !inSession);
            SetActive(ui.lobbyWindow, activeWindow == ui.lobbyWindow && inSession);
            SetActive(ui.connectedPlayersHud, inSession);
        }

        private async void CreateLobby()
        {
            if (!ResolveSessionManager())
            {
                ShowLocalStatus("No se encontro TestMultiplayerSessionManager en la escena.");
                return;
            }

            SaveNameFromInputs();
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

            SaveNameFromInputs();
            CharacterProfileStore.Save(profile);

            string joinCode = ui != null && ui.joinCodeInput != null ? ui.joinCodeInput.text : string.Empty;
            bool joined = await sessionManager.JoinLobbyAsync(profile, joinCode);

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
            SaveNameFromInputs();
            CharacterProfileStore.Save(profile);
            ShowMain();
        }

        private void SaveNameFromInputs()
        {
            if (ui == null)
            {
                return;
            }

            string playerName = ui.sessionPlayerNameInput != null && !string.IsNullOrWhiteSpace(ui.sessionPlayerNameInput.text)
                ? ui.sessionPlayerNameInput.text
                : ui.customizationPlayerNameInput != null
                    ? ui.customizationPlayerNameInput.text
                    : profile.PlayerName;

            profile.PlayerName = playerName;
            profile.Appearance.Head = ReadInt(ui.headInput, profile.Appearance.Head);
            profile.Appearance.Hair = ReadInt(ui.hairInput, profile.Appearance.Hair);
            profile.Appearance.UpperBody = ReadInt(ui.upperBodyInput, profile.Appearance.UpperBody);
            profile.Appearance.LowerBody = ReadInt(ui.lowerBodyInput, profile.Appearance.LowerBody);
            profile.Appearance.Eyes = ReadInt(ui.eyesInput, profile.Appearance.Eyes);
            profile = CharacterProfileStore.Sanitize(profile);
        }

        private void RefreshAll()
        {
            ResolveSessionManager();
            TestMultiplayerUIFactory.EnsurePersistentEventSystem();
            RefreshStatus();
            RefreshLobby();
            RefreshHud();

            if (ui == null || sessionManager == null)
            {
                return;
            }

            if (!sessionManager.IsInSession)
            {
                SetActive(ui.connectedPlayersHud, false);

                if (ui.lobbyWindow != null && ui.lobbyWindow.activeSelf)
                {
                    ShowMain();
                }

                return;
            }

            SetActive(ui.connectedPlayersHud, true);

            if (SceneManager.GetActiveScene().name == "TestMultiplayerGame")
            {
                HideCreationFlow();
            }
            else if (ui.lobbyWindow != null && !ui.lobbyWindow.activeSelf)
            {
                ShowLobby();
            }
        }

        private void RefreshProfileFields()
        {
            if (ui == null)
            {
                return;
            }

            if (ui.mainProfileText != null)
            {
                ui.mainProfileText.text = $"Perfil: {profile.PlayerName} | {profile.Appearance}";
            }
        }

        private void RefreshSessionFields()
        {
            if (ui == null)
            {
                return;
            }

            if (ui.sessionPlayerNameInput != null)
            {
                ui.sessionPlayerNameInput.text = profile.PlayerName;
            }
        }

        private void RefreshCustomizationFields()
        {
            if (ui == null)
            {
                return;
            }

            SetInput(ui.customizationPlayerNameInput, profile.PlayerName);
            SetInput(ui.headInput, profile.Appearance.Head.ToString());
            SetInput(ui.hairInput, profile.Appearance.Hair.ToString());
            SetInput(ui.upperBodyInput, profile.Appearance.UpperBody.ToString());
            SetInput(ui.lowerBodyInput, profile.Appearance.LowerBody.ToString());
            SetInput(ui.eyesInput, profile.Appearance.Eyes.ToString());
        }

        private void RefreshStatus()
        {
            if (ui == null || sessionManager == null)
            {
                return;
            }

            string status = string.IsNullOrWhiteSpace(sessionManager.ErrorMessage)
                ? sessionManager.StatusMessage
                : sessionManager.ErrorMessage;

            if (ui.mainStatusText != null)
            {
                ui.mainStatusText.text = status;
            }

            if (ui.sessionStatusText != null)
            {
                ui.sessionStatusText.text = status;
            }
        }

        private void RefreshLobby()
        {
            if (ui == null || ui.lobbyStateText == null || sessionManager == null)
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

            ui.lobbyStateText.text = builder.ToString();

            TestMultiplayerPlayerBrain localBrain = FindLocalBrain();
            TestMultiplayerUIFactory.SetButtonLabel(ui.readyButton, localBrain != null && localBrain.IsReady ? "No listo" : "Listo");

            if (ui.startGameButton != null)
            {
                ui.startGameButton.interactable = sessionManager.IsHost;
            }
        }

        private void RefreshHud()
        {
            if (ui == null || ui.connectedPlayersTitleText == null || sessionManager == null)
            {
                return;
            }

            Clear(ui.connectedPlayersRowsRoot);
            ui.connectedPlayersTitleText.text = $"Jugadores conectados: {sessionManager.Brains.Count}";

            foreach (TestMultiplayerPlayerBrain brain in sessionManager.Brains)
            {
                if (brain == null || ui.connectedPlayerRowPrefab == null || ui.connectedPlayersRowsRoot == null)
                {
                    continue;
                }

                string localMarker = brain.IsOwner ? " (tu)" : string.Empty;
                TestMultiplayerHudPlayerRow row = Instantiate(ui.connectedPlayerRowPrefab, ui.connectedPlayersRowsRoot, false);
                row.name = $"PlayerRow_{brain.PlayerName}";
                row.SetData($"{brain.PlayerName}{localMarker}", GetDisplayPingText(brain));
            }
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
            TestMultiplayerUIFactory.EnsurePersistentEventSystem();
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
            if (ui != null && ui.mainStatusText != null)
            {
                ui.mainStatusText.text = message;
            }

            if (ui != null && ui.sessionStatusText != null)
            {
                ui.sessionStatusText.text = message;
            }

            Debug.LogWarning(message);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetInput(TMP_InputField input, string value)
        {
            if (input != null)
            {
                input.text = value;
            }
        }

        private static int ReadInt(TMP_InputField input, int fallback)
        {
            return input != null && int.TryParse(input.text, out int parsed)
                ? Mathf.Max(0, parsed)
                : fallback;
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
