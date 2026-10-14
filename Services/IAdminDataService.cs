using AnjaniKitchen.Models;

namespace AnjaniKitchen.Services;

public interface IAdminDataService
{
    Task<AdminUser?> GetActiveUserByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemMenu>> GetMenusForUserAsync(int userId, int roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MenuCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dish>> GetDishesAsync(CancellationToken cancellationToken = default);
    Task<Dish> CreateDishAsync(DishUpsertRequest request, string name, string? imagePublicId, CancellationToken cancellationToken = default);
    Task<DishUpdateResult?> UpdateDishAsync(int id, DishUpsertRequest request, string name, string? imagePublicId, bool removeImage, CancellationToken cancellationToken = default);
    Task<DishDeleteResult> DeleteDishAsync(int id, CancellationToken cancellationToken = default);
    Task<MenuCategory> CreateCategoryAsync(string name, string details, string? imagePublicId, int? parentId, CancellationToken cancellationToken = default);
    Task<CategoryUpdateResult?> UpdateCategoryAsync(int id, string name, string details, string? imagePublicId, bool removeImage, CancellationToken cancellationToken = default);
    Task<CategoryDeleteResult> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasMenuPermissionAsync(int userId, int roleId, string controllerName, string actionName, MenuPermission permission, CancellationToken cancellationToken = default);
}
