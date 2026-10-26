namespace AnjaniKitchen.Models;

public sealed class RolePrivilegesViewModel
{
    public required IReadOnlyList<AdminRole> Roles { get; init; }
    public required IReadOnlyList<ItemMenu> Menus { get; init; }
    public required IReadOnlyDictionary<int, RolePrivilegeInput> Privileges { get; init; }
    public int? SelectedRoleId { get; init; }
    public bool CanUpdate { get; init; }
}
