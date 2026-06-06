using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Representa el asset de configuracion del rol Monk.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Roles/Monk")]
    public class MonkRole : RoleBase
    {
        /// <summary>
        /// Obtiene el tipo de rol expuesto por este asset.
        /// </summary>
        public override PlayerRoleType RoleType => PlayerRoleType.Monk;
    }
}
