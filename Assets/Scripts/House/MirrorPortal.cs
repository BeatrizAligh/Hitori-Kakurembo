using UnityEngine;

namespace HitoriKakurembo.House
{
    /// <summary>
    /// Representa un espejo del mapa que puede enlazarse con otro espejo para teletransporte.
    /// </summary>
    public class MirrorPortal : MonoBehaviour
    {
        /// <summary>
        /// Indice estable del espejo dentro del mapa; permite que servidor y clientes identifiquen el mismo portal.
        /// </summary>
        [SerializeField] private int portalIndex = -1;

        /// <summary>
        /// Portal de destino al que este espejo conduce.
        /// </summary>
        [SerializeField] private MirrorPortal linkedPortal = null;

        /// <summary>
        /// Obtiene el indice estable asignado al espejo.
        /// </summary>
        public int PortalIndex => portalIndex;

        /// <summary>
        /// Obtiene el portal enlazado configurado como destino.
        /// </summary>
        public MirrorPortal LinkedPortal => linkedPortal;

        /// <summary>
        /// Asigna programaticamente el indice estable del espejo durante la composicion de escena.
        /// </summary>
        /// <param name="index">
        /// Indice logico que identifica este espejo dentro del conjunto de portales.
        /// </param>
        public void SetPortalIndex(int index)
        {
            portalIndex = Mathf.Max(0, index);
        }

        /// <summary>
        /// Enlaza este espejo con otro portal de salida validado por el mapa.
        /// </summary>
        /// <param name="portal">
        /// Portal que actuara como destino cuando el muneco use este espejo.
        /// </param>
        public void SetLinkedPortal(MirrorPortal portal)
        {
            linkedPortal = portal;
        }

        /// <summary>
        /// Intenta devolver el portal enlazado actualmente configurado.
        /// </summary>
        /// <param name="portal">
        /// Cuando este metodo retorna, contiene el portal enlazado si existe; en caso contrario, <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando existe un portal enlazado; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool TryGetLinkedPortal(out MirrorPortal portal)
        {
            portal = linkedPortal;
            return portal != null;
        }

        /// <summary>
        /// Obtiene la posicion de salida asociada a este portal.
        /// </summary>
        /// <returns>
        /// Posicion del portal enlazado cuando existe; de lo contrario, la posicion del portal actual.
        /// </returns>
        public Vector3 GetExitPosition()
        {
            return linkedPortal != null ? linkedPortal.transform.position : transform.position;
        }

        /// <summary>
        /// Obtiene la rotacion de salida asociada al portal enlazado.
        /// </summary>
        /// <returns>
        /// Rotacion del portal enlazado cuando existe; de lo contrario, la rotacion del portal actual.
        /// </returns>
        public Quaternion GetExitRotation()
        {
            return linkedPortal != null ? linkedPortal.transform.rotation : transform.rotation;
        }
    }
}
