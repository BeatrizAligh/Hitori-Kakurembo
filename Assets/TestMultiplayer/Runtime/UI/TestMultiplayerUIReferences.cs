using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        public TMP_Text mainProfileText;
        public Button openSessionButton;
        public Button openCustomizationButton;
        public TMP_Text mainStatusText;

        [Header("Session Window")]
        public TMP_InputField sessionPlayerNameInput;
        public TMP_InputField joinCodeInput;
        public Button createLobbyButton;
        public Button joinLobbyButton;
        public Button sessionBackButton;
        public TMP_Text sessionStatusText;

        [Header("Customization Window")]
        public TMP_InputField customizationPlayerNameInput;
        public TMP_InputField headInput;
        public TMP_InputField hairInput;
        public TMP_InputField upperBodyInput;
        public TMP_InputField lowerBodyInput;
        public TMP_InputField eyesInput;
        public Button saveCustomizationButton;
        public Button customizationBackButton;

        [Header("Lobby Window")]
        public TMP_Text lobbyStateText;
        public Button readyButton;
        public Button startGameButton;
        public Button leaveButton;

        [Header("Connected Players HUD")]
        public TMP_Text connectedPlayersTitleText;
        public Transform connectedPlayersRowsRoot;
        public TestMultiplayerHudPlayerRow connectedPlayerRowPrefab;
    }
}
