// Coloca este archivo en cualquier carpeta llamada Editor dentro de tu proyecto
// Ejemplo: Assets/Editor/NetworkObjectHashFixer.cs

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkObjectHashFixer : EditorWindow
{
    [MenuItem("Netcode/Fix Duplicate GlobalObjectIdHash")]
    public static void ShowWindow()
    {
        GetWindow<NetworkObjectHashFixer>("Hash Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Netcode Scene Object Hash Fixer", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Detecta y repara NetworkObjects en escena que compartan el mismo GlobalObjectIdHash.\n\n" +
            "Úsalo cuando veas el error:\n'registered with ScenePlacedObjects which already contains the same GlobalObjectIdHash'",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("🔍 Detectar duplicados en escena activa", GUILayout.Height(35)))
            DetectDuplicates();

        EditorGUILayout.Space();

        if (GUILayout.Button("🔧 Reparar duplicados (reimportar prefabs de escena)", GUILayout.Height(35)))
            FixDuplicates();
    }

    // ─── Detect ───────────────────────────────────────────────────────────

    private static void DetectDuplicates()
    {
        var seen = new Dictionary<uint, List<NetworkObject>>();

        foreach (var netObj in FindAllSceneNetworkObjects())
        {
            uint hash = GetHash(netObj);
            if (hash == 0 || !seen.ContainsKey(hash))
                seen[hash] = new List<NetworkObject>();
            seen[hash].Add(netObj);
        }

        bool found = false;
        foreach (var kvp in seen)
        {
            if (kvp.Value.Count > 1)
            {
                found = true;
                string names = string.Join(", ", kvp.Value.ConvertAll(n => n.name));
                Debug.LogWarning($"[HashFixer] Hash duplicado {kvp.Key} en: {names}");
            }
        }

        if (!found)
            Debug.Log("[HashFixer] ✅ No se encontraron hashes duplicados.");
        else
            Debug.LogWarning("[HashFixer] ⚠ Duplicados encontrados — ejecuta 'Reparar' para corregirlos.");
    }

    // ─── Fix ──────────────────────────────────────────────────────────────

    private static void FixDuplicates()
    {
        // Guardar cambios pendientes antes de tocar la escena
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[HashFixer] Operación cancelada por el usuario.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        var allNetObjs = FindAllSceneNetworkObjects();

        var seen      = new Dictionary<uint, NetworkObject>(); // hash → primer objeto (se queda)
        var toReplace = new List<(NetworkObject corrupt, GameObject prefabSource)>();

        foreach (var netObj in allNetObjs)
        {
            uint hash = GetHash(netObj);
            if (hash == 0) continue;

            if (!seen.ContainsKey(hash))
            {
                seen[hash] = netObj;
                continue;
            }

            // Este es un duplicado — necesita ser reemplazado
            GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(netObj.gameObject);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[HashFixer] '{netObj.name}' tiene hash duplicado pero no es instancia de prefab. Unpackealo manualmente.");
                continue;
            }

            toReplace.Add((netObj, prefabRoot));
        }

        if (toReplace.Count == 0)
        {
            Debug.Log("[HashFixer] ✅ No hay nada que reparar.");
            return;
        }

        int fixedCount = 0;
        foreach (var (corrupt, prefabSource) in toReplace)
        {
            // Guardar transform para restaurar después
            Transform t = corrupt.transform;
            Vector3    pos    = t.position;
            Quaternion rot    = t.rotation;
            Vector3    scale  = t.localScale;
            Transform  parent = t.parent;
            int        sibIdx = t.GetSiblingIndex();

            // Destruir el objeto corrupto
            string objName = corrupt.name;
            Undo.DestroyObjectImmediate(corrupt.gameObject);

            // Instanciar una copia fresca del prefab — Unity asignará un nuevo GlobalObjectId
            GameObject fresh = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource, scene);
            fresh.name = objName;

            Transform ft = fresh.transform;
            ft.SetParent(parent);
            ft.position   = pos;
            ft.rotation   = rot;
            ft.localScale = scale;
            ft.SetSiblingIndex(sibIdx);

            Undo.RegisterCreatedObjectUndo(fresh, "HashFixer: Replace duplicate NetworkObject");

            Debug.Log($"[HashFixer] 🔧 Reemplazado: '{objName}'");
            fixedCount++;
        }

        // Marcar escena como modificada y guardar
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[HashFixer] ✅ Reparados {fixedCount} objeto(s). Escena guardada.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    // Accede a GlobalObjectIdHash via reflexión porque es internal en algunas versiones de Netcode
    private static readonly PropertyInfo s_HashProp = typeof(NetworkObject).GetProperty(
        "GlobalObjectIdHash",
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance
    );

    private static uint GetHash(NetworkObject netObj)
    {
        if (s_HashProp == null)
        {
            Debug.LogError("[HashFixer] No se encontró la propiedad GlobalObjectIdHash en NetworkObject. " +
                           "Verifica la versión de Netcode instalada.");
            return 0;
        }
        return (uint)s_HashProp.GetValue(netObj);
    }

    private static List<NetworkObject> FindAllSceneNetworkObjects()
    {
        var result = new List<NetworkObject>();
        var scene  = SceneManager.GetActiveScene();

        foreach (var root in scene.GetRootGameObjects())
        foreach (var netObj in root.GetComponentsInChildren<NetworkObject>(includeInactive: true))
            result.Add(netObj);

        return result;
    }
}
#endif
