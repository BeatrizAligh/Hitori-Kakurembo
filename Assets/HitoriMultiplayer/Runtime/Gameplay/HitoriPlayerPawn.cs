using HitoriKakurembo.Multiplayer.Networking;
using TestMultiplayer.Data;
using TestMultiplayer.Gameplay;
using TestMultiplayer.Networking;
using UnityEngine;

namespace HitoriKakurembo.Multiplayer.Gameplay
{
    /// <summary>
    /// Pawn jugable base para el prototipo multiplayer de Hitori.
    /// Representa el cuerpo fisico que controla un PlayerBrain, separando identidad de jugador y presencia en escena.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HitoriPlayerPawn : NetworkPawn
    {
        [SerializeField] private float moveSpeed = 4.2f;
        [SerializeField] private float runSpeed = 6.2f;
        [SerializeField] private float turnSpeed = 160f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private Renderer targetRenderer;

        private CharacterController characterController;
        private float verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponentInChildren<Renderer>();
        }

        /// <summary>
        /// Aplica input recibido desde el brain propietario.
        /// En esta primera version mantiene el modelo server authoritative heredado del modulo TestMultiplayer.
        /// </summary>
        public override void ApplyInputFromBrain(
            TestMultiplayerPlayerBrain brain,
            TestMultiplayerInputFrame input,
            bool isServerApplication)
        {
            if (!IsControlledBy(brain))
            {
                return;
            }

            characterController = characterController != null ? characterController : GetComponent<CharacterController>();

            if (characterController == null)
            {
                return;
            }

            float deltaTime = Mathf.Clamp(input.DeltaTime, 0f, 0.1f);
            float speed = input.SecondaryAction ? runSpeed : moveSpeed;
            Vector3 planarInput = new Vector3(input.Move.x, 0f, input.Move.y);
            planarInput = Vector3.ClampMagnitude(planarInput, 1f);
            Vector3 planarVelocity = transform.TransformDirection(planarInput) * speed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity += gravity * deltaTime;

            if (input.Jump && characterController.isGrounded)
            {
                verticalVelocity = 5.5f;
            }

            Vector3 motion = planarVelocity;
            motion.y = verticalVelocity;
            characterController.Move(motion * deltaTime);
            transform.Rotate(Vector3.up, input.Look.x * turnSpeed * deltaTime, Space.World);
        }

        /// <summary>
        /// Aplica una apariencia temporal basada en el perfil aprobado.
        /// Luego podemos reemplazarlo por los modelos reales de superviviente/muneco.
        /// </summary>
        public override void ApplyBrainAppearance(TestMultiplayerPlayerBrain brain, CharacterAppearanceData appearance)
        {
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponentInChildren<Renderer>();

            if (targetRenderer == null)
            {
                return;
            }

            Color baseColor = Color.HSVToRGB(
                Mathf.Repeat((appearance.Head * 0.13f) + (appearance.Hair * 0.07f), 1f),
                0.72f,
                0.9f);

            if (brain is HitoriPlayerBrain hitoriBrain && hitoriBrain.IsDoll)
            {
                baseColor = new Color(0.9f, 0.24f, 0.18f, 1f);
            }

            targetRenderer.material.color = baseColor;
        }
    }
}
