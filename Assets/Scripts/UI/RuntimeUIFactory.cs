using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HitoriKakurembo.UI
{
    /// <summary>
    /// Proporciona metodos de apoyo para construir una interfaz UGUI minima en runtime mientras el proyecto aun no tiene pantallas authoring definitivas.
    /// </summary>
    public static class RuntimeUIFactory
    {
        /// <summary>
        /// Cachea la fuente base utilizada por los textos creados en runtime.
        /// </summary>
        private static Font defaultFont;

        /// <summary>
        /// Garantiza la existencia de un <see cref="Canvas"/> overlay listo para recibir contenido de interfaz.
        /// </summary>
        /// <param name="name">
        /// Nombre que se aplicara al objeto raiz del canvas.
        /// </param>
        /// <param name="parent">
        /// Transform padre bajo el cual debe quedar creado el canvas.
        /// </param>
        /// <returns>
        /// Canvas listo para construir la interfaz del flujo actual.
        /// </returns>
        public static Canvas CreateCanvas(string name, Transform parent)
        {
            GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvasComponent = canvasObject.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasComponent;
        }

        /// <summary>
        /// Garantiza que exista un <see cref="EventSystem"/> utilizable por botones e input fields.
        /// </summary>
        /// <param name="parent">
        /// Transform bajo el cual se creara el event system cuando aun no exista uno en la sesion.
        /// </param>
        public static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("PrototypeEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(parent, false);
        }

        /// <summary>
        /// Elimina todos los hijos directos del contenedor indicado para poder reconstruir una nueva pantalla.
        /// </summary>
        /// <param name="parent">
        /// Transform cuyo contenido debe vaciarse.
        /// </param>
        public static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Object.Destroy(parent.GetChild(index).gameObject);
            }
        }

        /// <summary>
        /// Crea un panel central con layout vertical para agrupar el contenido principal de una pantalla.
        /// </summary>
        /// <param name="name">
        /// Nombre que se aplicara al panel.
        /// </param>
        /// <param name="parent">
        /// Transform padre del panel.
        /// </param>
        /// <returns>
        /// RectTransform del panel listo para recibir elementos hijos.
        /// </returns>
        public static RectTransform CreateCenteredCard(string name, Transform parent)
        {
            GameObject panelObject = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(720f, 0f);

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0.06f, 0.08f, 0.11f, 0.92f);

            VerticalLayoutGroup layoutGroup = panelObject.GetComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 16f;
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;

            ContentSizeFitter sizeFitter = panelObject.GetComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rectTransform;
        }

        /// <summary>
        /// Crea un texto UGUI listo para mostrar informacion contextual del flujo.
        /// </summary>
        /// <param name="name">
        /// Nombre del objeto de texto.
        /// </param>
        /// <param name="parent">
        /// Transform padre del texto.
        /// </param>
        /// <param name="content">
        /// Contenido inicial que debe mostrarse.
        /// </param>
        /// <param name="fontSize">
        /// Tamano de fuente base.
        /// </param>
        /// <param name="alignment">
        /// Alineacion que debe utilizar el texto.
        /// </param>
        /// <param name="color">
        /// Color aplicado al texto.
        /// </param>
        /// <returns>
        /// Componente <see cref="Text"/> creado.
        /// </returns>
        public static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            Text textComponent = textObject.GetComponent<Text>();
            textComponent.font = GetDefaultFont();
            textComponent.text = content;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = color;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = fontSize + 12f;

            return textComponent;
        }

        /// <summary>
        /// Crea un espaciador flexible dentro de un layout vertical.
        /// </summary>
        /// <param name="parent">
        /// Transform padre del espaciador.
        /// </param>
        /// <param name="height">
        /// Alto minimo que debe reservar el espaciador.
        /// </param>
        public static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacerObject = new GameObject("Spacer", typeof(LayoutElement));
            spacerObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = spacerObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = height;
        }

        /// <summary>
        /// Crea un <see cref="InputField"/> simple con placeholder y etiqueta de texto interna.
        /// </summary>
        /// <param name="name">
        /// Nombre del objeto raiz del input.
        /// </param>
        /// <param name="parent">
        /// Transform padre del input.
        /// </param>
        /// <param name="placeholderText">
        /// Texto placeholder mostrado cuando el campo esta vacio.
        /// </param>
        /// <param name="defaultValue">
        /// Valor inicial del campo.
        /// </param>
        /// <returns>
        /// Componente <see cref="InputField"/> creado.
        /// </returns>
        public static InputField CreateInputField(string name, Transform parent, string placeholderText, string defaultValue)
        {
            GameObject inputObject = new GameObject(name, typeof(Image), typeof(InputField), typeof(LayoutElement));
            inputObject.transform.SetParent(parent, false);

            Image background = inputObject.GetComponent<Image>();
            background.color = new Color(0.14f, 0.16f, 0.2f, 1f);

            LayoutElement layoutElement = inputObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 56f;

            RectTransform inputRect = inputObject.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(0f, 56f);

            Text textComponent = CreateInputText("Text", inputObject.transform, Color.white);
            Text placeholderComponent = CreateInputText("Placeholder", inputObject.transform, new Color(0.67f, 0.72f, 0.78f, 0.75f));
            placeholderComponent.text = placeholderText;
            placeholderComponent.fontStyle = FontStyle.Italic;

            InputField inputField = inputObject.GetComponent<InputField>();
            inputField.targetGraphic = background;
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderComponent;
            inputField.text = defaultValue;

            return inputField;
        }

        /// <summary>
        /// Crea un boton UGUI con su etiqueta de texto y colores base del prototipo.
        /// </summary>
        /// <param name="name">
        /// Nombre del objeto boton.
        /// </param>
        /// <param name="parent">
        /// Transform padre del boton.
        /// </param>
        /// <param name="label">
        /// Texto visible del boton.
        /// </param>
        /// <returns>
        /// Componente <see cref="Button"/> creado.
        /// </returns>
        public static Button CreateButton(string name, Transform parent, string label)
        {
            GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.18f, 0.4f, 0.62f, 1f);

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 56f;

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0f, 56f);

            Text labelText = CreateText("Label", buttonObject.transform, label, 20, TextAnchor.MiddleCenter, Color.white);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Button buttonComponent = buttonObject.GetComponent<Button>();
            ColorBlock colors = buttonComponent.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(0.22f, 0.48f, 0.72f, 1f);
            colors.pressedColor = new Color(0.11f, 0.28f, 0.42f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
            buttonComponent.colors = colors;

            return buttonComponent;
        }

        /// <summary>
        /// Resuelve la fuente por defecto utilizada por los textos runtime, con fallback compatible para editor y build.
        /// </summary>
        /// <returns>
        /// Fuente lista para asignarse a componentes <see cref="Text"/>.
        /// </returns>
        private static Font GetDefaultFont()
        {
            if (defaultFont != null)
            {
                return defaultFont;
            }

            defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            if (defaultFont != null)
            {
                return defaultFont;
            }

            try
            {
                defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return defaultFont;
        }

        /// <summary>
        /// Crea el texto interno utilizado por un input field para el valor editable o su placeholder.
        /// </summary>
        /// <param name="name">
        /// Nombre del objeto de texto.
        /// </param>
        /// <param name="parent">
        /// Transform padre del texto.
        /// </param>
        /// <param name="color">
        /// Color aplicado al texto.
        /// </param>
        /// <returns>
        /// Componente <see cref="Text"/> creado y configurado para usarse dentro del input.
        /// </returns>
        private static Text CreateInputText(string name, Transform parent, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text textComponent = textObject.GetComponent<Text>();
            textComponent.font = GetDefaultFont();
            textComponent.fontSize = 18;
            textComponent.alignment = TextAnchor.MiddleLeft;
            textComponent.color = color;

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(18f, 8f);
            rectTransform.offsetMax = new Vector2(-18f, -8f);

            return textComponent;
        }
    }
}
