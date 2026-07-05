using System.Collections.Generic;
using System.Linq;
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
            BuildMainMenuScene(brainPrefab, pawnPrefab, buttonPrefab);
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

            GameObject labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.GetComponent<Text>();
            label.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            label.text = "Button";
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
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

        private static void BuildMainMenuScene(GameObject brainPrefab, GameObject pawnPrefab, Button buttonPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

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
            serializedMainMenuUi.FindProperty("buttonPrefab").objectReferenceValue = buttonPrefab;
            serializedMainMenuUi.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        private static void BuildLobbyScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void BuildGameScene(GameObject pawnPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
