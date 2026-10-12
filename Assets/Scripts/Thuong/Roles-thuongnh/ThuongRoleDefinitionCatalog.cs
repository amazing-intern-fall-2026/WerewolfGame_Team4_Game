using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Thuong/Role Definition Catalog", fileName = "RoleDefinitions")]
public sealed class ThuongRoleDefinitionCatalog : ScriptableObject
{
    public List<ThuongRoleDefinition> definitions = new();
    public ThuongRoleDefinition Find(RoleType role) => definitions?.Find(item => item != null && item.roleType == role);
    public bool Validate(out string message)
    {
        var roles = new HashSet<RoleType>();
        var ids = new HashSet<string>();
        if (definitions != null)
            foreach (var definition in definitions)
                if (definition == null || string.IsNullOrWhiteSpace(definition.roleID) ||
                    !roles.Add(definition.roleType) || !ids.Add(definition.roleID))
                { message = "Role catalog contains a missing asset or duplicate RoleType/ID."; return false; }
        message = "Valid.";
        return true;
    }
}
