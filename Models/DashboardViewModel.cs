namespace AnjaniKitchen.Models;

public sealed class DashboardViewModel
{
    public required string AdminName { get; init; }
    public required string RoleName { get; init; }
    public required string InitialPage { get; init; }
    public required IReadOnlyList<ItemMenu> Menus { get; init; }
    public IReadOnlyList<MenuCategory> Categories { get; init; } = [];
    public IReadOnlyList<Dish> Dishes { get; init; } = [];
}
