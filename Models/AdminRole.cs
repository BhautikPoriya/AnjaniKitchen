namespace AnjaniKitchen.Models;

public sealed class AdminRole
{
    public int Id { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
