using UnityEngine;
using UnityEngine.UI;

namespace TestMultiplayer.UI
{
    public class TestMultiplayerHudPlayerRow : MonoBehaviour
    {
        [SerializeField] private Image profilePicture;
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text pingText;

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
