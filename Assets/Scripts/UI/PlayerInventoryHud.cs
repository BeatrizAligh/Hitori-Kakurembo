using HitoriKakurembo.Player;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.UI
{
    public class PlayerInventoryHud : MonoBehaviour
    {
        [SerializeField] private InventoryHandBeltView leftHandView;
        [SerializeField] private InventoryHandBeltView rightHandView;

        private PlayerInventory boundInventory;

        private void Awake()
        {
            ResolveViews();
        }

        private void OnEnable()
        {
            TryBindLocalInventory();
        }

        private void Update()
        {
            if (boundInventory == null)
            {
                TryBindLocalInventory();
            }
        }

        private void TryBindLocalInventory()
        {
            PlayerInventory inventory = FindLocalInventory();

            if (inventory == null || inventory == boundInventory)
            {
                return;
            }

            boundInventory = inventory;
            ResolveViews();
            leftHandView?.Bind(boundInventory, InventoryHand.Left);
            rightHandView?.Bind(boundInventory, InventoryHand.Right);
        }

        private static PlayerInventory FindLocalInventory()
        {
            foreach (PlayerInventory inventory in FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                NetworkObject networkObject = inventory.GetComponent<NetworkObject>();

                if (networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner)
                {
                    return inventory;
                }
            }

            return null;
        }

        private void ResolveViews()
        {
            if (leftHandView == null)
            {
                Transform leftHand = transform.Find("LeftHand");
                leftHandView = leftHand != null ? EnsureBeltView(leftHand.gameObject, InventoryHand.Left) : null;
            }

            if (rightHandView == null)
            {
                Transform rightHand = transform.Find("RightHand");
                rightHandView = rightHand != null ? EnsureBeltView(rightHand.gameObject, InventoryHand.Right) : null;
            }
        }

        private static InventoryHandBeltView EnsureBeltView(GameObject target, InventoryHand hand)
        {
            InventoryHandBeltView view = target.GetComponent<InventoryHandBeltView>();
            view = view != null ? view : target.AddComponent<InventoryHandBeltView>();
            return view;
        }
    }
}
