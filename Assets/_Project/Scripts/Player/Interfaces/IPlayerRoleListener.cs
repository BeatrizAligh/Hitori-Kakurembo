using HitoriKakurembo.PlayerSystem.Data;

namespace HitoriKakurembo.PlayerSystem.Interfaces
{
    /// <summary>
    /// Contrato para componentes que necesitan reaccionar cuando otro sistema asigna o limpia el rol del jugador.
    /// No contiene habilidades ni reglas de rol; solo comunica el cambio de identidad.
    /// </summary>
    public interface IPlayerRoleListener
    {
        /// <summary>
        /// Notifica que el rol actual del jugador cambio.
        /// </summary>
        /// <param name="newRole">
        /// Nuevo rol asignado al jugador.
        /// </param>
        void OnRoleChanged(PlayerRoleType newRole);
    }
}
