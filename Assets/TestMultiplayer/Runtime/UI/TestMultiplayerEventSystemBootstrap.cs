using UnityEngine;

namespace TestMultiplayer.UI
{
    public class TestMultiplayerEventSystemBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            TestMultiplayerUIFactory.EnsurePersistentEventSystem();
        }
    }
}
