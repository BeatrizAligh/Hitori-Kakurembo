using System;
using System.Collections.Generic;
using HitoriKakurembo.Player;
using UnityEngine;

namespace HitoriKakurembo.Doll
{
    /// <summary>
    /// Agrupa los subsistemas principales del muneco y expone una fachada de alto nivel para gameplay.
    /// </summary>
    public class DollController : MonoBehaviour
    {
        /// <summary>
        /// Administrador de habilidades disponibles para el muneco.
        /// </summary>
        [SerializeField] private DollAbilityManager abilityManager;

        /// <summary>
        /// Administrador de trampas activas colocadas por el muneco.
        /// </summary>
        [SerializeField] private DollTrapManager trapManager;

        /// <summary>
        /// Sistema responsable del desplazamiento del muneco entre espejos.
        /// </summary>
        [SerializeField] private DollMirrorTeleport mirrorTeleport;

        /// <summary>
        /// Sistema responsable de detectar jugadores cercanos al muneco.
        /// </summary>
        [SerializeField] private DollDetectionSystem detectionSystem;

        /// <summary>
        /// Resuelve automaticamente referencias locales cuando no fueron asignadas desde el inspector.
        /// </summary>
        private void Awake()
        {
            abilityManager = abilityManager != null ? abilityManager : GetComponent<DollAbilityManager>();
            trapManager = trapManager != null ? trapManager : GetComponent<DollTrapManager>();
            mirrorTeleport = mirrorTeleport != null ? mirrorTeleport : GetComponent<DollMirrorTeleport>();
            detectionSystem = detectionSystem != null ? detectionSystem : GetComponent<DollDetectionSystem>();
        }

        /// <summary>
        /// Determina si el muneco dispone de la habilidad solicitada.
        /// </summary>
        /// <param name="abilityType">
        /// Habilidad que se desea consultar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando la habilidad esta desbloqueada; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool HasAbility(DollAbilityType abilityType)
        {
            return abilityManager != null && abilityManager.HasAbility(abilityType);
        }

        /// <summary>
        /// Obtiene la lista de jugadores detectados actualmente por el sistema de deteccion del muneco.
        /// </summary>
        /// <returns>
        /// Coleccion de jugadores detectados, o una coleccion vacia cuando no existe sistema de deteccion.
        /// </returns>
        public IReadOnlyList<PlayerController> GetNearbyPlayers()
        {
            return detectionSystem != null
                ? detectionSystem.GetDetectedPlayers()
                : Array.Empty<PlayerController>();
        }

        /// <summary>
        /// Activa o desactiva el controlador principal del muneco y sus sistemas asociados.
        /// </summary>
        /// <param name="value">
        /// Estado que debe aplicarse al controlador.
        /// </param>
        public void SetDollActive(bool value)
        {
            enabled = value;

            if (detectionSystem != null)
            {
                detectionSystem.enabled = value;
            }
        }
    }
}
