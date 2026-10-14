namespace AnjaniKitchen.Models;

public sealed class Dish
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? OldPrice { get; init; }
    public string Description { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string? ParentCategoryName { get; init; }
    public string? Img { get; init; }
    public string? ImageUrl { get; set; }
    public bool IsNew { get; init; }
    public bool IsActive { get; init; }
}
