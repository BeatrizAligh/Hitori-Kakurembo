using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TestMultiplayer.UI
{
    public class TestMultiplayerHudPlayerRow : MonoBehaviour
    {
        [SerializeField] private Image profilePicture;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text pingText;

        public void SetData(string playerName, string ping)
        {
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }

            if (pingText != null)
            {
                pingText.text = $"Ping: {ping}";
            }
        }
    }
}
