using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Representa el asset de configuracion del rol Photographer.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Roles/Photographer")]
    public class PhotographerRole : RoleBase
    {
        /// <summary>
        /// Obtiene el tipo de rol expuesto por este asset.
        /// </summary>
        public override PlayerRoleType RoleType => PlayerRoleType.Photographer;
    }
}
