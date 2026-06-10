using System.Collections.Generic;
using HitoriKakurembo.Seals;
using UnityEngine;
using UnityEngine.AI;

namespace HitoriKakurembo.Spawning
{
    /// <summary>
    /// Valida poses candidatas antes de que el servidor instancie objetos de gameplay.
    /// Revisa superficie, clearance, overlap, distancia a otros sellos y accesibilidad basica.
    /// </summary>
    public class SpawnValidator
    {
        /// <summary>
        /// Sellos ya registrados o spawneados que se usan para evitar cercania excesiva.
        /// </summary>
        private readonly IReadOnlyList<RitualSeal> existingSeals;

        /// <summary>
        /// Indica si debe intentarse validacion contra NavMesh.
        /// </summary>
        private readonly bool useNavMeshValidation;

        /// <summary>
        /// Radio de busqueda para NavMesh.SamplePosition.
        /// </summary>
        private readonly float navMeshSampleDistance;

        /// <summary>
        /// Mascara fallback para validar suelo o accesibilidad cuando no hay NavMesh.
        /// </summary>
        private readonly LayerMask fallbackAccessibilityMask;

        /// <summary>
        /// Mascara usada para validar obstaculos durante llamadas sin SpawnArea explicita.
        /// </summary>
        private readonly LayerMask defaultObstacleMask;

        /// <summary>
        /// Distancia minima adicional definida por el sistema que solicita el spawn.
        /// </summary>
        private readonly float globalMinDistanceBetweenSeals;

        /// <summary>
        /// Crea un validador de poses.
        /// </summary>
        public SpawnValidator(
            IReadOnlyList<RitualSeal> existingSeals,
            bool useNavMeshValidation,
            float navMeshSampleDistance,
            LayerMask fallbackAccessibilityMask,
            LayerMask defaultObstacleMask,
            float globalMinDistanceBetweenSeals = 0f)
        {
            this.existingSeals = existingSeals;
            this.useNavMeshValidation = useNavMeshValidation;
            this.navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
            this.fallbackAccessibilityMask = fallbackAccessibilityMask;
            this.defaultObstacleMask = defaultObstacleMask;
            this.globalMinDistanceBetweenSeals = Mathf.Max(0f, globalMinDistanceBetweenSeals);
        }

        /// <summary>
        /// Ejecuta todas las validaciones principales sobre una pose candidata de sello.
        /// </summary>
        /// <param name="definition">Definicion del sello a colocar.</param>
        /// <param name="pose">Pose candidata.</param>
        /// <param name="area">Area donde se encontro la pose.</param>
        /// <returns>True si la pose es segura para spawn.</returns>
        public bool IsValidSealPose(SealDefinition definition, Pose pose, SpawnArea area)
        {
            if (definition == null || area == null || !area.Contains(pose.position))
            {
                return false;
            }

            Vector3 normal = ExtractSurfaceNormal(pose, definition.PlacementType);

            if (!IsSurfaceValid(normal, definition.PlacementType))
            {
                return false;
            }

            SurfaceType detectedSurface = SurfaceDetector.ClassifySurface(normal);

            if (!SurfaceDetector.IsSurfaceAllowed(detectedSurface, definition.RequiredSurfaceType))
            {
                return false;
            }

            if (!HasEnoughClearance(pose, definition.VisualSize, area.ObstacleLayerMask))
            {
                return false;
            }

            if (!IsNotOverlapping(pose, definition.VisualSize, area.ObstacleLayerMask))
            {
                return false;
            }

            float minDistance = Mathf.Max(definition.MinDistanceFromOtherSeals, area.MinDistanceBetweenObjects, globalMinDistanceBetweenSeals);

            if (!IsNotTooCloseToOtherSeals(pose, minDistance))
            {
                return false;
            }

            return IsReachableByPlayer(pose, area.PlayerAccessibilityMask);
        }

