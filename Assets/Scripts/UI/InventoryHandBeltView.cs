using System.Collections.Generic;
using HitoriKakurembo.Player;
using UnityEngine;

namespace HitoriKakurembo.UI
{
    public class InventoryHandBeltView : MonoBehaviour
    {
        [SerializeField] private InventoryHand hand = InventoryHand.Right;
        [SerializeField] private List<InventorySlotView> slots = new List<InventorySlotView>();

        private PlayerInventory inventory;

        private void Awake()
        {
            ResolveSlots();
        }

        public void Bind(PlayerInventory playerInventory, InventoryHand inventoryHand)
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= Refresh;
                inventory.SelectionChanged -= Refresh;
            }

            inventory = playerInventory;
            hand = inventoryHand;

            if (inventory != null)
            {
                inventory.InventoryChanged += Refresh;
                inventory.SelectionChanged += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= Refresh;
                inventory.SelectionChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            ResolveSlots();

            if (inventory == null)
            {
                foreach (InventorySlotView slot in slots)
                {
                    slot.Draw(null, false, false, null);
                }

                return;
            }

            int centerIndex = slots.Count / 2;

            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                int offset = slotIndex - centerIndex;
                PlayerInventory.InventoryEntry entry = inventory.GetEntryAtOffset(hand, offset);
                bool selected = offset == 0 && entry != null && inventory.GetSelectedEntry(hand) == entry;
                bool unavailable = inventory.IsEntrySelectedByOtherHand(entry, hand);
                slots[slotIndex].Draw(entry, selected, unavailable, inventory.DefaultItemSprite);
            }
        }

        private void ResolveSlots()
        {
            if (slots.Count > 0)
            {
                return;
            }

            slots.AddRange(GetComponentsInChildren<InventorySlotView>(true));

            if (slots.Count > 0)
            {
                return;
            }

            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Slot"))
                {
                    continue;
                }

                InventorySlotView slot = child.GetComponent<InventorySlotView>();
                slot = slot != null ? slot : child.gameObject.AddComponent<InventorySlotView>();
                slots.Add(slot);
            }
        }
    }
}
