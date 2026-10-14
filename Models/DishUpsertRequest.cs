using System.ComponentModel.DataAnnotations;

namespace AnjaniKitchen.Models;

public sealed class DishUpsertRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal Price { get; init; }

    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal? OldPrice { get; init; }

    [StringLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CategoryId { get; init; }

    public IFormFile? Image { get; init; }
    public bool RemoveImage { get; init; }
    public bool IsNew { get; init; }
    public bool IsActive { get; init; } = true;
}
