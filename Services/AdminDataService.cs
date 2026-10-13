using AnjaniKitchen.Models;
using Microsoft.Data.SqlClient;

namespace AnjaniKitchen.Services;

public sealed class AdminDataService : IAdminDataService
{
    private readonly string _connectionString;

    public AdminDataService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("The DefaultConnection connection string is not configured.");
    }

    public async Task<AdminUser?> GetActiveUserByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT userAccount.Id, userAccount.Name, userAccount.UserName, userAccount.[Password],
                   userAccount.IsActive, userAccount.RoleId, userRole.RoleName
            FROM dbo.AdminUsers AS userAccount
            INNER JOIN dbo.[Role] AS userRole ON userRole.Id = userAccount.RoleId
            WHERE userAccount.UserName = @UserName
              AND userAccount.IsActive = 1
              AND userRole.IsActive = 1;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", userName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdminUser
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            UserName = reader.GetString(2),
            Password = reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            RoleId = reader.GetInt32(5),
            RoleName = reader.GetString(6)
        };
    }

    public async Task<IReadOnlyList<ItemMenu>> GetMenusForUserAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT menu.Id, menu.Name, menu.ParentId, menu.ControllerName, menu.ActionName,
                   CAST(COALESCE(privilege.[View], 0) AS bit),
                   CAST(COALESCE(privilege.[Insert], 0) AS bit),
                   CAST(COALESCE(privilege.[Update], 0) AS bit),
                   CAST(COALESCE(privilege.[Delete], 0) AS bit)
            FROM dbo.ItemMenus AS menu
            INNER JOIN dbo.AdminUsers AS userAccount
                ON userAccount.Id = @UserId AND userAccount.RoleId = @RoleId AND userAccount.IsActive = 1
            INNER JOIN dbo.[Role] AS userRole ON userRole.Id = userAccount.RoleId AND userRole.IsActive = 1
            LEFT JOIN dbo.RolePrivilege AS privilege
                ON privilege.MenuId = menu.Id
                AND privilege.RoleId = userAccount.RoleId
                AND privilege.UserId = userAccount.Id
            WHERE menu.IsActive = 1
              AND
              (
                  COALESCE(privilege.[View], 0) = 1
                  OR
                  (
                      menu.ParentId IS NULL
                      AND EXISTS
                      (
                      SELECT 1
                      FROM dbo.ItemMenus AS childMenu
                      INNER JOIN dbo.RolePrivilege AS childPrivilege ON childPrivilege.MenuId = childMenu.Id
                      WHERE childMenu.ParentId = menu.Id
                        AND childMenu.IsActive = 1
                        AND childPrivilege.RoleId = userAccount.RoleId
                        AND childPrivilege.UserId = userAccount.Id
                        AND childPrivilege.[View] = 1
                      )
                  )
              )
            ORDER BY CASE WHEN menu.ParentId IS NULL THEN menu.Id ELSE menu.ParentId END, menu.Id;
            """;

        var menus = new List<ItemMenu>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@RoleId", roleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            menus.Add(new ItemMenu
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ParentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                ControllerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                ActionName = reader.IsDBNull(4) ? null : reader.GetString(4),
                CanView = reader.GetBoolean(5),
                CanInsert = reader.GetBoolean(6),
                CanUpdate = reader.GetBoolean(7),
                CanDelete = reader.GetBoolean(8)
            });
        }

        return menus;
    }

    public async Task<IReadOnlyList<MenuCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Img, IsActive
            FROM dbo.Category
            ORDER BY Name, Id;
            """;

        var categories = new List<MenuCategory>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new MenuCategory
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Img = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsActive = reader.GetBoolean(3)
            });
        }

        return categories;
    }

    public async Task<bool> HasMenuPermissionAsync(
        int userId,
        int roleId,
        string controllerName,
        string actionName,
        MenuPermission permission,
        CancellationToken cancellationToken = default)
    {
        var permissionColumn = permission switch
        {
            MenuPermission.View => "[View]",
            MenuPermission.Insert => "[Insert]",
            MenuPermission.Update => "[Update]",
            MenuPermission.Delete => "[Delete]",
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        };

        var sql = $"""
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.AdminUsers AS userAccount
                INNER JOIN dbo.[Role] AS userRole ON userRole.Id = userAccount.RoleId
                INNER JOIN dbo.RolePrivilege AS privilege
                    ON privilege.UserId = userAccount.Id
                    AND privilege.RoleId = userAccount.RoleId
                INNER JOIN dbo.ItemMenus AS menu ON menu.Id = privilege.MenuId
                WHERE userAccount.Id = @UserId
                  AND userAccount.RoleId = @RoleId
                  AND userAccount.IsActive = 1
                  AND userRole.IsActive = 1
                  AND menu.IsActive = 1
                  AND menu.ControllerName = @ControllerName
                  AND menu.ActionName = @ActionName
                  AND privilege.{permissionColumn} = 1
            ) THEN 1 ELSE 0 END;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@RoleId", roleId);
        command.Parameters.AddWithValue("@ControllerName", controllerName);
        command.Parameters.AddWithValue("@ActionName", actionName);

        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0) == 1;
    }
}
