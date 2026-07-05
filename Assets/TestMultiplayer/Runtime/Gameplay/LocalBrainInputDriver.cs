using TestMultiplayer.Data;
using TestMultiplayer.Networking;
using UnityEngine;

namespace TestMultiplayer.Gameplay
{
    public class LocalBrainInputDriver : MonoBehaviour
    {
        [SerializeField] private float sendInterval = 0.033f;

        private int sequence;
        private float nextSendTime;

        private void Update()
        {
            TestMultiplayerPlayerBrain localBrain = FindLocalBrain();

            if (localBrain == null || Time.unscaledTime < nextSendTime)
            {
                return;
            }

            nextSendTime = Time.unscaledTime + sendInterval;
            localBrain.SubmitInput(ReadInput());
        }

        private TestMultiplayerInputFrame ReadInput()
        {
            sequence++;
            return new TestMultiplayerInputFrame
            {
                Sequence = sequence,
                DeltaTime = Time.deltaTime,
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")),
                PrimaryAction = Input.GetMouseButton(0),
                SecondaryAction = Input.GetMouseButton(1),
                Jump = Input.GetKey(KeyCode.Space)
            };
        }

        private static TestMultiplayerPlayerBrain FindLocalBrain()
        {
            TestMultiplayerSessionManager session = TestMultiplayerSessionManager.Instance;

            if (session == null)
            {
                return null;
            }

            foreach (TestMultiplayerPlayerBrain brain in session.Brains)
            {
                if (brain != null && brain.IsOwner)
                {
                    return brain;
                }
            }

            return null;
        }
    }
}
