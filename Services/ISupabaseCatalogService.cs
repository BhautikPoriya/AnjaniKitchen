using AnjaniKitchen.Models;

namespace AnjaniKitchen.Services;

public interface ISupabaseCatalogService
{
    Task UpsertCategoryAsync(MenuCategory category, CancellationToken cancellationToken = default);
    Task UpdateCategoryStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task UpsertDishAsync(Dish dish, CancellationToken cancellationToken = default);
    Task UpdateDishStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteDishAsync(int id, CancellationToken cancellationToken = default);
}
