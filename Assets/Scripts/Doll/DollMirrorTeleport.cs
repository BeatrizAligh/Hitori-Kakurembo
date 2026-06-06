using HitoriKakurembo.House;
using HitoriKakurembo.Network;
using HitoriKakurembo.Ritual;
using HitoriKakurembo.Rounds;
using Unity.Netcode;
using UnityEngine;

namespace HitoriKakurembo.Doll
{
    /// <summary>
    /// Contiene la logica de teletransporte por espejos del muneco y delega la validacion real al servidor.
    /// </summary>
    public class DollMirrorTeleport : NetworkBehaviour
    {
        /// <summary>
        /// Distancia maxima desde el muneco hasta un espejo para poder iniciar el teletransporte.
        /// </summary>
        [SerializeField] private float mirrorUseRange = 3f;

        /// <summary>
        /// Segundos de espera entre teletransportes aceptados por servidor.
        /// </summary>
        [SerializeField] private float teleportCooldown = 4f;

        /// <summary>
        /// Offset frontal aplicado al salir del espejo para evitar aparecer dentro del collider visual.
        /// </summary>
        [SerializeField] private float mirrorExitOffset = 1.4f;

        /// <summary>
        /// Espejo en el que el muneco se considera actualmente alojado o desde el cual acaba de entrar.
        /// </summary>
        [SerializeField] private Transform currentMirror;

        /// <summary>
        /// Proximo tiempo de servidor en el que se aceptara otro teletransporte por espejo.
        /// </summary>
        private float nextServerTeleportAllowedTime;

        /// <summary>
        /// Obtiene un valor que indica si el muneco se encuentra marcado como dentro de un espejo.
        /// </summary>
        public bool IsInsideMirror { get; private set; }

        /// <summary>
        /// Obtiene la distancia local usada para elegir el espejo candidato.
        /// </summary>
        public float MirrorUseRange => mirrorUseRange;

        /// <summary>
        /// Solicita al servidor usar el espejo mas cercano; el cliente no decide el resultado final.
        /// </summary>
        public void RequestTeleportThroughNearestMirror()
        {
            if (!IsOwner)
            {
                return;
            }

            if (IsServer)
            {
                TryTeleportThroughNearestMirrorOnServer();
                return;
            }

            RequestMirrorTeleportRpc();
        }

        /// <summary>
        /// Registra la entrada del muneco a un espejo y mueve su posicion al punto de dicho espejo.
        /// </summary>
        /// <param name="mirrorTransform">
        /// Transform del espejo de entrada.
        /// </param>
        public void EnterMirror(Transform mirrorTransform)
        {
            currentMirror = mirrorTransform;
            IsInsideMirror = currentMirror != null;

            if (currentMirror != null)
            {
                transform.position = currentMirror.position;
            }
        }

        /// <summary>
        /// Registra la salida del muneco desde un espejo y lo posiciona en el punto de salida indicado.
        /// </summary>
        /// <param name="exitTransform">
        /// Transform de salida al que debe teletransportarse el muneco.
        /// </param>
        public void ExitMirror(Transform exitTransform)
        {
            if (exitTransform != null)
            {
                transform.position = exitTransform.position;
                transform.rotation = exitTransform.rotation;
            }

            currentMirror = null;
            IsInsideMirror = false;
        }

        /// <summary>
        /// Reinicia estado y cooldown de espejo al preparar una nueva ronda desde servidor.
        /// </summary>
        public void ResetMirrorStateOnServer()
        {
            if (!IsServer)
            {
                return;
            }

            currentMirror = null;
            IsInsideMirror = false;
            nextServerTeleportAllowedTime = 0f;
        }

        /// <summary>
        /// RPC enviado por el propietario local para pedir teletransporte por espejo.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestMirrorTeleportRpc()
        {
            TryTeleportThroughNearestMirrorOnServer();
        }

