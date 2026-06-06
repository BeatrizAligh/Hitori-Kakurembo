using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Representa el asset de configuracion del rol Medium.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Roles/Medium")]
    public class MediumRole : RoleBase
    {
        /// <summary>
        /// Obtiene el tipo de rol expuesto por este asset.
        /// </summary>
        public override PlayerRoleType RoleType => PlayerRoleType.Medium;
    }
}
