namespace AnjaniKitchen.Models;

public sealed class ItemMenu
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? ParentId { get; init; }
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public bool CanView { get; init; }
    public bool CanInsert { get; init; }
    public bool CanUpdate { get; init; }
    public bool CanDelete { get; init; }
}
