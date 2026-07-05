using System.Text;
using TestMultiplayer.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace TestMultiplayer.UI
{
    [System.Obsolete("Fallback legacy. Usa TestMultiplayerUIRoot.prefab con TestMultiplayerMainMenuUI.")]
    public class TestMultiplayerLobbyUI : MonoBehaviour
    {
        [SerializeField] private TestMultiplayerSessionManager sessionManager;

        private Text lobbyText;
        private Button readyButton;
        private Button startButton;

        private void Awake()
        {
            if (FindAnyObjectByType<TestMultiplayerMainMenuUI>() != null)
            {
                gameObject.SetActive(false);
                return;
            }

            ResolveSessionManager();
            Build();
        }

        private void OnEnable()
        {
            ResolveSessionManager();

            if (sessionManager != null)
            {
                sessionManager.StateChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.StateChanged -= Refresh;
            }
        }

        private void Build()
        {
            TestMultiplayerUIFactory.EnsureEventSystem();
            Canvas canvas = TestMultiplayerUIFactory.CreateCanvas("TestMultiplayerLobbyCanvas", transform);
            RectTransform panel = TestMultiplayerUIFactory.CreatePanel("LobbyPanel", canvas.transform);
            TestMultiplayerUIFactory.Text("Title", panel, "Lobby", 34, TextAnchor.MiddleCenter);
            lobbyText = TestMultiplayerUIFactory.Text("Players", panel, string.Empty, 18, TextAnchor.UpperLeft);
            readyButton = TestMultiplayerUIFactory.Button("ReadyButton", panel, "Listo");
            readyButton.onClick.AddListener(ToggleReady);
            startButton = TestMultiplayerUIFactory.Button("StartButton", panel, "Arrancar partida");
            startButton.onClick.AddListener(() => sessionManager?.StartGame());
            TestMultiplayerUIFactory.Button("LeaveButton", panel, "Salir").onClick.AddListener(() => sessionManager?.LeaveSession());
            Refresh();
        }

        private void ToggleReady()
        {
            if (!ResolveSessionManager())
            {
                return;
            }

            TestMultiplayerPlayerBrain localBrain = FindLocalBrain();
            localBrain?.SubmitReady(!localBrain.IsReady);
        }

        private void Refresh()
        {
            ResolveSessionManager();

            if (sessionManager == null || lobbyText == null)
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

                string ready = brain.IsReady ? "Listo" : "Esperando";
                builder.AppendLine($"{brain.PlayerName} [{ready}] {brain.Appearance}");
            }

            if (!string.IsNullOrWhiteSpace(sessionManager.StatusMessage))
            {
                builder.AppendLine();
                builder.AppendLine(sessionManager.StatusMessage);
            }

            lobbyText.text = builder.ToString();

            TestMultiplayerPlayerBrain localBrain = FindLocalBrain();
            readyButton.GetComponentInChildren<Text>().text = localBrain != null && localBrain.IsReady ? "No listo" : "Listo";
            startButton.interactable = sessionManager.IsHost;
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

        private bool ResolveSessionManager()
        {
            if (sessionManager != null)
            {
                return true;
            }

            sessionManager = TestMultiplayerSessionManager.Instance;
            sessionManager = sessionManager != null ? sessionManager : FindAnyObjectByType<TestMultiplayerSessionManager>();
            return sessionManager != null;
        }
    }
}
