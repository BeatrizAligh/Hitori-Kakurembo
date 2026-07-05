using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TestMultiplayer.UI
{
    public static class TestMultiplayerUIFactory
    {
        public static Canvas CreateCanvas(string name, Transform parent)
        {
            GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            EnsurePersistentEventSystem();
        }

        public static void EnsurePersistentEventSystem()
        {
            EventSystem existingEventSystem = Object.FindAnyObjectByType<EventSystem>();

            if (existingEventSystem != null)
            {
                if (existingEventSystem.transform.parent != null)
                {
                    existingEventSystem.transform.SetParent(null);
                }

                Object.DontDestroyOnLoad(existingEventSystem.gameObject);
                return;
            }

            GameObject eventSystemObject = new GameObject("TestMultiplayerEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemObject);
        }

        public static RectTransform CreatePanel(string name, Transform parent)
        {
            GameObject panelObject = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelObject.transform.SetParent(parent, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 0f);

            panelObject.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.1f, 0.94f);

            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        public static TMP_Text Text(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = ToTmpAlignment(alignment);
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;

            LayoutElement layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = size + 12f;
            return text;
        }

        public static Button Button(string name, Transform parent, string label)
        {
            GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.42f, 0.5f);
            buttonObject.GetComponent<LayoutElement>().minHeight = 54f;

            TMP_Text labelText = Text("Label", buttonObject.transform, label, 20, TextAnchor.MiddleCenter);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return buttonObject.GetComponent<Button>();
        }

        public static Button ButtonFromPrefab(Button buttonPrefab, string name, Transform parent, string label)
        {
            if (buttonPrefab == null)
            {
                return Button(name, parent, label);
            }

            Button button = Object.Instantiate(buttonPrefab, parent, false);
            button.name = name;
            SetButtonLabel(button, label);
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);

            if (labelText != null)
            {
                labelText.text = label;
            }
        }

        public static TMP_InputField Input(string name, Transform parent, string placeholder, string value)
        {
            GameObject inputObject = new GameObject(name, typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.19f);
            inputObject.GetComponent<LayoutElement>().minHeight = 54f;

            TMP_Text text = InputText("Text", inputObject.transform, Color.white);
            TMP_Text placeholderText = InputText("Placeholder", inputObject.transform, new Color(0.72f, 0.74f, 0.78f));
            placeholderText.text = placeholder;
            placeholderText.fontStyle = FontStyles.Italic;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.text = value;
            return input;
        }

        private static TMP_Text InputText(string name, Transform parent, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = color;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 6f);
            rect.offsetMax = new Vector2(-16f, -6f);
            return text;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }
    }
}
