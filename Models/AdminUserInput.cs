using System.ComponentModel.DataAnnotations;

namespace AnjaniKitchen.Models;

public sealed class AdminUserInput
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DOB { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    public bool IsSystemUser { get; set; }
    public bool IsActive { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int RoleId { get; set; }
}