        /// <summary>
        /// Valida si una normal corresponde al tipo de colocacion esperado.
        /// </summary>
        /// <param name="normal">Normal de superficie.</param>
        /// <param name="placementType">Tipo de colocacion del sello.</param>
        /// <returns>True si la normal coincide con pared, piso, techo o una configuracion aceptable.</returns>
        public bool IsSurfaceValid(Vector3 normal, SealPlacementType placementType)
        {
            SurfaceType surfaceType = SurfaceDetector.ClassifySurface(normal);

            switch (placementType)
            {
                case SealPlacementType.WallAttached:
                    return surfaceType == SurfaceType.Wall || surfaceType == SurfaceType.Inclined;
                case SealPlacementType.FloorPlaced:
                case SealPlacementType.FreeStanding:
                    return surfaceType == SurfaceType.Floor || surfaceType == SurfaceType.Inclined;
                case SealPlacementType.CeilingHanging:
                    return surfaceType == SurfaceType.Ceiling;
                case SealPlacementType.SurfaceDrawing:
                    return surfaceType != SurfaceType.None;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Verifica si existe espacio libre alrededor de la pose usando la mascara por defecto.
        /// </summary>
        public bool HasEnoughClearance(Pose pose, Vector3 size)
        {
            return HasEnoughClearance(pose, size, defaultObstacleMask);
        }

        /// <summary>
        /// Verifica si existe espacio libre alrededor de la pose usando una mascara especifica.
        /// </summary>
        public bool HasEnoughClearance(Pose pose, Vector3 size, LayerMask obstacleMask)
        {
            if (obstacleMask.value == 0)
            {
                return true;
            }

            Vector3 safeSize = SanitizeSize(size);
            Vector3 halfExtents = safeSize * 0.45f;
            Collider[] hits = Physics.OverlapBox(
                pose.position,
                halfExtents,
                pose.rotation,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            return hits == null || hits.Length == 0;
        }

        /// <summary>
        /// Revisa si el jugador podria acercarse a la pose.
        /// Usa NavMesh si esta habilitado y fallback con raycast hacia abajo si no hay NavMesh cercano.
        /// </summary>
        public bool IsReachableByPlayer(Pose pose)
        {
            return IsReachableByPlayer(pose, fallbackAccessibilityMask);
        }

        /// <summary>
        /// Revisa accesibilidad usando una mascara especifica para el area.
        /// </summary>
        public bool IsReachableByPlayer(Pose pose, LayerMask accessibilityMask)
        {
            if (useNavMeshValidation)
            {
                try
                {
                    if (NavMesh.SamplePosition(pose.position, out _, navMeshSampleDistance, NavMesh.AllAreas))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Si el proyecto no tiene NavMesh listo, se usa fallback fisico.
                }
            }

            int mask = accessibilityMask.value == 0 ? Physics.DefaultRaycastLayers : accessibilityMask.value;
            Vector3 origin = pose.position + Vector3.up * 1.5f;
            return Physics.Raycast(origin, Vector3.down, 3.5f, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Verifica que la pose no este dentro de colliders usando la mascara por defecto.
        /// </summary>
        public bool IsNotOverlapping(Pose pose, Vector3 size)
        {
            return IsNotOverlapping(pose, size, defaultObstacleMask);
        }

        /// <summary>
        /// Verifica que la pose no este dentro de colliders usando una mascara especifica.
        /// </summary>
        public bool IsNotOverlapping(Pose pose, Vector3 size, LayerMask obstacleMask)
        {
            return HasEnoughClearance(pose, size, obstacleMask);
        }

        /// <summary>
        /// Verifica que la pose no quede demasiado cerca de sellos existentes.
        /// </summary>
        /// <param name="pose">Pose candidata.</param>
        /// <param name="minDistance">Distancia minima aceptada.</param>
        /// <returns>True cuando respeta la separacion.</returns>
        public bool IsNotTooCloseToOtherSeals(Pose pose, float minDistance)
        {
            if (existingSeals == null || minDistance <= 0f)
            {
                return true;
            }

            float minSqrDistance = minDistance * minDistance;

            foreach (RitualSeal seal in existingSeals)
            {
                if (seal == null)
                {
                    continue;
                }

                if ((seal.transform.position - pose.position).sqrMagnitude < minSqrDistance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Extrae una normal de superficie desde la rotacion de la pose segun el tipo de colocacion.
        /// </summary>
        private static Vector3 ExtractSurfaceNormal(Pose pose, SealPlacementType placementType)
        {
            switch (placementType)
            {
                case SealPlacementType.WallAttached:
                    return pose.rotation * Vector3.back;
                case SealPlacementType.CeilingHanging:
                    return pose.rotation * Vector3.down;
                case SealPlacementType.FloorPlaced:
                case SealPlacementType.FreeStanding:
                case SealPlacementType.SurfaceDrawing:
                    return pose.rotation * Vector3.up;
                default:
                    return Vector3.up;
            }
        }

        /// <summary>
        /// Evita tamanos cero o negativos en OverlapBox.
        /// </summary>
        private static Vector3 SanitizeSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(0.05f, Mathf.Abs(size.x)),
                Mathf.Max(0.05f, Mathf.Abs(size.y)),
                Mathf.Max(0.05f, Mathf.Abs(size.z)));
        }
    }
}
