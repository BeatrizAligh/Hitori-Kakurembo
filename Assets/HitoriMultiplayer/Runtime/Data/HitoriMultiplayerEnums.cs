namespace HitoriKakurembo.Multiplayer.Data
{
    /// <summary>
    /// Equipo multiplayer actual de un jugador dentro de Hitori Kakurembo.
    /// Se sincroniza desde el servidor para que UI, rondas y pawns puedan reaccionar sin depender de managers antiguos.
    /// </summary>
    public enum HitoriPlayerTeam
    {
        None = 0,
        Survivor = 1,
        Doll = 2,
        Spectator = 3
    }

    /// <summary>
    /// Rol jugable asignable a un jugador superviviente.
    /// La logica concreta de habilidades vivira en sistemas posteriores; aqui solo queda el dato sincronizado.
    /// </summary>
    public enum HitoriPlayerRole
    {
        None = 0,
        Survivor = 1,
        Medium = 2,
        Exorcist = 3,
        Photographer = 4,
        Investigator = 5,
        Monk = 6
    }

    /// <summary>
    /// Estado de vida de alto nivel del jugador.
    /// Permite separar identidad de red del cuerpo fisico que controla.
    /// </summary>
    public enum HitoriLifeState
    {
        Alive = 0,
        Downed = 1,
        Dead = 2,
        Spectator = 3,
        TemporarilyTransformed = 4
    }
}
