namespace HitoriKakurembo.Rounds
{
    /// <summary>
    /// Define los estados de alto nivel utilizados por el flujo de rondas.
    /// </summary>
    public enum RoundState
    {
        /// <summary>
        /// La partida aun espera suficientes jugadores para poder preparar una ronda.
        /// </summary>
        WaitingForPlayers = 0,

        /// <summary>
        /// La ronda esta configurando participantes, roles y estado previo al juego.
        /// </summary>
        Preparing = 1,

        /// <summary>
        /// La parte jugable de la ronda se encuentra activa.
        /// </summary>
        Playing = 2,

        /// <summary>
        /// La ronda esta procesando calculo de puntos u otros cierres.
        /// </summary>
        Scoring = 3,

        /// <summary>
        /// La ronda ya termino y espera una nueva transicion.
        /// </summary>
        Completed = 4
    }

    /// <summary>
    /// Define el resultado autoritativo de la ronda actual para que UI, puntuacion y depuracion compartan el mismo cierre.
    /// </summary>
    public enum RoundOutcome
    {
        /// <summary>
        /// La ronda aun no tiene resultado final.
        /// </summary>
        None = 0,

        /// <summary>
        /// El equipo superviviente gano la ronda.
        /// </summary>
        SurvivorsWin = 1,

        /// <summary>
        /// El muneco gano la ronda.
        /// </summary>
        DollWin = 2,

        /// <summary>
        /// La ronda termino sin ganador claro, reservado para reglas futuras.
        /// </summary>
        Draw = 3
    }

    /// <summary>
    /// Explica por que se cerro la ronda; se sincroniza como dato compacto para mostrar mensajes coherentes en todos los clientes.
    /// </summary>
    public enum RoundEndReason
    {
        /// <summary>
        /// La ronda aun no termino o no se asigno una causa.
        /// </summary>
        None = 0,

        /// <summary>
        /// Los supervivientes activaron los seis sellos rituales requeridos.
        /// </summary>
        AllSealsActivated = 1,

        /// <summary>
        /// El muneco elimino a todos los supervivientes vivos.
        /// </summary>
        AllSurvivorsEliminated = 2,

        /// <summary>
        /// El tiempo de caceria llego a cero antes de que los supervivientes completaran el objetivo.
        /// </summary>
        HuntTimerExpired = 3,

        /// <summary>
        /// Otro sistema cerro la ronda manualmente.
        /// </summary>
        Manual = 4,

        /// <summary>
        /// Un superviviente uso la accion ritual final para eliminar al muneco vulnerable.
        /// </summary>
        DollExorcised = 5
    }
}
