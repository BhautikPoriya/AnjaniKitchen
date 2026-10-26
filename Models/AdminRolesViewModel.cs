namespace AnjaniKitchen.Models;

public sealed class AdminRolesViewModel
{
    public required IReadOnlyList<AdminRole> Roles { get; init; }
    public required ItemMenu Permissions { get; init; }
}
