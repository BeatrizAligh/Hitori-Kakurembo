using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Multiplayer.Gameplay;
using HitoriKakurembo.Multiplayer.Networking;
using HitoriKakurembo.Multiplayer.UI;
using TestMultiplayer.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HitoriKakurembo.Multiplayer.Editor
{
    /// <summary>
    /// Genera los assets authoring del nuevo sistema multiplayer de Hitori sin modificar Assets/TestMultiplayer.
    /// </summary>
    public static class HitoriMultiplayerAssetBuilder
    {
        private const string Root = "Assets/HitoriMultiplayer";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string UiPrefabFolder = PrefabFolder + "/UI";
        private const string SceneFolder = Root + "/Scenes";
        private const string BrainPrefabPath = PrefabFolder + "/HitoriPlayerBrain.prefab";
        private const string PawnPrefabPath = PrefabFolder + "/HitoriSurvivorPawn.prefab";
        private const string DollPawnPrefabPath = PrefabFolder + "/HitoriDollPawn.prefab";
        private const string ButtonPrefabPath = UiPrefabFolder + "/HitoriMultiplayerButton.prefab";
        private const string MainScenePath = SceneFolder + "/HitoriMultiplayerMainMenu.unity";
        private const string LobbyScenePath = SceneFolder + "/HitoriMultiplayerLobby.unity";
        private const string GameScenePath = SceneFolder + "/HitoriMultiplayerGame.unity";

        [MenuItem("Hitori Multiplayer/Build Multiplayer Assets")]
        public static void BuildMultiplayerAssets()
        {
            EnsureFolders();
            GameObject survivorPawnPrefab = BuildPawnPrefab(PawnPrefabPath, "HitoriSurvivorPawn", new Color(0.35f, 0.7f, 0.95f, 1f));
            GameObject dollPawnPrefab = BuildPawnPrefab(DollPawnPrefabPath, "HitoriDollPawn", new Color(0.95f, 0.25f, 0.18f, 1f));
            GameObject brainPrefab = BuildBrainPrefab();
            Button buttonPrefab = BuildButtonPrefab();
            BuildMainMenuScene(brainPrefab, survivorPawnPrefab, dollPawnPrefab, buttonPrefab);
            BuildLobbyScene();
            BuildGameScene(survivorPawnPrefab, dollPawnPrefab);
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Hitori multiplayer assets generated.");
        }

        private static GameObject BuildBrainPrefab()
        {
            GameObject root = new GameObject("HitoriPlayerBrain");
            root.AddComponent<NetworkObject>();
            root.AddComponent<HitoriPlayerBrain>();
            PrefabUtility.SaveAsPrefabAsset(root, BrainPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BrainPrefabPath);
        }

        private static GameObject BuildPawnPrefab(string path, string name, Color color)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());

            Renderer renderer = root.GetComponent<Renderer>();

            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader);
                renderer.sharedMaterial.color = color;
            }

            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.45f;
            characterController.center = Vector3.up;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 45f;

            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            networkTransform.Interpolate = true;

            HitoriPlayerPawn pawn = root.AddComponent<HitoriPlayerPawn>();
            SerializedObject serializedPawn = new SerializedObject(pawn);
            serializedPawn.FindProperty("targetRenderer").objectReferenceValue = renderer;
            serializedPawn.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Button BuildButtonPrefab()
        {
            GameObject buttonObject = new GameObject("HitoriMultiplayerButton", typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.GetComponent<Image>().color = new Color(0.46f, 0.08f, 0.07f, 1f);
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

        private static void BuildMainMenuScene(GameObject brainPrefab, GameObject survivorPawnPrefab, GameObject dollPawnPrefab, Button buttonPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            GameObject root = new GameObject("HitoriMultiplayerRoot");
            HitoriMultiplayerSessionManager session = root.AddComponent<HitoriMultiplayerSessionManager>();
            HitoriMultiplayerMainMenuUI mainMenuUi = root.AddComponent<HitoriMultiplayerMainMenuUI>();

            SerializedObject serializedSession = new SerializedObject(session);
            serializedSession.FindProperty("playerBrainPrefab").objectReferenceValue = brainPrefab.GetComponent<NetworkObject>();
            SerializedProperty registeredPrefabs = serializedSession.FindProperty("registeredNetworkPrefabs");
            registeredPrefabs.arraySize = 2;
            registeredPrefabs.GetArrayElementAtIndex(0).objectReferenceValue = survivorPawnPrefab.GetComponent<NetworkObject>();
            registeredPrefabs.GetArrayElementAtIndex(1).objectReferenceValue = dollPawnPrefab.GetComponent<NetworkObject>();
            serializedSession.FindProperty("lobbySceneName").stringValue = "HitoriMultiplayerLobby";
            serializedSession.FindProperty("gameSceneName").stringValue = "HitoriMultiplayerGame";
            serializedSession.FindProperty("maximumPlayers").intValue = 6;
            serializedSession.FindProperty("minimumPlayersToStart").intValue = 1;
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

        private static void BuildGameScene(GameObject survivorPawnPrefab, GameObject dollPawnPrefab)
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
            ground.name = "Hitori Multiplayer Test Ground";
            ground.transform.localScale = new Vector3(2.5f, 1f, 2.5f);

            GameObject spawnerObject = new GameObject("HitoriGamePawnSpawner");
            spawnerObject.AddComponent<NetworkObject>();
            HitoriGamePawnSpawner spawner = spawnerObject.AddComponent<HitoriGamePawnSpawner>();
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("survivorPawnPrefab").objectReferenceValue = survivorPawnPrefab.GetComponent<NetworkPawn>();
            serializedSpawner.FindProperty("dollPawnPrefab").objectReferenceValue = dollPawnPrefab.GetComponent<NetworkPawn>();
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("HitoriLocalInputDriver").AddComponent<HitoriLocalInputDriver>();
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.035f, 0.032f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "HitoriMultiplayer");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(PrefabFolder, "UI");
            EnsureFolder(Root, "Scenes");
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{folder}"))
            {
                AssetDatabase.CreateFolder(parent, folder);
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
