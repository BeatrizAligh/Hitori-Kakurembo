using HitoriKakurembo.Doll;
using HitoriKakurembo.Network;
using HitoriKakurembo.Rounds;
using HitoriKakurembo.Seals;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Gestiona interacciones locales del jugador propietario y delega las validaciones importantes al servidor.
    /// </summary>
    public class PlayerInteraction : NetworkBehaviour
    {
        /// <summary>
        /// Nombre canonico de la escena donde se permiten interacciones de gameplay.
        /// </summary>
        private const string GameSceneName = "GameScene";

        /// <summary>
        /// Distancia maxima a la que una interaccion se considera valida localmente para elegir candidato.
        /// </summary>
        [SerializeField] private float interactionRange = 3f;

        /// <summary>
        /// Distancia maxima local usada para elegir un superviviente candidato cuando el muneco intenta eliminar.
        /// </summary>
        [SerializeField] private float dollAttackRange = 2.4f;

        /// <summary>
        /// Distancia maxima local usada para elegir al muneco vulnerable durante la accion ritual final.
        /// </summary>
        [SerializeField] private float dollExorcismRange = 2.8f;

        /// <summary>
        /// Obtiene la distancia maxima de interaccion configurada.
        /// </summary>
        public float InteractionRange => interactionRange;

        /// <summary>
        /// Obtiene la distancia maxima local de ataque configurada para el muneco.
        /// </summary>
        public float DollAttackRange => dollAttackRange;

        /// <summary>
        /// Obtiene la distancia maxima local para intentar exorcizar al muneco vulnerable.
        /// </summary>
        public float DollExorcismRange => dollExorcismRange;

        /// <summary>
        /// Lee input local y envia solicitudes de interaccion solo desde el jugador propietario.
        /// </summary>
        private void Update()
        {
            if (!IsOwner || SceneManager.GetActiveScene().name != GameSceneName)
            {
                return;
            }

            if (WasInteractPressedThisFrame())
            {
                TryActivateNearestSeal();
            }

            if (WasDollAttackPressedThisFrame())
            {
                if (!TryRequestDollAttack())
                {
                    TryRequestDollExorcism();
                }
            }

            if (WasMirrorTeleportPressedThisFrame())
            {
                TryRequestMirrorTeleport();
            }

            if (WasTrapPlacementPressedThisFrame())
            {
                TryRequestTrapPlacement();
            }
        }

        /// <summary>
        /// Determina si el objetivo indicado se encuentra dentro del rango de interaccion.
        /// </summary>
        /// <param name="target">
        /// Transform del objetivo que se desea evaluar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el objetivo existe y esta dentro del rango permitido; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool CanInteractWith(Transform target)
        {
            return target != null && Vector3.Distance(transform.position, target.position) <= interactionRange;
        }

        /// <summary>
        /// Intenta ejecutar una interaccion generica sobre el objeto indicado.
        /// </summary>
        /// <param name="target">
        /// Objeto con el que se desea interactuar.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el objetivo es valido y esta dentro del rango; en caso contrario, <see langword="false"/>.
        /// </returns>
        public bool TryInteract(GameObject target)
        {
            if (target == null || !CanInteractWith(target.transform))
            {
                return false;
            }

            Debug.Log($"{name} interacted with {target.name}.");
            return true;
        }

        /// <summary>
        /// Busca el sello interactivo mas cercano y solicita su activacion al servidor.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se encontro un sello candidato y se envio la solicitud.
        /// </returns>
        private bool TryActivateNearestSeal()
        {
            SealActivationZone nearestZone = FindNearestSealActivationZone();

            if (nearestZone == null || nearestZone.TargetSeal == null)
            {
                return false;
            }

            SealManager sealManager = FindAnyObjectByType<SealManager>();

            if (sealManager == null)
            {
                return false;
            }

            sealManager.RequestActivateSealFromLocalPlayer(nearestZone.TargetSeal.SealIndex);
            return true;
        }

        /// <summary>
        /// Busca el superviviente vivo mas cercano y solicita al servidor que valide la eliminacion del muneco.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se encontro un objetivo candidato y se envio la solicitud.
        /// </returns>
        private bool TryRequestDollAttack()
        {
            NetworkPlayer localPlayer = GetComponent<NetworkPlayer>();

            if (localPlayer == null || !localPlayer.IsDoll || !localPlayer.IsAlive)
            {
                return false;
            }

            NetworkPlayer nearestTarget = FindNearestAliveSurvivor(localPlayer);

            if (nearestTarget == null)
            {
                return false;
            }

            RoundManager roundManager = FindAnyObjectByType<RoundManager>();

            if (roundManager == null)
            {
                return false;
            }

            roundManager.RequestDollAttackFromLocalPlayer(nearestTarget.OwnerClientId);
            return true;
        }

        /// <summary>
        /// Busca al muneco vulnerable cercano y solicita al servidor completar la accion ritual final.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se encontro al muneco candidato y se envio la solicitud.
        /// </returns>
        private bool TryRequestDollExorcism()
        {
            NetworkPlayer localPlayer = GetComponent<NetworkPlayer>();

            if (localPlayer == null || localPlayer.IsDoll || !localPlayer.IsAlive)
            {
                return false;
            }

            RoundManager roundManager = FindAnyObjectByType<RoundManager>();

            if (roundManager == null || !roundManager.IsDollVulnerable)
            {
                return false;
            }

            NetworkPlayer dollTarget = FindNearestDoll(localPlayer);

            if (dollTarget == null)
            {
                return false;
            }

            roundManager.RequestDollExorcismFromLocalPlayer(dollTarget.OwnerClientId);
            return true;
        }

        /// <summary>
        /// Solicita al componente de habilidad del muneco usar el espejo mas cercano.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando existe componente local y se envio la solicitud.
        /// </returns>
        private bool TryRequestMirrorTeleport()
        {
            NetworkPlayer localPlayer = GetComponent<NetworkPlayer>();

            if (localPlayer == null || !localPlayer.IsDoll || !localPlayer.IsAlive)
            {
                return false;
            }

            DollMirrorTeleport mirrorTeleport = GetComponent<DollMirrorTeleport>();

            if (mirrorTeleport == null)
            {
                return false;
            }

            mirrorTeleport.RequestTeleportThroughNearestMirror();
            return true;
        }

        /// <summary>
        /// Solicita al servidor colocar una trampa provisional del muneco.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando existe componente local y se envio la solicitud.
        /// </returns>
        private bool TryRequestTrapPlacement()
        {
            NetworkPlayer localPlayer = GetComponent<NetworkPlayer>();

            if (localPlayer == null || !localPlayer.IsDoll || !localPlayer.IsAlive)
            {
                return false;
            }

            DollTrapManager trapManager = GetComponent<DollTrapManager>();

            if (trapManager == null)
            {
                return false;
            }

            trapManager.RequestPlaceVoiceTrapFromLocalDoll();
            return true;
        }

        /// <summary>
        /// Selecciona localmente el superviviente vivo mas cercano para reducir ruido de input antes de enviar la peticion al servidor.
        /// </summary>
        /// <param name="localPlayer">
        /// Jugador propietario que intenta atacar.
        /// </param>
        /// <returns>
        /// Superviviente candidato mas cercano o <see langword="null"/> si no hay ninguno en rango.
        /// </returns>
        private NetworkPlayer FindNearestAliveSurvivor(NetworkPlayer localPlayer)
        {
            NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>();
            NetworkPlayer nearestTarget = null;
            float nearestDistance = dollAttackRange;

            foreach (NetworkPlayer candidate in players)
            {
                if (candidate == null || candidate == localPlayer || candidate.IsDoll || !candidate.IsAlive)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, candidate.transform.position);

                if (distance > nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestTarget = candidate;
            }

            return nearestTarget;
        }

        /// <summary>
        /// Selecciona localmente al muneco vivo mas cercano para intentar la accion ritual final.
        /// </summary>
        /// <param name="localPlayer">
        /// Superviviente propietario que intenta completar el ritual final.
        /// </param>
        /// <returns>
        /// Muneco candidato mas cercano o <see langword="null"/> si no esta en rango.
        /// </returns>
        private NetworkPlayer FindNearestDoll(NetworkPlayer localPlayer)
        {
            NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>();
            NetworkPlayer nearestDoll = null;
            float nearestDistance = dollExorcismRange;

            foreach (NetworkPlayer candidate in players)
            {
                if (candidate == null || candidate == localPlayer || !candidate.IsDoll || !candidate.IsAlive)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, candidate.transform.position);

                if (distance > nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestDoll = candidate;
            }

            return nearestDoll;
        }

        /// <summary>
        /// Selecciona la zona de activacion de sello mas cercana dentro del rango local permitido.
        /// </summary>
        /// <returns>
        /// Zona mas cercana encontrada, o <see langword="null"/> si no existe ninguna en rango.
        /// </returns>
        private SealActivationZone FindNearestSealActivationZone()
        {
            SealActivationZone[] zones = FindObjectsByType<SealActivationZone>();
            SealActivationZone nearestZone = null;
            float nearestDistance = interactionRange;

            foreach (SealActivationZone zone in zones)
            {
                if (zone == null || zone.TargetSeal == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, zone.transform.position);

                if (distance > nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestZone = zone;
            }

            return nearestZone;
        }

        /// <summary>
        /// Detecta si el jugador presiono la tecla de interaccion durante este frame.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se presiono la tecla E localmente.
        /// </returns>
        private static bool WasInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        /// <summary>
        /// Detecta si el jugador presiono la tecla provisional de ataque del muneco durante este frame.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se presiono la tecla F localmente.
        /// </returns>
        private static bool WasDollAttackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F);
#else
            return false;
#endif
        }

        /// <summary>
        /// Detecta si el muneco presiono la tecla provisional de teletransporte por espejo.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se presiono Q localmente.
        /// </returns>
        private static bool WasMirrorTeleportPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.qKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Q);
#else
            return false;
#endif
        }

        /// <summary>
        /// Detecta si el muneco presiono la tecla provisional para colocar trampa.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando se presiono T localmente.
        /// </returns>
        private static bool WasTrapPlacementPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.T);
#else
            return false;
#endif
        }
    }
}
