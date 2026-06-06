using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Representa el asset de configuracion del rol Exorcist.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Roles/Exorcist")]
    public class ExorcistRole : RoleBase
    {
        /// <summary>
        /// Obtiene el tipo de rol expuesto por este asset.
        /// </summary>
        public override PlayerRoleType RoleType => PlayerRoleType.Exorcist;
    }
}
