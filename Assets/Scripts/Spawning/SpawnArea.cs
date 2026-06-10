using System.Collections.Generic;
using HitoriKakurembo.Core;
using HitoriKakurembo.Seals;
using UnityEngine;

namespace HitoriKakurembo.Spawning
{
    /// <summary>
    /// Define un volumen configurable donde el servidor puede buscar posiciones validas de spawn.
    /// No representa un punto fijo: funciona como una zona con reglas de superficies, distancia y accesibilidad.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnArea : MonoBehaviour
    {
        /// <summary>
        /// Identificador logico de la zona, util para debug y herramientas.
        /// </summary>
        [SerializeField] private string areaId = "spawn_area";

        /// <summary>
        /// Tipo funcional de zona.
        /// </summary>
        [SerializeField] private SpawnAreaType areaType = SpawnAreaType.Room;

        /// <summary>
        /// Superficies aceptadas dentro de esta zona.
        /// </summary>
        [SerializeField] private SurfaceType allowedSurfaces = SurfaceType.Wall | SurfaceType.Floor;

        /// <summary>
        /// Tipos de colocacion de sellos soportados por esta zona.
        /// </summary>
        [SerializeField] private List<SealPlacementType> supportedPlacementTypes = new List<SealPlacementType>
        {
            SealPlacementType.WallAttached
        };

        /// <summary>
        /// Maximo de objetos permitidos simultaneamente en esta zona.
        /// </summary>
        [SerializeField] private int maxObjectsInArea = 3;

        /// <summary>
        /// Distancia minima entre objetos colocados en esta zona.
        /// </summary>
        [SerializeField] private float minDistanceBetweenObjects = 2f;

        /// <summary>
        /// Permite colocacion en paredes.
        /// </summary>
        [SerializeField] private bool allowWallPlacement = true;

        /// <summary>
        /// Permite colocacion en piso.
        /// </summary>
        [SerializeField] private bool allowFloorPlacement = false;

        /// <summary>
        /// Permite colocacion en techo.
        /// </summary>
        [SerializeField] private bool allowCeilingPlacement = false;

        /// <summary>
        /// Indica si el area es alcanzable por jugadores.
        /// </summary>
        [SerializeField] private bool isAccessibleArea = true;

        /// <summary>
        /// Capas que se consideran superficies validas para raycasts.
        /// </summary>
        [SerializeField] private LayerMask surfaceLayerMask = Physics.DefaultRaycastLayers;

        /// <summary>
        /// Capas que bloquean espacio libre alrededor del spawn.
        /// </summary>
        [SerializeField] private LayerMask obstacleLayerMask = Physics.DefaultRaycastLayers;

        /// <summary>
        /// Capas usadas por validaciones de accesibilidad de jugador.
        /// </summary>
        [SerializeField] private LayerMask playerAccessibilityMask = Physics.DefaultRaycastLayers;

        /// <summary>
        /// Collider usado como volumen de la zona. Si no se asigna, se intenta resolver en el mismo GameObject.
        /// </summary>
        [SerializeField] private Collider areaCollider;

        /// <summary>
        /// Color base para Gizmos del area.
        /// </summary>
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.25f);

        /// <summary>
        /// Puntos candidatos generados para debug visual en editor.
        /// </summary>
        private readonly List<Vector3> debugCandidatePoints = new List<Vector3>();

        public string AreaId => areaId;
        public SpawnAreaType AreaType => areaType;
        public SurfaceType AllowedSurfaces => allowedSurfaces;
        public IReadOnlyList<SealPlacementType> SupportedPlacementTypes => supportedPlacementTypes;
        public int MaxObjectsInArea => Mathf.Max(1, maxObjectsInArea);
        public float MinDistanceBetweenObjects => Mathf.Max(0f, minDistanceBetweenObjects);
        public bool AllowWallPlacement => allowWallPlacement;
        public bool AllowFloorPlacement => allowFloorPlacement;
        public bool AllowCeilingPlacement => allowCeilingPlacement;
        public bool IsAccessibleArea => isAccessibleArea;
        public LayerMask SurfaceLayerMask => surfaceLayerMask;
        public LayerMask ObstacleLayerMask => obstacleLayerMask;
        public LayerMask PlayerAccessibilityMask => playerAccessibilityMask;