        /// <summary>
        /// Valida en servidor si el muneco puede usar el espejo mas cercano y aplica el teleport sincronizado.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el teletransporte fue aceptado por servidor.
        /// </returns>
        private bool TryTeleportThroughNearestMirrorOnServer()
        {
            if (!CanProcessMirrorTeleportOnServer())
            {
                return false;
            }

            MirrorPortal entryPortal = FindNearestMirrorPortal();

            if (entryPortal == null || !entryPortal.TryGetLinkedPortal(out MirrorPortal exitPortal))
            {
                return false;
            }

            Vector3 exitPosition = exitPortal.transform.position + (exitPortal.transform.forward * mirrorExitOffset);
            exitPosition.y = Mathf.Max(exitPosition.y, 1.1f);
            Quaternion exitRotation = exitPortal.GetExitRotation();

            nextServerTeleportAllowedTime = Time.time + teleportCooldown;
            EnterMirror(entryPortal.transform);

            NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();
            networkPlayer?.TeleportToGameSpawnOnServer(exitPosition, exitRotation);

            ApplyMirrorExitClientRpc(entryPortal.PortalIndex, exitPortal.PortalIndex, exitPosition, exitRotation);
            return true;
        }

        /// <summary>
        /// Determina si el servidor acepta procesar un teletransporte de espejo en el estado actual.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> cuando el jugador es el muneco vivo y la ronda esta en caceria.
        /// </returns>
        private bool CanProcessMirrorTeleportOnServer()
        {
            if (!IsServer || Time.time < nextServerTeleportAllowedTime)
            {
                return false;
            }

            NetworkPlayer networkPlayer = GetComponent<NetworkPlayer>();

            if (networkPlayer == null || !networkPlayer.IsDoll || !networkPlayer.IsAlive)
            {
                return false;
            }

            RoundManager roundManager = HitoriKakurembo.Core.ServiceLocator.Resolve<RoundManager>() ?? FindAnyObjectByType<RoundManager>();

            return roundManager != null
                && roundManager.CurrentState == RoundState.Playing
                && roundManager.CurrentRitualPhase == RitualPhase.Hunt
                && roundManager.CurrentOutcome == RoundOutcome.None;
        }

        /// <summary>
        /// Busca el espejo mas cercano dentro del rango permitido usando la posicion real del servidor.
        /// </summary>
        /// <returns>
        /// Portal candidato o <see langword="null"/> si no existe espejo en rango.
        /// </returns>
        private MirrorPortal FindNearestMirrorPortal()
        {
            MirrorPortal[] portals = FindObjectsByType<MirrorPortal>();
            MirrorPortal nearestPortal = null;
            float nearestDistance = mirrorUseRange;

            foreach (MirrorPortal portal in portals)
            {
                if (portal == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, portal.transform.position);

                if (distance > nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestPortal = portal;
            }

            return nearestPortal;
        }

        /// <summary>
        /// Replica en clientes la salida por espejo para mantener estado local y trazas de depuracion coherentes.
        /// </summary>
        /// <param name="entryPortalIndex">
        /// Indice del espejo de entrada elegido por servidor.
        /// </param>
        /// <param name="exitPortalIndex">
        /// Indice del espejo de salida elegido por servidor.
        /// </param>
        /// <param name="exitPosition">
        /// Posicion final validada.
        /// </param>
        /// <param name="exitRotation">
        /// Rotacion final validada.
        /// </param>
        [Rpc(SendTo.ClientsAndHost)]
        private void ApplyMirrorExitClientRpc(int entryPortalIndex, int exitPortalIndex, Vector3 exitPosition, Quaternion exitRotation)
        {
            currentMirror = ResolveMirrorPortalTransform(exitPortalIndex);
            IsInsideMirror = false;

            if (IsOwner)
            {
                transform.SetPositionAndRotation(exitPosition, exitRotation);
            }
        }

        /// <summary>
        /// Resuelve el transform de un espejo por indice para estado local y depuracion.
        /// </summary>
        /// <param name="portalIndex">
        /// Indice del portal buscado.
        /// </param>
        /// <returns>
        /// Transform del espejo encontrado o <see langword="null"/> si no existe.
        /// </returns>
        private static Transform ResolveMirrorPortalTransform(int portalIndex)
        {
            MirrorPortal[] portals = FindObjectsByType<MirrorPortal>();

            foreach (MirrorPortal portal in portals)
            {
                if (portal != null && portal.PortalIndex == portalIndex)
                {
                    return portal.transform;
                }
            }

            return null;
        }
    }
}
