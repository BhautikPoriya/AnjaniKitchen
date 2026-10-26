namespace AnjaniKitchen.Models;

public sealed class AdminUsersViewModel
{
    public required IReadOnlyList<AdminUser> Users { get; init; }
    public required IReadOnlyList<AdminRole> Roles { get; init; }
    public required ItemMenu Permissions { get; init; }
}
