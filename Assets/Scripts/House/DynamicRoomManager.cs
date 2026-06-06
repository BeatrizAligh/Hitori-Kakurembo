using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HitoriKakurembo.House
{
    /// <summary>
    /// Controla la activacion y consulta de habitaciones dinamicas del mapa.
    /// </summary>
    public class DynamicRoomManager : MonoBehaviour
    {
        /// <summary>
        /// Lista de habitaciones dinamicas gestionadas por el manager.
        /// </summary>
        [SerializeField] private List<GameObject> dynamicRooms = new List<GameObject>();

        /// <summary>
        /// Obtiene la vista de solo lectura de habitaciones gestionadas.
        /// </summary>
        public IReadOnlyList<GameObject> DynamicRooms => dynamicRooms;

        /// <summary>
        /// Activa o desactiva una habitacion por indice.
        /// </summary>
        /// <param name="index">
        /// Posicion de la habitacion en la lista gestionada.
        /// </param>
        /// <param name="isActive">
        /// Estado que debe aplicarse a la habitacion.
        /// </param>
        public void SetRoomActive(int index, bool isActive)
        {
            if (index < 0 || index >= dynamicRooms.Count || dynamicRooms[index] == null)
            {
                return;
            }

            dynamicRooms[index].SetActive(isActive);
        }

        /// <summary>
        /// Obtiene una lista de habitaciones que actualmente se encuentran activas.
        /// </summary>
        /// <returns>
        /// Lista de habitaciones activas.
        /// </returns>
        public List<GameObject> GetActiveRooms()
        {
            return dynamicRooms.Where(room => room != null && room.activeSelf).ToList();
        }

        /// <summary>
        /// Cuenta cuantas habitaciones dinamicas se encuentran activas.
        /// </summary>
        /// <returns>
        /// Numero de habitaciones activas.
        /// </returns>
        public int GetActiveRoomCount()
        {
            return GetActiveRooms().Count;
        }
    }
}
