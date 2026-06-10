using UnityEngine;

namespace HitoriKakurembo.Items
{
    /// <summary>
    /// Configurable data for an inventory item.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Hitori Kakurembo/Inventory/Item Definition")]
    public class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "item";
        [SerializeField] private string displayName = "Item";
        [SerializeField] private ItemType itemType = ItemType.None;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject worldPrefab;
        [SerializeField] private bool consumable;
        [SerializeField] private bool droppable = true;

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName;
        public ItemType ItemType => itemType;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public bool Consumable => consumable;
        public bool Droppable => droppable;
    }
}
