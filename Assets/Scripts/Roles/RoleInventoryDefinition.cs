using System;
using System.Collections.Generic;
using HitoriKakurembo.Items;
using UnityEngine;

namespace HitoriKakurembo.Roles
{
    [CreateAssetMenu(fileName = "RoleInventory", menuName = "Hitori Kakurembo/Inventory/Role Inventory")]
    public class RoleInventoryDefinition : ScriptableObject
    {
        [Serializable]
        public class InventoryGrant
        {
            [SerializeField] private InventoryItemDefinition item;
            [SerializeField] private int quantity = 1;
            [SerializeField] private bool roleBound;

            public InventoryItemDefinition Item => item;
            public int Quantity => Mathf.Max(1, quantity);
            public bool RoleBound => roleBound;
        }

        [SerializeField] private PlayerRoleType roleType = PlayerRoleType.Survivor;
        [SerializeField] private List<InventoryGrant> startingItems = new List<InventoryGrant>();

        public PlayerRoleType RoleType => roleType;
        public IReadOnlyList<InventoryGrant> StartingItems => startingItems;
    }
}
