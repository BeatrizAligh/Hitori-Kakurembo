using System.Collections.Generic;
using System.Linq;
using HitoriKakurembo.Items;
using UnityEngine;

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Gestiona el inventario basico del jugador y sus operaciones de alta, baja y consulta de items.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        /// <summary>
        /// Capacidad maxima del inventario para esta fase del prototipo.
        /// </summary>
        [SerializeField] private int capacity = 3;

        /// <summary>
        /// Lista interna de items actualmente almacenados por el jugador.
        /// </summary>
        private readonly List<ItemBase> items = new List<ItemBase>();

        /// <summary>
        /// Obtiene la vista de solo lectura de los items almacenados.
        /// </summary>
        public IReadOnlyList<ItemBase> Items => items;

        /// <summary>
        /// Intenta agregar un item al inventario.
        /// </summary>
        /// <param name="item">
        /// Item que se desea almacenar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el item fue agregado correctamente; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool AddItem(ItemBase item)
        {
            if (item == null || items.Count >= capacity)
            {
                return false;
            }

            items.Add(item);
            return true;
        }

        /// <summary>
        /// Intenta remover un item del inventario.
        /// </summary>
        /// <param name="item">
        /// Item que se desea eliminar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el item existia y fue removido; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool RemoveItem(ItemBase item)
        {
            return item != null && items.Remove(item);
        }

        /// <summary>
        /// Determina si el inventario contiene al menos un item del tipo solicitado.
        /// </summary>
        /// <param name="itemType">
        /// Tipo de item que se desea buscar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando existe un item del tipo solicitado; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool HasItemType(ItemType itemType)
        {
            return items.Any(item => item != null && item.Type == itemType);
        }
    }
}
