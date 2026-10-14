namespace AnjaniKitchen.Models;

public sealed class MenuCategory
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string? ParentName { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string? Img { get; init; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; init; }
}
