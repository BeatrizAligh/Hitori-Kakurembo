using System.Collections.Generic;
using System.Linq;
using TMPro;
using TestMultiplayer.Gameplay;
using TestMultiplayer.Networking;
using TestMultiplayer.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestMultiplayer.Editor
{
    public static class TestMultiplayerDemoAssetBuilder
    {
        private const string Root = "Assets/TestMultiplayer";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string UiPrefabFolder = PrefabFolder + "/UI";
        private const string SceneFolder = Root + "/Scenes";
        private const string BrainPrefabPath = PrefabFolder + "/TestMultiplayerPlayerBrain.prefab";
        private const string PawnPrefabPath = PrefabFolder + "/TestMultiplayerDemoPawn.prefab";
        private const string ButtonPrefabPath = UiPrefabFolder + "/TestMultiplayerButton.prefab";
        private const string PlayerRowPrefabPath = UiPrefabFolder + "/TestMultiplayerPlayerRow.prefab";
        private const string UiRootPrefabPath = UiPrefabFolder + "/TestMultiplayerUIRoot.prefab";
        private const string MainWindowPrefabPath = UiPrefabFolder + "/Windows/TestMultiplayerMainWindow.prefab";
        private const string SessionWindowPrefabPath = UiPrefabFolder + "/Windows/TestMultiplayerSessionWindow.prefab";
        private const string CustomizationWindowPrefabPath = UiPrefabFolder + "/Windows/TestMultiplayerCustomizationWindow.prefab";
        private const string LobbyWindowPrefabPath = UiPrefabFolder + "/Windows/TestMultiplayerLobbyWindow.prefab";
        private const string ConnectedPlayersHudPrefabPath = UiPrefabFolder + "/Windows/TestMultiplayerConnectedPlayersHud.prefab";
        private const string MainScenePath = SceneFolder + "/TestMultiplayerMainMenu.unity";
        private const string LobbyScenePath = SceneFolder + "/TestMultiplayerLobby.unity";
        private const string GameScenePath = SceneFolder + "/TestMultiplayerGame.unity";

        [MenuItem("Test Multiplayer/Build Demo Assets")]
        public static void BuildDemoAssets()
        {
            EnsureFolders();
            GameObject pawnPrefab = BuildPawnPrefab();
            GameObject brainPrefab = BuildBrainPrefab();
            Button buttonPrefab = BuildButtonPrefab();
            TestMultiplayerHudPlayerRow playerRowPrefab = BuildPlayerRowPrefab();
            TestMultiplayerUIReferences uiRootPrefab = BuildUiRootPrefab(buttonPrefab, playerRowPrefab);
            BuildMainMenuScene(brainPrefab, pawnPrefab, uiRootPrefab);
            BuildLobbyScene();
            BuildGameScene(pawnPrefab);
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Test Multiplayer demo assets generated.");
        }

        private static GameObject BuildBrainPrefab()
        {
            GameObject root = new GameObject("TestMultiplayerPlayerBrain");
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            root.AddComponent<TestMultiplayerPlayerBrain>();
            PrefabUtility.SaveAsPrefabAsset(root, BrainPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BrainPrefabPath);
        }

        private static GameObject BuildPawnPrefab()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "TestMultiplayerDemoPawn";
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.AddComponent<NetworkObject>();

            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;

            DemoPawn pawn = root.AddComponent<DemoPawn>();
            SerializedObject serializedPawn = new SerializedObject(pawn);
            serializedPawn.FindProperty("targetRenderer").objectReferenceValue = root.GetComponent<Renderer>();
            serializedPawn.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PawnPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPath);
        }

        private static Button BuildButtonPrefab()
        {
            GameObject buttonObject = new GameObject("TestMultiplayerButton", typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.42f, 0.5f);
            buttonObject.GetComponent<LayoutElement>().minHeight = 54f;

            GameObject labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = "Button";
            label.fontSize = 20;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            PrefabUtility.SaveAsPrefabAsset(buttonObject, ButtonPrefabPath);
            Object.DestroyImmediate(buttonObject);
            return AssetDatabase.LoadAssetAtPath<Button>(ButtonPrefabPath);
        }

        private static TestMultiplayerHudPlayerRow BuildPlayerRowPrefab()
        {
            GameObject rowObject = new GameObject("TestMultiplayerPlayerRow", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.GetComponent<LayoutElement>().minHeight = 48f;

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;

            GameObject pictureObject = new GameObject("ProfilePicture", typeof(Image), typeof(LayoutElement));
            pictureObject.transform.SetParent(rowObject.transform, false);
            pictureObject.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.26f, 1f);
            LayoutElement pictureLayout = pictureObject.GetComponent<LayoutElement>();
            pictureLayout.minWidth = 42f;
            pictureLayout.minHeight = 42f;
            pictureLayout.preferredWidth = 42f;
            pictureLayout.preferredHeight = 42f;

            GameObject textColumn = new GameObject("TextColumn", typeof(VerticalLayoutGroup), typeof(LayoutElement));
            textColumn.transform.SetParent(rowObject.transform, false);
            textColumn.GetComponent<LayoutElement>().minWidth = 240f;
            VerticalLayoutGroup columnLayout = textColumn.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 0f;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;

            TMP_Text playerNameText = CreateText("PlayerName", textColumn.transform, "Player", 14, TextAnchor.MiddleLeft);
            TMP_Text pingText = CreateText("Ping", textColumn.transform, "Ping: --", 12, TextAnchor.MiddleLeft);

            TestMultiplayerHudPlayerRow row = rowObject.AddComponent<TestMultiplayerHudPlayerRow>();
            SerializedObject serializedRow = new SerializedObject(row);
            serializedRow.FindProperty("profilePicture").objectReferenceValue = pictureObject.GetComponent<Image>();
            serializedRow.FindProperty("playerNameText").objectReferenceValue = playerNameText;
            serializedRow.FindProperty("pingText").objectReferenceValue = pingText;
            serializedRow.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(rowObject, PlayerRowPrefabPath);
            Object.DestroyImmediate(rowObject);
            return AssetDatabase.LoadAssetAtPath<TestMultiplayerHudPlayerRow>(PlayerRowPrefabPath);
        }

        private static TestMultiplayerUIReferences BuildUiRootPrefab(Button buttonPrefab, TestMultiplayerHudPlayerRow playerRowPrefab)
        {
            GameObject root = new GameObject("TestMultiplayerUIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            TestMultiplayerUIReferences references = root.AddComponent<TestMultiplayerUIReferences>();

            GameObject mainWindow = BuildMainWindow(buttonPrefab);
            GameObject sessionWindow = BuildSessionWindow(buttonPrefab);
            GameObject customizationWindow = BuildCustomizationWindow(buttonPrefab);
            GameObject lobbyWindow = BuildLobbyWindow(buttonPrefab);
            GameObject connectedPlayersHud = BuildConnectedPlayersHud(playerRowPrefab);

            GameObject mainInstance = PrefabUtility.InstantiatePrefab(mainWindow, root.transform) as GameObject;
            GameObject sessionInstance = PrefabUtility.InstantiatePrefab(sessionWindow, root.transform) as GameObject;
            GameObject customizationInstance = PrefabUtility.InstantiatePrefab(customizationWindow, root.transform) as GameObject;
            GameObject lobbyInstance = PrefabUtility.InstantiatePrefab(lobbyWindow, root.transform) as GameObject;
            GameObject hudInstance = PrefabUtility.InstantiatePrefab(connectedPlayersHud, root.transform) as GameObject;

            AssignUiReferences(references, mainInstance, sessionInstance, customizationInstance, lobbyInstance, hudInstance, playerRowPrefab);
            PrefabUtility.SaveAsPrefabAsset(root, UiRootPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<TestMultiplayerUIReferences>(UiRootPrefabPath);
        }

        private static GameObject BuildMainWindow(Button buttonPrefab)
        {
            GameObject window = CreateWindowRoot("TestMultiplayerMainWindow");
            CreateText("Title", window.transform, "Test Multiplayer", 34, TextAnchor.MiddleCenter);
            CreateText("ProfileText", window.transform, "Perfil", 18, TextAnchor.MiddleCenter);
            AddButton(buttonPrefab, "OpenSessionButton", window.transform, "Iniciar juego");
            AddButton(buttonPrefab, "OpenCustomizationButton", window.transform, "Personalizar personaje");
            CreateText("StatusText", window.transform, string.Empty, 16, TextAnchor.MiddleCenter);
            PrefabUtility.SaveAsPrefabAsset(window, MainWindowPrefabPath);
            Object.DestroyImmediate(window);
            return AssetDatabase.LoadAssetAtPath<GameObject>(MainWindowPrefabPath);
        }

        private static GameObject BuildSessionWindow(Button buttonPrefab)
        {
            GameObject window = CreateWindowRoot("TestMultiplayerSessionWindow");
            CreateText("Title", window.transform, "Sala multiplayer", 30, TextAnchor.MiddleCenter);
            CreateInput("PlayerNameInput", window.transform, "Nombre", "Player");
            CreateInput("JoinCodeInput", window.transform, "Codigo de acceso", string.Empty);
            AddButton(buttonPrefab, "CreateLobbyButton", window.transform, "Crear partida");
            AddButton(buttonPrefab, "JoinLobbyButton", window.transform, "Unirse a partida");
            AddButton(buttonPrefab, "BackButton", window.transform, "Volver");
            CreateText("StatusText", window.transform, string.Empty, 16, TextAnchor.MiddleCenter);
            PrefabUtility.SaveAsPrefabAsset(window, SessionWindowPrefabPath);
            Object.DestroyImmediate(window);
            return AssetDatabase.LoadAssetAtPath<GameObject>(SessionWindowPrefabPath);
        }

        private static GameObject BuildCustomizationWindow(Button buttonPrefab)
        {
            GameObject window = CreateWindowRoot("TestMultiplayerCustomizationWindow");
            CreateText("Title", window.transform, "Personaje", 30, TextAnchor.MiddleCenter);
            CreateInput("PlayerNameInput", window.transform, "Nombre", "Player");
            CreateInput("HeadInput", window.transform, "Cabeza", "0").contentType = TMP_InputField.ContentType.IntegerNumber;
            CreateInput("HairInput", window.transform, "Cabello", "0").contentType = TMP_InputField.ContentType.IntegerNumber;
            CreateInput("UpperBodyInput", window.transform, "Parte superior", "0").contentType = TMP_InputField.ContentType.IntegerNumber;
            CreateInput("LowerBodyInput", window.transform, "Parte inferior", "0").contentType = TMP_InputField.ContentType.IntegerNumber;
            CreateInput("EyesInput", window.transform, "Ojos", "0").contentType = TMP_InputField.ContentType.IntegerNumber;
            AddButton(buttonPrefab, "SaveButton", window.transform, "Guardar");
            AddButton(buttonPrefab, "BackButton", window.transform, "Volver");
            PrefabUtility.SaveAsPrefabAsset(window, CustomizationWindowPrefabPath);
            Object.DestroyImmediate(window);
            return AssetDatabase.LoadAssetAtPath<GameObject>(CustomizationWindowPrefabPath);
        }

        private static GameObject BuildLobbyWindow(Button buttonPrefab)
        {
            GameObject window = CreateWindowRoot("TestMultiplayerLobbyWindow");
            CreateText("Title", window.transform, "Lobby", 30, TextAnchor.MiddleCenter);
            CreateText("LobbyStateText", window.transform, string.Empty, 18, TextAnchor.UpperLeft);
            AddButton(buttonPrefab, "ReadyButton", window.transform, "Listo");
            AddButton(buttonPrefab, "StartGameButton", window.transform, "Arrancar partida");
            AddButton(buttonPrefab, "LeaveButton", window.transform, "Salir");
            PrefabUtility.SaveAsPrefabAsset(window, LobbyWindowPrefabPath);
            Object.DestroyImmediate(window);
            return AssetDatabase.LoadAssetAtPath<GameObject>(LobbyWindowPrefabPath);
        }

        private static GameObject BuildConnectedPlayersHud(TestMultiplayerHudPlayerRow playerRowPrefab)
        {
            GameObject panel = new GameObject("TestMultiplayerConnectedPlayersHud", typeof(Image), typeof(VerticalLayoutGroup));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(360f, 220f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.82f);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText("ConnectedPlayersTitle", panel.transform, "Jugadores conectados: 0", 16, TextAnchor.UpperLeft);
            GameObject rows = new GameObject("PlayerRows", typeof(VerticalLayoutGroup));
            rows.transform.SetParent(panel.transform, false);
            VerticalLayoutGroup rowsLayout = rows.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 8f;
            rowsLayout.childControlHeight = true;
            rowsLayout.childControlWidth = true;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childForceExpandWidth = true;

            PrefabUtility.SaveAsPrefabAsset(panel, ConnectedPlayersHudPrefabPath);
            Object.DestroyImmediate(panel);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ConnectedPlayersHudPrefabPath);
        }

        private static void BuildMainMenuScene(GameObject brainPrefab, GameObject pawnPrefab, TestMultiplayerUIReferences uiRootPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("TestMultiplayerEventSystemBootstrap").AddComponent<TestMultiplayerEventSystemBootstrap>();

            GameObject root = new GameObject("TestMultiplayerDemo");
            TestMultiplayerSessionManager session = root.AddComponent<TestMultiplayerSessionManager>();
            TestMultiplayerMainMenuUI mainMenuUi = root.AddComponent<TestMultiplayerMainMenuUI>();

            SerializedObject serializedSession = new SerializedObject(session);
            serializedSession.FindProperty("playerBrainPrefab").objectReferenceValue = brainPrefab.GetComponent<NetworkObject>();
            SerializedProperty registeredPrefabs = serializedSession.FindProperty("registeredNetworkPrefabs");
            registeredPrefabs.arraySize = 1;
            registeredPrefabs.GetArrayElementAtIndex(0).objectReferenceValue = pawnPrefab.GetComponent<NetworkObject>();
            serializedSession.FindProperty("lobbySceneName").stringValue = "TestMultiplayerLobby";
            serializedSession.FindProperty("gameSceneName").stringValue = "TestMultiplayerGame";
            serializedSession.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedMainMenuUi = new SerializedObject(mainMenuUi);
            serializedMainMenuUi.FindProperty("sessionManager").objectReferenceValue = session;
            serializedMainMenuUi.FindProperty("uiRootPrefab").objectReferenceValue = uiRootPrefab;
            serializedMainMenuUi.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        private static void BuildLobbyScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("TestMultiplayerEventSystemBootstrap").AddComponent<TestMultiplayerEventSystemBootstrap>();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void BuildGameScene(GameObject pawnPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("TestMultiplayerEventSystemBootstrap").AddComponent<TestMultiplayerEventSystemBootstrap>();
            Camera camera = CreateCamera();
            camera.transform.position = new Vector3(0f, 8f, -10f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Demo Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            GameObject spawnerObject = new GameObject("DemoGamePawnSpawner");
            spawnerObject.AddComponent<NetworkObject>();
            DemoGamePawnSpawner spawner = spawnerObject.AddComponent<DemoGamePawnSpawner>();
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("pawnPrefab").objectReferenceValue = pawnPrefab.GetComponent<NetworkPawn>();
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("LocalBrainInputDriver").AddComponent<LocalBrainInputDriver>();
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static GameObject CreateWindowRoot(string name)
        {
            GameObject window = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));

            RectTransform rect = window.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 0f);

            window.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.1f, 0.94f);

            VerticalLayoutGroup layout = window.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = window.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return window;
        }

        private static Button AddButton(Button buttonPrefab, string name, Transform parent, string label)
        {
            Button button = buttonPrefab != null
                ? Object.Instantiate(buttonPrefab, parent, false)
                : CreateFallbackButton(name, parent);

            button.name = name;
            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);

            if (labelText != null)
            {
                labelText.text = label;
            }

            return button;
        }

        private static Button CreateFallbackButton(string name, Transform parent)
        {
            GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.42f, 0.5f);
            buttonObject.GetComponent<LayoutElement>().minHeight = 54f;
            TMP_Text label = CreateText("Label", buttonObject.transform, "Button", 20, TextAnchor.MiddleCenter);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return buttonObject.GetComponent<Button>();
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
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

        private static TMP_InputField CreateInput(string name, Transform parent, string placeholder, string value)
        {
            GameObject inputObject = new GameObject(name, typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.19f);
            inputObject.GetComponent<LayoutElement>().minHeight = 54f;

            TMP_Text text = CreateInputText("Text", inputObject.transform, Color.white);
            TMP_Text placeholderText = CreateInputText("Placeholder", inputObject.transform, new Color(0.72f, 0.74f, 0.78f));
            placeholderText.text = placeholder;
            placeholderText.fontStyle = FontStyles.Italic;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.text = value;
            return input;
        }

        private static TMP_Text CreateInputText(string name, Transform parent, Color color)
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

        private static void AssignUiReferences(
            TestMultiplayerUIReferences references,
            GameObject mainWindow,
            GameObject sessionWindow,
            GameObject customizationWindow,
            GameObject lobbyWindow,
            GameObject connectedPlayersHud,
            TestMultiplayerHudPlayerRow playerRowPrefab)
        {
            SerializedObject serializedReferences = new SerializedObject(references);
            serializedReferences.FindProperty("mainWindow").objectReferenceValue = mainWindow;
            serializedReferences.FindProperty("sessionWindow").objectReferenceValue = sessionWindow;
            serializedReferences.FindProperty("customizationWindow").objectReferenceValue = customizationWindow;
            serializedReferences.FindProperty("lobbyWindow").objectReferenceValue = lobbyWindow;
            serializedReferences.FindProperty("connectedPlayersHud").objectReferenceValue = connectedPlayersHud;

            serializedReferences.FindProperty("mainProfileText").objectReferenceValue = Find<TMP_Text>(mainWindow, "ProfileText");
            serializedReferences.FindProperty("openSessionButton").objectReferenceValue = Find<Button>(mainWindow, "OpenSessionButton");
            serializedReferences.FindProperty("openCustomizationButton").objectReferenceValue = Find<Button>(mainWindow, "OpenCustomizationButton");
            serializedReferences.FindProperty("mainStatusText").objectReferenceValue = Find<TMP_Text>(mainWindow, "StatusText");

            serializedReferences.FindProperty("sessionPlayerNameInput").objectReferenceValue = Find<TMP_InputField>(sessionWindow, "PlayerNameInput");
            serializedReferences.FindProperty("joinCodeInput").objectReferenceValue = Find<TMP_InputField>(sessionWindow, "JoinCodeInput");
            serializedReferences.FindProperty("createLobbyButton").objectReferenceValue = Find<Button>(sessionWindow, "CreateLobbyButton");
            serializedReferences.FindProperty("joinLobbyButton").objectReferenceValue = Find<Button>(sessionWindow, "JoinLobbyButton");
            serializedReferences.FindProperty("sessionBackButton").objectReferenceValue = Find<Button>(sessionWindow, "BackButton");
            serializedReferences.FindProperty("sessionStatusText").objectReferenceValue = Find<TMP_Text>(sessionWindow, "StatusText");

            serializedReferences.FindProperty("customizationPlayerNameInput").objectReferenceValue = Find<TMP_InputField>(customizationWindow, "PlayerNameInput");
            serializedReferences.FindProperty("headInput").objectReferenceValue = Find<TMP_InputField>(customizationWindow, "HeadInput");
            serializedReferences.FindProperty("hairInput").objectReferenceValue = Find<TMP_InputField>(customizationWindow, "HairInput");
            serializedReferences.FindProperty("upperBodyInput").objectReferenceValue = Find<TMP_InputField>(customizationWindow, "UpperBodyInput");
            serializedReferences.FindProperty("lowerBodyInput").objectReferenceValue = Find<TMP_InputField>(customizationWindow, "LowerBodyInput");
            serializedReferences.FindProperty("eyesInput").objectReferenceValue = Find<TMP_InputField>(customizationWindow, "EyesInput");
            serializedReferences.FindProperty("saveCustomizationButton").objectReferenceValue = Find<Button>(customizationWindow, "SaveButton");
            serializedReferences.FindProperty("customizationBackButton").objectReferenceValue = Find<Button>(customizationWindow, "BackButton");

            serializedReferences.FindProperty("lobbyStateText").objectReferenceValue = Find<TMP_Text>(lobbyWindow, "LobbyStateText");
            serializedReferences.FindProperty("readyButton").objectReferenceValue = Find<Button>(lobbyWindow, "ReadyButton");
            serializedReferences.FindProperty("startGameButton").objectReferenceValue = Find<Button>(lobbyWindow, "StartGameButton");
            serializedReferences.FindProperty("leaveButton").objectReferenceValue = Find<Button>(lobbyWindow, "LeaveButton");

            serializedReferences.FindProperty("connectedPlayersTitleText").objectReferenceValue = Find<TMP_Text>(connectedPlayersHud, "ConnectedPlayersTitle");
            serializedReferences.FindProperty("connectedPlayersRowsRoot").objectReferenceValue = FindTransform(connectedPlayersHud, "PlayerRows");
            serializedReferences.FindProperty("connectedPlayerRowPrefab").objectReferenceValue = playerRowPrefab;
            serializedReferences.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Find<T>(GameObject root, string childName) where T : Component
        {
            Transform child = FindTransform(root, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindTransform(GameObject root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
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

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                AssetDatabase.CreateFolder("Assets", "TestMultiplayer");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder(Root, "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(UiPrefabFolder))
            {
                AssetDatabase.CreateFolder(PrefabFolder, "UI");
            }

            if (!AssetDatabase.IsValidFolder(UiPrefabFolder + "/Windows"))
            {
                AssetDatabase.CreateFolder(UiPrefabFolder, "Windows");
            }

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder(Root, "Scenes");
            }
        }

        private static void AddScenesToBuildSettings()
        {
            string[] scenePaths = { MainScenePath, LobbyScenePath, GameScenePath };
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            foreach (string path in scenePaths)
            {
                if (scenes.Any(scene => scene.path == path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
