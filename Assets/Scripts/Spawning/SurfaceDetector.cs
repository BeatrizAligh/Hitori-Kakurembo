using HitoriKakurembo.Seals;
using UnityEngine;

namespace HitoriKakurembo.Spawning
{
    /// <summary>
    /// Resultado de una deteccion fisica de superficie.
    /// Contiene el punto, la normal, el tipo clasificado y el collider impactado.
    /// </summary>
    public readonly struct SurfaceHit
    {
        /// <summary>
        /// Punto exacto de contacto del raycast.
        /// </summary>
        public readonly Vector3 Point;

        /// <summary>
        /// Normal fisica de la superficie detectada.
        /// </summary>
        public readonly Vector3 Normal;

        /// <summary>
        /// Tipo de superficie inferido desde la normal.
        /// </summary>
        public readonly SurfaceType SurfaceType;

        /// <summary>
        /// Collider que recibio el impacto.
        /// </summary>
        public readonly Collider Collider;

        /// <summary>
        /// Crea un resultado de superficie inmutable.
        /// </summary>
        public SurfaceHit(Vector3 point, Vector3 normal, SurfaceType surfaceType, Collider collider)
        {
            Point = point;
            Normal = normal;
            SurfaceType = surfaceType;
            Collider = collider;
        }
    }

    /// <summary>
    /// Utilidad de fisica para encontrar y clasificar superficies validas de spawn.
    /// Centraliza reglas de normales para piso, pared, techo e inclinaciones.
    /// </summary>
    public static class SurfaceDetector
    {
        /// <summary>
        /// Minimo dot contra Vector3.up para considerar una superficie como piso.
        /// </summary>
        private const float FloorDotThreshold = 0.72f;

        /// <summary>
        /// Maximo dot absoluto contra Vector3.up para considerar una superficie como pared.
        /// </summary>
        private const float WallAbsDotThreshold = 0.35f;

        /// <summary>
        /// Distancia por defecto para buscar superficies cercanas dentro de una zona.
        /// </summary>
        private const float DefaultSurfaceSearchDistance = 8f;

        /// <summary>
        /// Direcciones horizontales usadas para buscar paredes alrededor de un punto candidato.
        /// </summary>
        private static readonly Vector3[] HorizontalDirections =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left,
            new Vector3(1f, 0f, 1f).normalized,
            new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized,
            new Vector3(-1f, 0f, -1f).normalized
        };

        /// <summary>
        /// Intenta detectar una superficie apropiada para el tipo de colocacion solicitado.
        /// </summary>
        /// <param name="candidatePoint">Punto base dentro de una SpawnArea.</param>
        /// <param name="placementType">Tipo de colocacion del sello.</param>
        /// <param name="surfaceLayerMask">LayerMask de superficies validas. Si esta vacia, se usa DefaultRaycastLayers.</param>
        /// <param name="areaBounds">Bounds de la zona para calcular origenes seguros.</param>
        /// <param name="hit">Resultado de superficie detectada.</param>
        /// <returns>True si se encontro una superficie compatible.</returns>
        public static bool TryDetectSurface(
            Vector3 candidatePoint,
            SealPlacementType placementType,
            LayerMask surfaceLayerMask,
            Bounds areaBounds,
            out SurfaceHit hit)
        {
            int mask = surfaceLayerMask.value == 0 ? Physics.DefaultRaycastLayers : surfaceLayerMask.value;
            float searchDistance = Mathf.Max(DefaultSurfaceSearchDistance, areaBounds.extents.magnitude + 0.5f);

            switch (placementType)
            {
                case SealPlacementType.WallAttached:
                    return TryDetectWall(candidatePoint, searchDistance, mask, out hit);
                case SealPlacementType.CeilingHanging:
                    return TryCast(candidatePoint + Vector3.down * 0.25f, Vector3.up, searchDistance, mask, out hit);
                case SealPlacementType.FloorPlaced:
                case SealPlacementType.FreeStanding:
                case SealPlacementType.SurfaceDrawing:
                    return TryCast(candidatePoint + Vector3.up * areaBounds.extents.y, Vector3.down, searchDistance, mask, out hit);
                default:
                    return TryCast(candidatePoint + Vector3.up * areaBounds.extents.y, Vector3.down, searchDistance, mask, out hit);
            }
        }

