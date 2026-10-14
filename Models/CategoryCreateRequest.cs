using System.ComponentModel.DataAnnotations;

namespace AnjaniKitchen.Models;

public sealed class CategoryCreateRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(1000)]
    public string Details { get; init; } = string.Empty;

    public int? ParentId { get; init; }

    public IFormFile? Image { get; init; }

    public bool RemoveImage { get; init; }
}
