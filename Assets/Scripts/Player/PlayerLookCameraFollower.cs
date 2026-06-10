using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Mantiene el punto de mirada del jugador local alineado con la camara principal activa.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class PlayerLookCameraFollower : MonoBehaviour
    {
        [SerializeField] private bool copyPosition = true;
        [SerializeField] private bool copyRotation = true;

        private NetworkObject ownerNetworkObject;
        private Transform mainCameraTransform;

        private void Awake()
        {
            ownerNetworkObject = GetComponentInParent<NetworkObject>();
        }

        private void LateUpdate()
        {
            if (!CanFollowLocalCamera())
            {
                return;
            }

            ResolveMainCamera();

            if (mainCameraTransform == null)
            {
                return;
            }

            if (copyPosition && copyRotation)
            {
                transform.SetPositionAndRotation(mainCameraTransform.position, mainCameraTransform.rotation);
                return;
            }

            if (copyPosition)
            {
                transform.position = mainCameraTransform.position;
            }

            if (copyRotation)
            {
                transform.rotation = mainCameraTransform.rotation;
            }
        }

        private bool CanFollowLocalCamera()
        {
            ownerNetworkObject = ownerNetworkObject != null ? ownerNetworkObject : GetComponentInParent<NetworkObject>();
            return ownerNetworkObject != null && ownerNetworkObject.IsSpawned && ownerNetworkObject.IsOwner;
        }

        private void ResolveMainCamera()
        {
            if (mainCameraTransform != null && mainCameraTransform.gameObject.activeInHierarchy)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            mainCameraTransform = mainCamera != null ? mainCamera.transform : null;
        }
    }
}