        /// <summary>
        /// Cachea collider y registra el area en el SpawnManager disponible.
        /// </summary>
        private void OnEnable()
        {
            areaCollider = areaCollider != null ? areaCollider : GetComponent<Collider>();
            SpawnManager spawnManager = ServiceLocator.Resolve<SpawnManager>() ?? FindAnyObjectByType<SpawnManager>();
            spawnManager?.RegisterSpawnArea(this);
        }

        /// <summary>
        /// Desregistra el area cuando sale de escena o se desactiva.
        /// </summary>
        private void OnDisable()
        {
            SpawnManager spawnManager = ServiceLocator.Resolve<SpawnManager>() ?? FindAnyObjectByType<SpawnManager>();
            spawnManager?.UnregisterSpawnArea(this);
        }

        /// <summary>
        /// Devuelve los bounds usados para buscar puntos dentro de la zona.
        /// </summary>
        /// <returns>Bounds del collider o un bounds pequeno alrededor del transform si no existe collider.</returns>
        public Bounds GetBounds()
        {
            areaCollider = areaCollider != null ? areaCollider : GetComponent<Collider>();
            return areaCollider != null
                ? areaCollider.bounds
                : new Bounds(transform.position, Vector3.one);
        }

        /// <summary>
        /// Verifica si esta zona puede alojar un sello concreto.
        /// </summary>
        /// <param name="sealDefinition">Definicion del sello solicitado.</param>
        /// <returns>True si la zona soporta el tipo de superficie y colocacion.</returns>
        public bool SupportsSeal(SealDefinition sealDefinition)
        {
            if (sealDefinition == null || !isAccessibleArea)
            {
                return false;
            }

            if (!SupportsPlacementType(sealDefinition.PlacementType))
            {
                return false;
            }

            if (!SurfaceDetector.IsSurfaceAllowed(sealDefinition.RequiredSurfaceType, allowedSurfaces))
            {
                return false;
            }

            return SupportsPlacementFlags(sealDefinition.PlacementType);
        }

        /// <summary>
        /// Genera un punto aleatorio dentro de los bounds del area.
        /// </summary>
        /// <param name="point">Punto candidato generado.</param>
        /// <returns>True si se pudo generar un punto.</returns>
        public bool TryGetRandomPoint(out Vector3 point)
        {
            Bounds bounds = GetBounds();

            if (bounds.size.sqrMagnitude <= 0.001f)
            {
                point = transform.position;
                return false;
            }

            point = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z));

            debugCandidatePoints.Add(point);

            if (debugCandidatePoints.Count > 24)
            {
                debugCandidatePoints.RemoveAt(0);
            }

            return true;
        }

        /// <summary>
        /// Determina si una posicion mundial cae dentro del volumen del area.
        /// </summary>
        /// <param name="position">Posicion mundial a evaluar.</param>
        /// <returns>True si la posicion pertenece a los bounds de la zona.</returns>
        public bool Contains(Vector3 position)
        {
            return GetBounds().Contains(position);
        }

        /// <summary>
        /// Genera un punto candidato desde el menu contextual para revisar Gizmos.
        /// </summary>
        [ContextMenu("Debug/Generate Candidate Point")]
        private void DebugGenerateCandidatePoint()
        {
            TryGetRandomPoint(out _);
        }

        /// <summary>
        /// Indica si el tipo de colocacion esta en la lista soportada.
        /// </summary>
        private bool SupportsPlacementType(SealPlacementType placementType)
        {
            return supportedPlacementTypes == null
                || supportedPlacementTypes.Count == 0
                || supportedPlacementTypes.Contains(placementType);
        }

        /// <summary>
        /// Valida flags especificos para pared, piso o techo.
        /// </summary>
        private bool SupportsPlacementFlags(SealPlacementType placementType)
        {
            switch (placementType)
            {
                case SealPlacementType.WallAttached:
                    return allowWallPlacement;
                case SealPlacementType.FloorPlaced:
                case SealPlacementType.FreeStanding:
                    return allowFloorPlacement;
                case SealPlacementType.CeilingHanging:
                    return allowCeilingPlacement;
                case SealPlacementType.SurfaceDrawing:
                    return allowWallPlacement || allowFloorPlacement || allowCeilingPlacement;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Dibuja el volumen del area y candidatos recientes para facilitar configuracion en escena.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Bounds bounds = GetBounds();
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = Color.yellow;

            foreach (Vector3 point in debugCandidatePoints)
            {
                Gizmos.DrawSphere(point, 0.08f);
            }
        }
    }
}
