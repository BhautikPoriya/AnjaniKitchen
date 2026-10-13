using AnjaniKitchen.Models;

namespace AnjaniKitchen.Services;

public interface IAdminDataService
{
    Task<AdminUser?> GetActiveUserByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemMenu>> GetMenusForUserAsync(int userId, int roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MenuCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<bool> HasMenuPermissionAsync(int userId, int roleId, string controllerName, string actionName, MenuPermission permission, CancellationToken cancellationToken = default);
}
