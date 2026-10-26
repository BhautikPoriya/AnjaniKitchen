namespace AnjaniKitchen.Models;

public sealed class AdminUserFormViewModel
{
    public required AdminUserInput User { get; init; }
    public required IReadOnlyList<AdminRole> Roles { get; init; }
    public bool IsEdit { get; init; }
}
