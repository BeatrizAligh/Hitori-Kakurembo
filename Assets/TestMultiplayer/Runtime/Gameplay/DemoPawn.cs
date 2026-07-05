using TestMultiplayer.Data;
using TestMultiplayer.Networking;
using Unity.Netcode;
using UnityEngine;

namespace TestMultiplayer.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public class DemoPawn : NetworkPawn
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float turnSpeed = 160f;
        [SerializeField] private Renderer targetRenderer;

        private void Awake()
        {
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponentInChildren<Renderer>();
        }

        public override void ApplyInputFromBrain(
            TestMultiplayerPlayerBrain brain,
            TestMultiplayerInputFrame input,
            bool isServerApplication)
        {
            if (!IsControlledBy(brain))
            {
                return;
            }

            float deltaTime = Mathf.Clamp(input.DeltaTime, 0f, 0.1f);
            Vector3 move = new Vector3(input.Move.x, 0f, input.Move.y);
            move = Vector3.ClampMagnitude(move, 1f);

            transform.position += transform.TransformDirection(move) * moveSpeed * deltaTime;
            transform.Rotate(Vector3.up, input.Look.x * turnSpeed * deltaTime, Space.World);

            if (input.Jump)
            {
                transform.position += Vector3.up * 0.02f;
            }
        }

        public override void ApplyBrainAppearance(TestMultiplayerPlayerBrain brain, CharacterAppearanceData appearance)
        {
            if (targetRenderer == null)
            {
                return;
            }

            float hue = Mathf.Repeat((appearance.Head * 0.13f) + (appearance.Hair * 0.07f) + (appearance.Eyes * 0.17f), 1f);
            float saturation = Mathf.Lerp(0.45f, 0.85f, Mathf.Repeat(appearance.UpperBody * 0.19f, 1f));
            float value = Mathf.Lerp(0.55f, 0.95f, Mathf.Repeat(appearance.LowerBody * 0.23f, 1f));
            targetRenderer.material.color = Color.HSVToRGB(hue, saturation, value);
        }
    }
}
