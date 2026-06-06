using System.Collections.Generic;
using UnityEngine;

namespace HitoriKakurembo.Doll
{
    /// <summary>
    /// Enumera las habilidades basicas del muneco previstas para el prototipo.
    /// </summary>
    public enum DollAbilityType
    {
        /// <summary>
        /// Indica que no existe una habilidad asignada.
        /// </summary>
        None = 0,

        /// <summary>
        /// Habilidad de desplazamiento mediante espejos.
        /// </summary>
        MirrorTeleport = 1,

        /// <summary>
        /// Habilidad de deteccion o pulso de presencia.
        /// </summary>
        DetectionPulse = 2,

        /// <summary>
        /// Habilidad para colocar trampas en el entorno.
        /// </summary>
        TrapPlacement = 3
    }

    /// <summary>
    /// Gestiona el conjunto de habilidades actualmente desbloqueadas para el muneco.
    /// </summary>
    public class DollAbilityManager : MonoBehaviour
    {
        /// <summary>
        /// Lista de habilidades habilitadas para el muneco al inicio o durante la partida.
        /// </summary>
        [SerializeField] private List<DollAbilityType> unlockedAbilities = new List<DollAbilityType>
        {
            DollAbilityType.MirrorTeleport
        };

        /// <summary>
        /// Obtiene la vista de solo lectura de las habilidades desbloqueadas.
        /// </summary>
        public IReadOnlyList<DollAbilityType> UnlockedAbilities => unlockedAbilities;

        /// <summary>
        /// Determina si la habilidad solicitada ya se encuentra desbloqueada.
        /// </summary>
        /// <param name="abilityType">
        /// Habilidad que se desea consultar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la habilidad esta disponible; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool HasAbility(DollAbilityType abilityType)
        {
            return unlockedAbilities.Contains(abilityType);
        }

        /// <summary>
        /// Agrega una nueva habilidad al conjunto desbloqueado cuando aun no existe.
        /// </summary>
        /// <param name="abilityType">
        /// Habilidad que se desea desbloquear.
        /// </param>
        public void UnlockAbility(DollAbilityType abilityType)
        {
            if (abilityType == DollAbilityType.None || unlockedAbilities.Contains(abilityType))
            {
                return;
            }

            unlockedAbilities.Add(abilityType);
        }

        /// <summary>
        /// Elimina todas las habilidades desbloqueadas del muneco.
        /// </summary>
        public void ResetAbilities()
        {
            unlockedAbilities.Clear();
        }
    }
}
