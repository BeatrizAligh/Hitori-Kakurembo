using System.Collections.Generic;
using HitoriKakurembo.Core;
using UnityEngine;

namespace HitoriKakurembo.Ritual
{
    /// <summary>
    /// Controla la fase ritual global y el conjunto de elementos requeridos para completar la preparacion.
    /// </summary>
    public class RitualManager : MonoBehaviour
    {
        /// <summary>
        /// Fase ritual actual expuesta al resto de sistemas.
        /// </summary>
        [SerializeField] private RitualPhase currentPhase = RitualPhase.Preparation;

        /// <summary>
        /// Coleccion interna de items rituales conocidos por la escena actual.
        /// </summary>
        private readonly List<RitualItem> ritualItems = new List<RitualItem>();

        /// <summary>
        /// Obtiene la fase ritual actual.
        /// </summary>
        public RitualPhase CurrentPhase => currentPhase;

        /// <summary>
        /// Obtiene la vista de solo lectura de los items rituales registrados.
        /// </summary>
        public IReadOnlyList<RitualItem> RitualItems => ritualItems;

        /// <summary>
        /// Obtiene un valor que indica si todos los items rituales registrados ya fueron completados.
        /// </summary>
        public bool IsRitualReady => ritualItems.Count > 0 && ritualItems.TrueForAll(item => item != null && item.IsCollected);

        /// <summary>
        /// Registra este manager en el localizador de servicios y recopila los items rituales presentes en la escena.
        /// </summary>
        private void Awake()
        {
            ServiceLocator.Register<RitualManager>(this);

            foreach (RitualItem item in FindObjectsByType<RitualItem>())
            {
                RegisterItem(item);
            }
        }

        /// <summary>
        /// Agrega un item ritual al seguimiento del manager cuando aun no se encuentra registrado.
        /// </summary>
        /// <param name="item">
        /// Item ritual que debe quedar bajo seguimiento.
        /// </param>
        public void RegisterItem(RitualItem item)
        {
            if (item != null && !ritualItems.Contains(item))
            {
                ritualItems.Add(item);
            }
        }

        /// <summary>
        /// Elimina un item ritual del seguimiento del manager.
        /// </summary>
        /// <param name="item">
        /// Item ritual que debe ser removido.
        /// </param>
        public void UnregisterItem(RitualItem item)
        {
            if (item != null)
            {
                ritualItems.Remove(item);
            }
        }

        /// <summary>
        /// Fuerza la fase actual del ritual al estado de preparacion.
        /// </summary>
        public void SetPreparationPhase()
        {
            currentPhase = RitualPhase.Preparation;
        }

        /// <summary>
        /// Fuerza la fase actual del ritual al estado de ritual activo.
        /// </summary>
        public void SetRitualPhase()
        {
            currentPhase = RitualPhase.Ritual;
        }

        /// <summary>
        /// Fuerza la fase actual del ritual al estado de caceria.
        /// </summary>
        public void SetHuntPhase()
        {
            currentPhase = RitualPhase.Hunt;
        }

        /// <summary>
        /// Avanza la fase ritual al siguiente estado del flujo principal.
        /// </summary>
        public void AdvancePhase()
        {
            switch (currentPhase)
            {
                case RitualPhase.Preparation:
                    SetRitualPhase();
                    break;
                case RitualPhase.Ritual:
                    SetHuntPhase();
                    break;
                default:
                    SetPreparationPhase();
                    break;
            }
        }
    }
}
