using System;
using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Items;
using HitoriKakurembo.Roles;
using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HitoriKakurembo.Player
{
    public enum InventoryHand
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// Infinite player inventory with two active hand selections.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public class InventoryEntry
        {
            [SerializeField] private InventoryItemDefinition item;
            [SerializeField] private int quantity = 1;
            [SerializeField] private bool roleBound;

            public InventoryItemDefinition Item => item;
            public int Quantity => quantity;
            public bool RoleBound => roleBound;
            public bool CanDrop => item != null && item.Droppable && !roleBound;

            public InventoryEntry(InventoryItemDefinition item, int quantity, bool roleBound)
            {
                this.item = item;
                this.quantity = Mathf.Max(1, quantity);
                this.roleBound = roleBound;
            }

            public void AddQuantity(int amount)
            {
                quantity = Mathf.Max(1, quantity + amount);
            }
        }

        private const string RoleInventoryDatabasePath = "Inventory/RoleInventoryDatabase";

        [Header("Definitions")]
        [SerializeField] private RoleInventoryDatabase roleInventoryDatabase;
        [SerializeField] private Sprite defaultItemSprite;

        [Header("Flashlight")]
        [SerializeField] private Light flashlightLight;
        [SerializeField] private string flashlightObjectName = "Spot Light";

        private readonly List<InventoryEntry> entries = new List<InventoryEntry>();
        private NetworkObject networkObject;
        private PlayerRoleType appliedRole = PlayerRoleType.None;
        private int leftHandIndex = -1;
        private int rightHandIndex = -1;

        public event Action InventoryChanged;
        public event Action SelectionChanged;

        public IReadOnlyList<InventoryEntry> Entries => entries;
        public Sprite DefaultItemSprite => defaultItemSprite;
        public InventoryEntry LeftHandEntry => GetEntryAtIndex(leftHandIndex);
        public InventoryEntry RightHandEntry => GetEntryAtIndex(rightHandIndex);

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            roleInventoryDatabase = roleInventoryDatabase != null
                ? roleInventoryDatabase
                : Resources.Load<RoleInventoryDatabase>(RoleInventoryDatabasePath);
            ResolveFlashlightLight();
            RefreshEquippedItemEffects();
        }

        private void Update()
        {
            if (!IsLocalOwner())
            {
                return;
            }

            if (WasLeftUpPressedThisFrame())
            {
                CycleHand(InventoryHand.Left, -1);
            }

            if (WasLeftDownPressedThisFrame())
            {
                CycleHand(InventoryHand.Left, 1);
            }

            if (WasRightUpPressedThisFrame())
            {
                CycleHand(InventoryHand.Right, -1);
            }

            if (WasRightDownPressedThisFrame())
            {
                CycleHand(InventoryHand.Right, 1);
            }
        }

        public void ApplyDefaultInventoryForRole(PlayerRoleType roleType)
        {
            if (appliedRole == roleType)
            {
                return;
            }

            appliedRole = roleType;
            entries.RemoveAll(entry => entry != null && entry.RoleBound);

            RoleInventoryDefinition roleInventory = roleInventoryDatabase != null
                ? roleInventoryDatabase.GetInventoryForRole(roleType)
                : null;

            if (roleInventory != null)
            {
                foreach (RoleInventoryDefinition.InventoryGrant grant in roleInventory.StartingItems)
                {
                    if (grant?.Item != null)
                    {
                        AddItem(grant.Item, grant.Quantity, grant.RoleBound);
                    }
                }
            }

            NormalizeSelections();
            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke();
            RefreshEquippedItemEffects();
        }

        public bool AddItem(InventoryItemDefinition item, int quantity = 1, bool roleBound = false)
        {
            if (item == null)
            {
                return false;
            }

            InventoryEntry existingStack = entries.FirstOrDefault(entry =>
                entry != null
                && entry.Item == item
                && entry.RoleBound == roleBound
                && item.Consumable);

            if (existingStack != null)
            {
                existingStack.AddQuantity(quantity);
            }
            else
            {
                entries.Add(new InventoryEntry(item, quantity, roleBound));
            }

            NormalizeSelections();
            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke();
            RefreshEquippedItemEffects();
            return true;
        }

        public bool AddItem(ItemBase item)
        {
            return item != null && HasItemType(item.Type);
        }

        public bool RemoveEntry(InventoryEntry entry)
        {
            if (entry == null || entry.RoleBound || !entries.Remove(entry))
            {
                return false;
            }

            NormalizeSelections();
            InventoryChanged?.Invoke();
            SelectionChanged?.Invoke();
            RefreshEquippedItemEffects();
            return true;
        }

        public bool DropSelected(InventoryHand hand)
        {
            InventoryEntry selected = GetSelectedEntry(hand);
            return selected != null && selected.CanDrop && RemoveEntry(selected);
        }

        public bool HasItemType(ItemType itemType)
        {
            return entries.Any(entry => entry?.Item != null && entry.Item.ItemType == itemType);
        }

        public bool HasActiveItemType(ItemType itemType)
        {
            return IsActiveEntryOfType(LeftHandEntry, itemType) || IsActiveEntryOfType(RightHandEntry, itemType);
        }

        public void CycleHand(InventoryHand hand, int direction)
        {
            if (entries.Count == 0)
            {
                SetHandIndex(hand, -1);
                return;
            }

            int currentIndex = GetHandIndex(hand);
            int nextIndex = currentIndex < 0 ? 0 : currentIndex;
            int step = direction < 0 ? -1 : 1;

            for (int attempt = 0; attempt < entries.Count; attempt++)
            {
                nextIndex = Mod(nextIndex + step, entries.Count);

                if (!IsEntrySelectedByOtherHand(nextIndex, hand))
                {
                    SetHandIndex(hand, nextIndex);
                    return;
                }
            }
        }

        public InventoryEntry GetSelectedEntry(InventoryHand hand)
        {
            return GetEntryAtIndex(GetHandIndex(hand));
        }

        public InventoryEntry GetEntryAtOffset(InventoryHand hand, int offset)
        {
            if (entries.Count == 0)
            {
                return null;
            }

            int selectedIndex = GetHandIndex(hand);
            selectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
            return GetEntryAtIndex(Mod(selectedIndex + offset, entries.Count));
        }

        public bool IsEntrySelectedByOtherHand(InventoryEntry entry, InventoryHand hand)
        {
            if (entry == null)
            {
                return false;
            }

            int index = entries.IndexOf(entry);
            return index >= 0 && IsEntrySelectedByOtherHand(index, hand);
        }

        private void SetHandIndex(InventoryHand hand, int index)
        {
            int normalizedIndex = entries.Count == 0 ? -1 : Mathf.Clamp(index, 0, entries.Count - 1);

            if (hand == InventoryHand.Left)
            {
                leftHandIndex = normalizedIndex;
            }
            else
            {
                rightHandIndex = normalizedIndex;
            }

            NormalizeSelections();
            SelectionChanged?.Invoke();
            RefreshEquippedItemEffects();
        }

        private void NormalizeSelections()
        {
            if (entries.Count == 0)
            {
                leftHandIndex = -1;
                rightHandIndex = -1;
                return;
            }

            leftHandIndex = leftHandIndex < 0 ? 0 : Mathf.Clamp(leftHandIndex, 0, entries.Count - 1);
            rightHandIndex = rightHandIndex < 0 ? FindFirstIndexNotUsedBy(InventoryHand.Right) : Mathf.Clamp(rightHandIndex, 0, entries.Count - 1);

            if (leftHandIndex == rightHandIndex)
            {
                rightHandIndex = FindFirstIndexNotUsedBy(InventoryHand.Right);
            }
        }

        private int FindFirstIndexNotUsedBy(InventoryHand hand)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (!IsEntrySelectedByOtherHand(index, hand))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool IsEntrySelectedByOtherHand(int index, InventoryHand hand)
        {
            return hand == InventoryHand.Left ? rightHandIndex == index : leftHandIndex == index;
        }

        private int GetHandIndex(InventoryHand hand)
        {
            return hand == InventoryHand.Left ? leftHandIndex : rightHandIndex;
        }

        private InventoryEntry GetEntryAtIndex(int index)
        {
            return index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private static int Mod(int value, int count)
        {
            return count <= 0 ? 0 : (value % count + count) % count;
        }

        private static bool IsActiveEntryOfType(InventoryEntry entry, ItemType itemType)
        {
            return entry?.Item != null && entry.Item.ItemType == itemType;
        }

        private bool IsLocalOwner()
        {
            networkObject = networkObject != null ? networkObject : GetComponent<NetworkObject>();
            return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
        }

        private void RefreshEquippedItemEffects()
        {
            ResolveFlashlightLight();

            if (flashlightLight != null)
            {
                flashlightLight.enabled = HasActiveItemType(ItemType.Flashlight);
            }
        }

        private void ResolveFlashlightLight()
        {
            if (flashlightLight != null)
            {
                return;
            }

            foreach (Light lightComponent in GetComponentsInChildren<Light>(true))
            {
                if (lightComponent != null && lightComponent.name == flashlightObjectName)
                {
                    flashlightLight = lightComponent;
                    return;
                }
            }
        }

        private static bool WasLeftUpPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.qKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Q);
#else
            return false;
#endif
        }

        private static bool WasLeftDownPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.zKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Z);
#else
            return false;
#endif
        }

        private static bool WasRightUpPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private static bool WasRightDownPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.cKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.C);
#else
            return false;
#endif
        }
    }
}
