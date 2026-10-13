namespace AnjaniKitchen.Models;

public sealed class MenuCategory
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Img { get; init; }
    public bool IsActive { get; init; }
}
