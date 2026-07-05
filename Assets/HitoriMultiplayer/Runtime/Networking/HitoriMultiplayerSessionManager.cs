using System.Collections.Generic;
using TestMultiplayer.Networking;

namespace HitoriKakurembo.Multiplayer.Networking
{
    /// <summary>
    /// Especializacion de la sesion base TestMultiplayer para Hitori Kakurembo.
    /// Hereda el flujo probado de Relay, lobby, ready system, aprobacion de conexiones y carga de escenas sin modificar la carpeta TestMultiplayer.
    /// </summary>
    public class HitoriMultiplayerSessionManager : TestMultiplayerSessionManager
    {
        /// <summary>
        /// Devuelve la instancia activa de sesion Hitori cuando el runtime fue creado desde las escenas/adaptadores de este modulo.
        /// </summary>
        public static HitoriMultiplayerSessionManager HitoriInstance => Instance as HitoriMultiplayerSessionManager;

        /// <summary>
        /// Devuelve los brains conectados ya casteados al tipo especializado de Hitori.
        /// </summary>
        public IReadOnlyList<HitoriPlayerBrain> HitoriBrains
        {
            get
            {
                List<HitoriPlayerBrain> result = new List<HitoriPlayerBrain>();

                foreach (TestMultiplayerPlayerBrain brain in Brains)
                {
                    if (brain is HitoriPlayerBrain hitoriBrain)
                    {
                        result.Add(hitoriBrain);
                    }
                }

                return result;
            }
        }
    }
}
