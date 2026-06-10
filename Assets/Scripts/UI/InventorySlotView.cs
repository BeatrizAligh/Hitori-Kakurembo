using HitoriKakurembo.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HitoriKakurembo.UI
{
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color unavailableColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0f);

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            ResolveReferences();
        }

        public void Draw(PlayerInventory.InventoryEntry entry, bool selected, bool unavailable, Sprite fallbackSprite)
        {
            ResolveReferences();

            if (iconImage == null)
            {
                return;
            }

            if (entry?.Item == null)
            {
                iconImage.sprite = null;
                iconImage.color = emptyColor;
                SetBackground(selected, false);
                return;
            }

            iconImage.sprite = entry.Item.Icon != null ? entry.Item.Icon : fallbackSprite;
            iconImage.color = unavailable ? unavailableColor : normalColor;
            SetBackground(selected, unavailable);
        }

        private void SetBackground(bool selected, bool unavailable)
        {
            if (backgroundImage == null)
            {
                return;
            }

            if (selected)
            {
                backgroundImage.color = unavailable ? unavailableColor : selectedColor;
            }
        }

        private void ResolveReferences()
        {
            backgroundImage = backgroundImage != null ? backgroundImage : GetComponent<Image>();

            if (iconImage != null)
            {
                return;
            }

            Transform iconTransform = transform.Find("Icon");
            iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : GetComponentInChildren<Image>(true);
        }
    }
}
