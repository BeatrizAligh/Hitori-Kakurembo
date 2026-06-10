using System.Collections.Generic;
using UnityEngine;

namespace HitoriKakurembo.Roles
{
    [CreateAssetMenu(fileName = "RoleInventoryDatabase", menuName = "Hitori Kakurembo/Inventory/Role Inventory Database")]
    public class RoleInventoryDatabase : ScriptableObject
    {
        [SerializeField] private List<RoleInventoryDefinition> roleInventories = new List<RoleInventoryDefinition>();

        public RoleInventoryDefinition GetInventoryForRole(PlayerRoleType roleType)
        {
            foreach (RoleInventoryDefinition inventory in roleInventories)
            {
                if (inventory != null && inventory.RoleType == roleType)
                {
                    return inventory;
                }
            }

            return null;
        }
    }
}
