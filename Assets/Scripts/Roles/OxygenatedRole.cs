using UnityEngine;

namespace HitoriKakurembo.Roles
{
    [CreateAssetMenu(fileName = "OxygenatedRole", menuName = "Hitori Kakurembo/Roles/Oxygenated")]
    public class OxygenatedRole : RoleBase
    {
        public override PlayerRoleType RoleType => PlayerRoleType.Oxygenated;
    }
}
