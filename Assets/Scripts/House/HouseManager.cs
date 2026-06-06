using HitoriKakurembo.Core;
using UnityEngine;

namespace HitoriKakurembo.House
{
    /// <summary>
    /// Centraliza el acceso a los subsistemas principales del mapa o casa de la partida.
    /// </summary>
    public class HouseManager : MonoBehaviour
    {
        /// <summary>
        /// Coleccion de espejos relevantes del mapa.
        /// </summary>
        [SerializeField] private MirrorPortal[] mirrorPortals = System.Array.Empty<MirrorPortal>();

        /// <summary>
        /// Referencia al sistema que evalua zonas visibles o seguras.
        /// </summary>
        [SerializeField] private SafeZoneManager safeZoneManager = null;

        /// <summary>
        /// Referencia al sistema que controla habitaciones dinamicas.
        /// </summary>
        [SerializeField] private DynamicRoomManager dynamicRoomManager = null;

        /// <summary>
        /// Registra este manager en el localizador de servicios y resuelve referencias faltantes desde la escena.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<HouseManager>(this);
            safeZoneManager = safeZoneManager != null ? safeZoneManager : FindAnyObjectByType<SafeZoneManager>();
            dynamicRoomManager = dynamicRoomManager != null ? dynamicRoomManager : FindAnyObjectByType<DynamicRoomManager>();
        }

        /// <summary>
        /// Devuelve el conjunto de espejos registrados para la casa.
        /// </summary>
        /// <returns>
        /// Arreglo de espejos configurados en el manager.
        /// </returns>
        public MirrorPortal[] GetMirrorPortals()
        {
            return mirrorPortals;
        }

        /// <summary>
        /// Determina si el muneco se encuentra dentro de una zona visible del mapa.
        /// </summary>
        /// <param name="dollTransform">
        /// Transform del muneco que se desea evaluar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el muneco esta dentro de la zona visible configurada; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool IsDollInVisibleZone(Transform dollTransform)
        {
            return safeZoneManager != null && safeZoneManager.IsDollInsideVisibleZone(dollTransform);
        }

        /// <summary>
        /// Devuelve el administrador de habitaciones dinamicas asociado a la casa.
        /// </summary>
        /// <returns>
        /// Referencia al administrador de habitaciones dinamicas, o <see langword="null"/> si no existe.
        /// </returns>
        public DynamicRoomManager GetDynamicRoomManager()
        {
            return dynamicRoomManager;
        }
    }
}
