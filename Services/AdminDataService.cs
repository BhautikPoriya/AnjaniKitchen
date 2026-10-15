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
        await EnsureCategoryDetailsColumnAsync(cancellationToken);

        const string sql = """
            SELECT category.Id, category.ParentId, parent.Name, category.Name,
                   category.Details, category.Img, category.IsActive
            FROM dbo.Category AS category
            LEFT JOIN dbo.Category AS parent ON parent.Id = category.ParentId
            ORDER BY CASE WHEN category.ParentId IS NULL THEN category.Id ELSE category.ParentId END,
                     CASE WHEN category.ParentId IS NULL THEN 0 ELSE 1 END,
                     category.Name, category.Id;
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
                ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                ParentName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Name = reader.GetString(3),
                Details = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Img = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetBoolean(6)
            });
        }

        return categories;
    }

    public async Task<IReadOnlyList<Dish>> GetDishesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDishTableAsync(cancellationToken);

        const string sql = """
            SELECT dish.Id, dish.Name, dish.Price, dish.OldPrice, dish.Description,
                   dish.CategoryId, category.Name, parent.Name, dish.Img, dish.IsNew, dish.IsActive
            FROM dbo.Dish AS dish
            INNER JOIN dbo.Category AS category ON category.Id = dish.CategoryId
            LEFT JOIN dbo.Category AS parent ON parent.Id = category.ParentId
            ORDER BY dish.Name, dish.Id;
            """;

        var dishes = new List<Dish>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dishes.Add(ReadDish(reader));
        }

        return dishes;
    }

    public async Task<Dish> CreateDishAsync(
        DishUpsertRequest request,
        string name,
        string? imagePublicId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDishTableAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Dish (Name, Price, OldPrice, Description, CategoryId, Img, IsNew, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Price, @OldPrice, @Description, @CategoryId, @Img, @IsNew, @IsActive);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Transaction = transaction;
        AddDishParameters(command, request, name, imagePublicId);
        var id = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The dish insert did not return an ID."));

        var dish = await GetDishByIdAsync(id, connection, transaction, cancellationToken)
            ?? throw new InvalidOperationException("The saved dish could not be read back.");
        await transaction.CommitAsync(cancellationToken);
        return dish;
    }

    public async Task<DishUpdateResult?> UpdateDishAsync(
        int id,
        DishUpsertRequest request,
        string name,
        string? imagePublicId,
        bool removeImage,
        CancellationToken cancellationToken = default)
    {
        await EnsureDishTableAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Dish
            SET Name = @Name,
                Price = @Price,
                OldPrice = @OldPrice,
                Description = @Description,
                CategoryId = @CategoryId,
                Img = CASE WHEN @RemoveImage = 1 THEN NULL WHEN @Img IS NOT NULL THEN @Img ELSE Img END,
                IsNew = @IsNew,
                IsActive = @IsActive
            OUTPUT DELETED.Img
            WHERE Id = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue("@Id", id);
        AddDishParameters(command, request, name, imagePublicId);
        command.Parameters.AddWithValue("@RemoveImage", removeImage);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var previousImagePublicId = reader.IsDBNull(0) ? null : reader.GetString(0);
        await reader.DisposeAsync();
        var dish = await GetDishByIdAsync(id, connection, transaction, cancellationToken);
        if (dish is null)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return new DishUpdateResult(dish, previousImagePublicId);
    }

    public async Task<DishDeleteResult> DeleteDishAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureDishTableAsync(cancellationToken);

        const string sql = "DELETE FROM dbo.Dish OUTPUT DELETED.Img WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        var imagePublicId = await command.ExecuteScalarAsync(cancellationToken);
        return imagePublicId is null
            ? new DishDeleteResult(false, null)
            : new DishDeleteResult(true, imagePublicId is DBNull ? null : (string)imagePublicId);
    }

    public async Task<bool?> UpdateDishStatusAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await EnsureDishTableAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Dish
            SET IsActive = @IsActive
            OUTPUT INSERTED.IsActive
            WHERE Id = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@IsActive", isActive);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? null : (bool)result;
    }

    private static async Task<Dish?> GetDishByIdAsync(
        int id,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT dish.Id, dish.Name, dish.Price, dish.OldPrice, dish.Description,
                   dish.CategoryId, category.Name, parent.Name, dish.Img, dish.IsNew, dish.IsActive
            FROM dbo.Dish AS dish
            INNER JOIN dbo.Category AS category ON category.Id = dish.CategoryId
            LEFT JOIN dbo.Category AS parent ON parent.Id = category.ParentId
            WHERE dish.Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDish(reader) : null;
    }

    private static Dish ReadDish(SqlDataReader reader)
    {
        return new Dish
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Price = reader.GetDecimal(2),
            OldPrice = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            CategoryId = reader.GetInt32(5),
            CategoryName = reader.GetString(6),
            ParentCategoryName = reader.IsDBNull(7) ? null : reader.GetString(7),
            Img = reader.IsDBNull(8) ? null : reader.GetString(8),
            IsNew = reader.GetBoolean(9),
            IsActive = reader.GetBoolean(10)
        };
    }

    private static void AddDishParameters(SqlCommand command, DishUpsertRequest request, string name, string? imagePublicId)
    {
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Price", request.Price);
        command.Parameters.AddWithValue("@OldPrice", (object?)request.OldPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("@Description", request.Description.Trim());
        command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
        command.Parameters.AddWithValue("@Img", (object?)imagePublicId ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsNew", request.IsNew);
        command.Parameters.AddWithValue("@IsActive", request.IsActive);
    }

    private async Task EnsureDishTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.Dish', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Dish
                (
                    Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Dish PRIMARY KEY,
                    Name nvarchar(200) NOT NULL,
                    Price decimal(10, 2) NOT NULL,
                    OldPrice decimal(10, 2) NULL,
                    Description nvarchar(2000) NOT NULL CONSTRAINT DF_Dish_Description DEFAULT (N''),
                    CategoryId int NOT NULL,
                    Img nvarchar(500) NULL,
                    IsNew bit NOT NULL CONSTRAINT DF_Dish_IsNew DEFAULT (0),
                    IsActive bit NOT NULL CONSTRAINT DF_Dish_IsActive DEFAULT (1),
                    CONSTRAINT FK_Dish_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category (Id)
                );
            END;
            IF COL_LENGTH(N'dbo.Dish', N'Img') IS NULL
            BEGIN
                ALTER TABLE dbo.Dish ADD Img nvarchar(500) NULL;
            END;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MenuCategory> CreateCategoryAsync(
        string name,
        string details,
        string? imagePublicId,
        int? parentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryDetailsColumnAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Category (ParentId, Name, Details, Img)
            OUTPUT INSERTED.Id
            VALUES (@ParentId, @Name, @Details, @Img);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ParentId", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Details", details);
        command.Parameters.AddWithValue("@Img", (object?)imagePublicId ?? DBNull.Value);
        var id = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The category insert did not return an ID."));

        return new MenuCategory
        {
            Id = id,
            ParentId = parentId,
            Name = name,
            Details = details,
            Img = imagePublicId,
            IsActive = true
        };
    }

    public async Task<CategoryUpdateResult?> UpdateCategoryAsync(
        int id,
        string name,
        string details,
        string? imagePublicId,
        bool removeImage,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryDetailsColumnAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Category
            SET Name = @Name,
                Details = @Details,
                Img = CASE
                    WHEN @RemoveImage = 1 THEN NULL
                    WHEN @Img IS NOT NULL THEN @Img
                    ELSE Img
                END
            OUTPUT INSERTED.Id, INSERTED.ParentId, INSERTED.Name, INSERTED.Details,
                   INSERTED.Img, INSERTED.IsActive, DELETED.Img
            WHERE Id = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Details", details);
        command.Parameters.AddWithValue("@Img", (object?)imagePublicId ?? DBNull.Value);
        command.Parameters.AddWithValue("@RemoveImage", removeImage);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var category = new MenuCategory
        {
            Id = reader.GetInt32(0),
            ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            Name = reader.GetString(2),
            Details = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Img = reader.IsDBNull(4) ? null : reader.GetString(4),
            IsActive = reader.GetBoolean(5)
        };
        return new CategoryUpdateResult(category, reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    public async Task<CategoryDeleteResult> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureDishTableAsync(cancellationToken);

        const string hasDishesSql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Dish WHERE CategoryId = @Id) THEN 1 ELSE 0 END;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var hasDishesCommand = new SqlCommand(hasDishesSql, connection);
        hasDishesCommand.Parameters.AddWithValue("@Id", id);
        if ((int)(await hasDishesCommand.ExecuteScalarAsync(cancellationToken) ?? 0) == 1)
        {
            return new CategoryDeleteResult(false, null, true);
        }

        const string sql = """
            DELETE FROM dbo.Category
            OUTPUT DELETED.Img
            WHERE Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new CategoryDeleteResult(false, null);
        }

        return new CategoryDeleteResult(true, reader.IsDBNull(0) ? null : reader.GetString(0));
    }

    public async Task<bool?> UpdateCategoryStatusAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryDetailsColumnAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Category
            SET IsActive = @IsActive
            OUTPUT INSERTED.IsActive
            WHERE Id = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@IsActive", isActive);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? null : (bool)result;
    }

    private async Task EnsureCategoryDetailsColumnAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF COL_LENGTH(N'dbo.Category', N'Details') IS NULL
            BEGIN
                ALTER TABLE dbo.Category
                    ADD Details nvarchar(1000) NOT NULL
                        CONSTRAINT DF_Category_Details_Runtime DEFAULT (N'');
            END;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
