using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Core;
using HitoriKakurembo.Seals;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Spawning
{
    /// <summary>
    /// Servicio server-side responsable de encontrar poses validas e instanciar prefabs de red.
    /// Usa SpawnArea como volumen configurable en lugar de puntos fijos.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        /// <summary>
        /// Areas registradas manualmente o descubiertas en escena.
        /// </summary>
        [SerializeField] private List<SpawnArea> spawnAreas = new List<SpawnArea>();

        /// <summary>
        /// Intentos maximos por solicitud de pose antes de fallar con warning.
        /// </summary>
        [SerializeField] private int maxPoseAttempts = 80;

        /// <summary>
        /// Habilita validacion contra NavMesh cuando exista uno configurado.
        /// </summary>
        [SerializeField] private bool useNavMeshValidation = true;

        /// <summary>
        /// Distancia para buscar un punto de NavMesh cercano.
        /// </summary>
        [SerializeField] private float navMeshSampleDistance = 2.5f;

        /// <summary>
        /// Permite descubrir SpawnAreas en escena al habilitar el manager.
        /// </summary>
        [SerializeField] private bool discoverAreasOnEnable = true;

        /// <summary>
        /// Controla logs de diagnostico.
        /// </summary>
        [SerializeField] private bool verboseLogs = true;

        /// <summary>
        /// Sellos spawneados por este manager durante la ronda actual.
        /// </summary>
        private readonly List<RitualSeal> spawnedSeals = new List<RitualSeal>();

        /// <summary>
        /// Ultima pose candidata aceptada para debug.
        /// </summary>
        private Pose lastValidPose;

        /// <summary>
        /// Indica si existe una ultima pose valida para Gizmos.
        /// </summary>
        private bool hasLastValidPose;

        public IReadOnlyList<SpawnArea> SpawnAreas => spawnAreas;
        public IReadOnlyList<RitualSeal> SpawnedSeals => spawnedSeals;

        /// <summary>
        /// Registra el servicio para que otros sistemas lo resuelvan sin busquedas globales repetidas.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<SpawnManager>(this);
        }

        /// <summary>
        /// Descubre areas existentes cuando la escena carga o el manager se reactiva.
        /// </summary>
        private void OnEnable()
        {
            if (discoverAreasOnEnable)
            {
                DiscoverSpawnAreas();
            }
        }

        /// <summary>
        /// Agrega una SpawnArea al registro si aun no existe.
        /// </summary>
        /// <param name="area">Area que debe quedar disponible para spawns.</param>
        public void RegisterSpawnArea(SpawnArea area)
        {
            if (area == null || spawnAreas.Contains(area))
            {
                return;
            }

            spawnAreas.Add(area);
        }

        /// <summary>
        /// Quita una SpawnArea del registro.
        /// </summary>
        /// <param name="area">Area que deja de estar disponible.</param>
        public void UnregisterSpawnArea(SpawnArea area)
        {
            if (area == null)
            {
                return;
            }

            spawnAreas.Remove(area);
        }

        /// <summary>
        /// Devuelve las areas compatibles con una definicion de sello.
        /// </summary>
        /// <param name="definition">Definicion del sello a colocar.</param>
        /// <returns>Lista de areas compatibles y activas.</returns>
        public IReadOnlyList<SpawnArea> GetCompatibleAreas(SealDefinition definition)
        {
            CleanupAreaList();
            return spawnAreas
                .Where(area => area != null && area.isActiveAndEnabled && area.SupportsSeal(definition))
                .ToList();
        }

        /// <summary>
        /// Busca una pose valida para un sello usando areas compatibles, raycasts y validadores espaciales.
        /// </summary>
        /// <param name="definition">Definicion del sello.</param>
        /// <param name="pose">Pose resultante si se encuentra una posicion valida.</param>
        /// <returns>True si se encontro pose valida.</returns>
        public bool TryFindValidSpawnPose(SealDefinition definition, out Pose pose)
        {
            return TryFindValidSpawnPose(definition, 0f, out pose);
        }

        /// <summary>
        /// Busca una pose valida aplicando una distancia minima adicional entre sellos.
        /// </summary>
        /// <param name="definition">Definicion del sello.</param>
        /// <param name="minDistanceOverride">Distancia minima adicional solicitada por el sistema de ronda.</param>
        /// <param name="pose">Pose resultante si se encuentra una posicion valida.</param>
        /// <returns>True si se encontro pose valida.</returns>
        public bool TryFindValidSpawnPose(SealDefinition definition, float minDistanceOverride, out Pose pose)
        {
            pose = default;

            IReadOnlyList<SpawnArea> compatibleAreas = GetCompatibleAreas(definition);

            if (compatibleAreas.Count == 0)
            {
                if (verboseLogs)
                {
                    Debug.LogWarning("No SpawnAreas found. Add SpawnArea components to valid rooms or surfaces.");
                }

                return false;
            }

            int attempts = Mathf.Max(1, maxPoseAttempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                SpawnArea area = compatibleAreas[Random.Range(0, compatibleAreas.Count)];

                if (area == null || !area.TryGetRandomPoint(out Vector3 candidatePoint))
                {
                    continue;
                }

                if (!SurfaceDetector.TryDetectSurface(candidatePoint, definition.PlacementType, area.SurfaceLayerMask, area.GetBounds(), out SurfaceHit surfaceHit))
                {
                    continue;
                }

                if (!SurfaceDetector.IsSurfaceAllowed(surfaceHit.SurfaceType, definition.RequiredSurfaceType))
                {
                    continue;
                }

                Pose candidatePose = SurfaceDetector.CreatePose(surfaceHit, definition.PlacementType);
                SpawnValidator validator = CreateValidator(area, minDistanceOverride);

                if (!validator.IsValidSealPose(definition, candidatePose, area))
                {
                    continue;
                }

                pose = candidatePose;
                lastValidPose = pose;
                hasLastValidPose = true;
                return true;
            }

            if (verboseLogs)
            {
                Debug.LogWarning($"SpawnManager no encontro una pose valida para el sello '{definition.DisplayName}' despues de {attempts} intentos.");
            }

            return false;
        }

        /// <summary>
        /// Instancia un prefab de red en servidor y llama NetworkObject.Spawn().
        /// </summary>
        /// <param name="prefab">Prefab que debe contener NetworkObject.</param>
        /// <param name="pose">Pose final validada.</param>
        /// <returns>NetworkObject spawneado o null si fallo.</returns>
        public NetworkObject SpawnNetworkPrefab(GameObject prefab, Pose pose)
        {
            if (!IsServerActive())
            {
                if (verboseLogs)
                {
                    Debug.LogWarning("SpawnNetworkPrefab solo puede ejecutarse en servidor activo.");
                }

                return null;
            }

            if (prefab == null)
            {
                Debug.LogWarning("SpawnNetworkPrefab recibio un prefab nulo.");
                return null;
            }

            GameObject instance = Instantiate(prefab, pose.position, pose.rotation);
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogWarning($"El prefab '{prefab.name}' no contiene NetworkObject. No se puede spawnear por Netcode.");
                Destroy(instance);
                return null;
            }

            networkObject.Spawn();
            return networkObject;
        }

        /// <summary>
        /// Busca pose valida, instancia el prefab de sello y configura su indice.
        /// </summary>
        /// <param name="definition">Definicion de sello.</param>
        /// <param name="sealIndex">Indice logico dentro de la ronda.</param>
        /// <param name="seal">Sello resultante.</param>
        /// <returns>True si el sello fue spawneado.</returns>
        public bool TrySpawnSeal(SealDefinition definition, int sealIndex, out RitualSeal seal)
        {
            return TrySpawnSeal(definition, sealIndex, 0f, out seal);
        }

        /// <summary>
        /// Busca pose valida, instancia el prefab de sello y aplica distancia minima adicional entre sellos.
        /// </summary>
        /// <param name="definition">Definicion de sello.</param>
        /// <param name="sealIndex">Indice logico dentro de la ronda.</param>
        /// <param name="minDistanceOverride">Distancia minima global adicional entre sellos.</param>
        /// <param name="seal">Sello resultante.</param>
        /// <returns>True si el sello fue spawneado.</returns>
        public bool TrySpawnSeal(SealDefinition definition, int sealIndex, float minDistanceOverride, out RitualSeal seal)
        {
            seal = null;

            if (definition == null || definition.SealPrefab == null)
            {
                Debug.LogWarning("No se puede spawnear un sello sin SealDefinition o SealPrefab.");
                return false;
            }

            if (!TryFindValidSpawnPose(definition, minDistanceOverride, out Pose pose))
            {
                return false;
            }

            NetworkObject networkObject = SpawnNetworkPrefab(definition.SealPrefab, pose);

            if (networkObject == null)
            {
                return false;
            }

            seal = networkObject.GetComponent<RitualSeal>();

            if (seal == null)
            {
                Debug.LogWarning($"El prefab '{definition.SealPrefab.name}' no contiene RitualSeal.");
                networkObject.Despawn(true);
                return false;
            }

            seal.ConfigureFromDefinition(definition, sealIndex, true);
            spawnedSeals.Add(seal);
            return true;
        }

        /// <summary>
        /// Elimina del registro los sellos spawneados por este manager.
        /// </summary>
        public void ClearSpawnedSeals()
        {
            if (!IsServerActive())
            {
                return;
            }

            for (int index = spawnedSeals.Count - 1; index >= 0; index--)
            {
                RitualSeal seal = spawnedSeals[index];

                if (seal == null)
                {
                    continue;
                }

                NetworkObject networkObject = seal.GetComponent<NetworkObject>();

                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
                else
                {
                    Destroy(seal.gameObject);
                }
            }

            spawnedSeals.Clear();
        }

        /// <summary>
        /// Redescubre areas desde el menu contextual del editor.
        /// </summary>
        [ContextMenu("Debug/Discover Spawn Areas")]
        public void DiscoverSpawnAreas()
        {
            spawnAreas.Clear();
            SpawnArea[] areas = FindObjectsByType<SpawnArea>(FindObjectsInactive.Exclude);

            foreach (SpawnArea area in areas)
            {
                RegisterSpawnArea(area);
            }

            if (spawnAreas.Count == 0 && verboseLogs)
            {
                Debug.LogWarning("No SpawnAreas found. Add SpawnArea components to valid rooms or surfaces.");
            }
        }

        /// <summary>
        /// Limpia referencias destruidas del registro.
        /// </summary>
        private void CleanupAreaList()
        {
            spawnAreas.RemoveAll(area => area == null);
        }

        /// <summary>
        /// Crea un validador con las listas y parametros actuales.
        /// </summary>
        private SpawnValidator CreateValidator(SpawnArea area, float minDistanceOverride)
        {
            return new SpawnValidator(
                spawnedSeals,
                useNavMeshValidation,
                navMeshSampleDistance,
                area != null ? area.PlayerAccessibilityMask : Physics.DefaultRaycastLayers,
                area != null ? area.ObstacleLayerMask : Physics.DefaultRaycastLayers,
                minDistanceOverride);
        }

        /// <summary>
        /// Determina si la instancia local es servidor de Netcode activo.
        /// </summary>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsServer;
        }

        /// <summary>
        /// Dibuja la ultima pose valida encontrada.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!hasLastValidPose)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(lastValidPose.position, 0.12f);
            Gizmos.DrawRay(lastValidPose.position, lastValidPose.rotation * Vector3.forward * 0.5f);
        }
    }
}
