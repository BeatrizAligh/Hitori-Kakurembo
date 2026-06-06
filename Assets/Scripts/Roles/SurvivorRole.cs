using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Representa el asset de configuracion del rol Survivor.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Roles/Survivor")]
    public class SurvivorRole : RoleBase
    {
        /// <summary>
        /// Obtiene el tipo de rol expuesto por este asset.
        /// </summary>
        public override PlayerRoleType RoleType => PlayerRoleType.Survivor;
    }
}
