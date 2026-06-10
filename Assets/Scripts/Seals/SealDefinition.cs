using HitoriKakurembo.Spawning;
using UnityEngine;

namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Define los datos configurables de un sello ritual.
    /// Este asset no ejecuta gameplay: solo describe como se instancia, donde puede colocarse y que reglas basicas debe respetar.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Seals/Seal Definition")]
    public class SealDefinition : ScriptableObject
    {
        /// <summary>
        /// Identificador estable usado por guardado, debug o configuraciones de ronda.
        /// </summary>
        [SerializeField] private string sealId = "seal_talisman";

        /// <summary>
        /// Nombre legible para UI, logs o herramientas de debug.
        /// </summary>
        [SerializeField] private string displayName = "Ritual Seal";

        /// <summary>
        /// Descripcion corta para documentar la intencion del sello.
        /// </summary>
        [TextArea]
        [SerializeField] private string description = "A ritual seal used by survivors.";

        /// <summary>
        /// Prefab de red que debe instanciar el servidor.
        /// Debe contener NetworkObject y RitualSeal.
        /// </summary>
        [SerializeField] private GameObject sealPrefab;

        /// <summary>
        /// Tipo conceptual del sello.
        /// </summary>
        [SerializeField] private SealKind sealKind = SealKind.Talisman;

        /// <summary>
        /// Forma de colocacion requerida para orientar el prefab.
        /// </summary>
        [SerializeField] private SealPlacementType placementType = SealPlacementType.WallAttached;

        /// <summary>
        /// Superficie fisica requerida para colocar este sello.
        /// </summary>
        [SerializeField] private SurfaceType requiredSurfaceType = SurfaceType.Wall;

        /// <summary>
        /// Tamano aproximado usado para validaciones de overlap y separacion.
        /// </summary>
        [SerializeField] private Vector3 visualSize = new Vector3(0.45f, 0.65f, 0.05f);

        /// <summary>
        /// Tiempo requerido para completar activacion por supervivientes.
        /// </summary>
        [SerializeField] private float activationDuration = 1.5f;

        /// <summary>
        /// Tiempo requerido para completar desactivacion por el muneco.
        /// </summary>
        [SerializeField] private float deactivationDuration = 1.5f;

        /// <summary>
        /// Distancia minima respecto a otros sellos activos o candidatos.
        /// </summary>
        [SerializeField] private float minDistanceFromOtherSeals = 2f;

        /// <summary>
        /// Indica si se requiere linea de vision desde el jugador para activar.
        /// La validacion concreta puede vivir en sistemas de interaccion futuros.
        /// </summary>
        [SerializeField] private bool requiresLineOfSight = true;

        /// <summary>
        /// Permite que supervivientes activen este sello.
        /// </summary>
        [SerializeField] private bool canBeActivatedBySurvivor = true;

        /// <summary>
        /// Permite que el muneco desactive este sello.
        /// </summary>
        [SerializeField] private bool canBeDeactivatedByDoll = true;

        /// <summary>
        /// Permite que el sello pase a estado corrompido.
        /// </summary>
        [SerializeField] private bool canBeCorrupted = true;

        /// <summary>
        /// Permite que un sello corrompido o desactivado vuelva a activarse.
        /// </summary>
        [SerializeField] private bool canBeReactivated = true;

        /// <summary>
        /// Sonido reproducido cuando el sello se activa.
        /// </summary>
        [SerializeField] private AudioClip activationSound;

        /// <summary>
        /// Sonido reproducido cuando el sello se desactiva o corrompe.
        /// </summary>
        [SerializeField] private AudioClip deactivationSound;

        /// <summary>
        /// VFX opcional para activacion.
        /// </summary>
        [SerializeField] private GameObject activationVfx;

        /// <summary>
        /// VFX opcional para desactivacion o corrupcion.
        /// </summary>
        [SerializeField] private GameObject deactivationVfx;

        public string SealId => sealId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? sealId : displayName;
        public string Description => description;
        public GameObject SealPrefab => sealPrefab;
        public SealKind SealKind => sealKind;
        public SealPlacementType PlacementType => placementType;
        public SurfaceType RequiredSurfaceType => requiredSurfaceType;
        public Vector3 VisualSize => visualSize;
        public float ActivationDuration => Mathf.Max(0f, activationDuration);
        public float DeactivationDuration => Mathf.Max(0f, deactivationDuration);
        public float MinDistanceFromOtherSeals => Mathf.Max(0f, minDistanceFromOtherSeals);
        public bool RequiresLineOfSight => requiresLineOfSight;
        public bool CanBeActivatedBySurvivor => canBeActivatedBySurvivor;
        public bool CanBeDeactivatedByDoll => canBeDeactivatedByDoll;
        public bool CanBeCorrupted => canBeCorrupted;
        public bool CanBeReactivated => canBeReactivated;
        public AudioClip ActivationSound => activationSound;
        public AudioClip DeactivationSound => deactivationSound;
        public GameObject ActivationVfx => activationVfx;
        public GameObject DeactivationVfx => deactivationVfx;
    }
}
