namespace HitoriKakurembo.PlayerSystem.Interfaces
{
    /// <summary>
    /// Contrato comun para componentes del jugador que pueden habilitar o bloquear control local.
    /// Permite que cinematicas, menus o estados futuros apaguen input sin conocer la implementacion concreta.
    /// </summary>
    public interface IPlayerControllable
    {
        /// <summary>
        /// Habilita el control normal del componente.
        /// </summary>
        void EnableControl();

        /// <summary>
        /// Deshabilita el control normal del componente.
        /// </summary>
        void DisableControl();
    }
}
