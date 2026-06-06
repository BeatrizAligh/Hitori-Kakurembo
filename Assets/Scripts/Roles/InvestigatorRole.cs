using UnityEngine;

namespace HitoriKakurembo.Roles
{
    /// <summary>
    /// Representa el asset de configuracion del rol Investigator.
    /// </summary>
    [CreateAssetMenu(menuName = "Hitori Kakurembo/Roles/Investigator")]
    public class InvestigatorRole : RoleBase
    {
        /// <summary>
        /// Obtiene el tipo de rol expuesto por este asset.
        /// </summary>
        public override PlayerRoleType RoleType => PlayerRoleType.Investigator;
    }
}