        /// <summary>
        /// Crea una pose orientada para que el sello quede apoyado o adherido a la superficie detectada.
        /// </summary>
        /// <param name="hit">Superficie detectada.</param>
        /// <param name="placementType">Tipo de colocacion requerido.</param>
        /// <param name="surfaceOffset">Separacion minima para evitar z-fighting o interseccion con la superficie.</param>
        /// <returns>Pose mundial final sugerida.</returns>
        public static Pose CreatePose(SurfaceHit hit, SealPlacementType placementType, float surfaceOffset = 0.025f)
        {
            Vector3 normal = hit.Normal.sqrMagnitude > 0.001f ? hit.Normal.normalized : Vector3.up;
            Vector3 position = hit.Point + normal * surfaceOffset;
            Quaternion rotation;

            switch (placementType)
            {
                case SealPlacementType.WallAttached:
                    rotation = Quaternion.LookRotation(-normal, Vector3.up);
                    break;
                case SealPlacementType.CeilingHanging:
                    rotation = Quaternion.FromToRotation(Vector3.down, normal);
                    break;
                case SealPlacementType.FloorPlaced:
                case SealPlacementType.FreeStanding:
                case SealPlacementType.SurfaceDrawing:
                    rotation = Quaternion.FromToRotation(Vector3.up, normal);
                    break;
                default:
                    rotation = Quaternion.identity;
                    break;
            }

            return new Pose(position, rotation);
        }

        /// <summary>
        /// Clasifica una normal en pared, piso, techo o inclinacion.
        /// </summary>
        /// <param name="normal">Normal fisica recibida de raycast.</param>
        /// <returns>Tipo de superficie inferido.</returns>
        public static SurfaceType ClassifySurface(Vector3 normal)
        {
            if (normal.sqrMagnitude <= 0.001f)
            {
                return SurfaceType.None;
            }

            Vector3 normalized = normal.normalized;
            float upDot = Vector3.Dot(normalized, Vector3.up);

            if (upDot >= FloorDotThreshold)
            {
                return SurfaceType.Floor;
            }

            if (upDot <= -FloorDotThreshold)
            {
                return SurfaceType.Ceiling;
            }

            if (Mathf.Abs(upDot) <= WallAbsDotThreshold)
            {
                return SurfaceType.Wall;
            }

            return SurfaceType.Inclined;
        }

        /// <summary>
        /// Verifica si una superficie clasificada esta incluida en una mascara de tipos permitidos.
        /// </summary>
        /// <param name="surfaceType">Superficie detectada.</param>
        /// <param name="allowedSurfaces">Superficies aceptadas.</param>
        /// <returns>True cuando la superficie esta permitida.</returns>
        public static bool IsSurfaceAllowed(SurfaceType surfaceType, SurfaceType allowedSurfaces)
        {
            if (surfaceType == SurfaceType.None || allowedSurfaces == SurfaceType.None)
            {
                return false;
            }

            return allowedSurfaces == SurfaceType.Any || (allowedSurfaces & surfaceType) != 0;
        }

        /// <summary>
        /// Busca una pared cercana usando direcciones horizontales alrededor del punto candidato.
        /// </summary>
        private static bool TryDetectWall(Vector3 candidatePoint, float searchDistance, int mask, out SurfaceHit hit)
        {
            Vector3 origin = candidatePoint + Vector3.up * 1.25f;

            foreach (Vector3 direction in HorizontalDirections)
            {
                if (!TryCast(origin, direction, searchDistance, mask, out SurfaceHit candidateHit))
                {
                    continue;
                }

                if (candidateHit.SurfaceType == SurfaceType.Wall || candidateHit.SurfaceType == SurfaceType.Inclined)
                {
                    hit = candidateHit;
                    return true;
                }
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// Ejecuta un raycast y empaqueta el resultado en SurfaceHit.
        /// </summary>
        private static bool TryCast(Vector3 origin, Vector3 direction, float distance, int mask, out SurfaceHit hit)
        {
            if (Physics.Raycast(origin, direction.normalized, out RaycastHit raycastHit, distance, mask, QueryTriggerInteraction.Ignore))
            {
                SurfaceType surfaceType = ClassifySurface(raycastHit.normal);
                hit = new SurfaceHit(raycastHit.point, raycastHit.normal, surfaceType, raycastHit.collider);
                return true;
            }

            hit = default;
            return false;
        }
    }
}
