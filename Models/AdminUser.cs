namespace AnjaniKitchen.Models;

public sealed class AdminUser
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
}
