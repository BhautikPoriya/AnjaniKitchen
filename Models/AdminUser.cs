namespace AnjaniKitchen.Models;

public sealed class AdminUser
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public DateTime? DOB { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Country { get; init; }
    public bool IsSystemUser { get; init; }
    public bool IsActive { get; init; }
    public int RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
}
