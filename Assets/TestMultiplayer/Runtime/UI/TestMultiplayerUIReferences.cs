using UnityEngine;
using UnityEngine.UI;

namespace TestMultiplayer.UI
{
    public class TestMultiplayerUIReferences : MonoBehaviour
    {
        [Header("Windows")]
        public GameObject mainWindow;
        public GameObject sessionWindow;
        public GameObject customizationWindow;
        public GameObject lobbyWindow;
        public GameObject connectedPlayersHud;

        [Header("Main Window")]
        public Text mainProfileText;
        public Button openSessionButton;
        public Button openCustomizationButton;
        public Text mainStatusText;

        [Header("Session Window")]
        public InputField sessionPlayerNameInput;
        public InputField joinCodeInput;
        public Button createLobbyButton;
        public Button joinLobbyButton;
        public Button sessionBackButton;
        public Text sessionStatusText;

        [Header("Customization Window")]
        public InputField customizationPlayerNameInput;
        public InputField headInput;
        public InputField hairInput;
        public InputField upperBodyInput;
        public InputField lowerBodyInput;
        public InputField eyesInput;
        public Button saveCustomizationButton;
        public Button customizationBackButton;

        [Header("Lobby Window")]
        public Text lobbyStateText;
        public Button readyButton;
        public Button startGameButton;
        public Button leaveButton;

        [Header("Connected Players HUD")]
        public Text connectedPlayersTitleText;
        public Transform connectedPlayersRowsRoot;
        public TestMultiplayerHudPlayerRow connectedPlayerRowPrefab;
    }
}
