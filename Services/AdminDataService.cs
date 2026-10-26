using System.Data;
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

    public async Task<IReadOnlyList<AdminUser>> GetAdminUsersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT userAccount.Id, userAccount.FirstName, userAccount.LastName, userAccount.Email,
                   userAccount.UserName, userAccount.Phone, userAccount.DOB, userAccount.City,
                   userAccount.State, userAccount.Country, userAccount.IsSystemUser,
                   userAccount.IsActive, userAccount.RoleId, userRole.RoleName
            FROM dbo.AdminUsers AS userAccount
            INNER JOIN dbo.[Role] AS userRole ON userRole.Id = userAccount.RoleId
            ORDER BY userAccount.LastName, userAccount.FirstName, userAccount.Id;
            """;
        var users = new List<AdminUser>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var firstName = reader.GetString(1);
            var lastName = reader.GetString(2);
            users.Add(new AdminUser
            {
                Id = reader.GetInt32(0),
                FirstName = firstName,
                LastName = lastName,
                Name = $"{firstName} {lastName}".Trim(),
                Email = reader.GetString(3),
                UserName = reader.GetString(4),
                Phone = reader.IsDBNull(5) ? null : reader.GetString(5),
                DOB = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                City = reader.IsDBNull(7) ? null : reader.GetString(7),
                State = reader.IsDBNull(8) ? null : reader.GetString(8),
                Country = reader.IsDBNull(9) ? null : reader.GetString(9),
                IsSystemUser = reader.GetBoolean(10),
                IsActive = reader.GetBoolean(11),
                RoleId = reader.GetInt32(12),
                RoleName = reader.GetString(13)
            });
        }
        return users;
    }

    public async Task<int> SaveAdminUserAsync(
        AdminUserInput user,
        string? passwordHash,
        CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO dbo.AdminUsers
                (Name, FirstName, LastName, Email, UserName, [Password], Phone, DOB, City, State, Country,
                 IsSystemUser, IsActive, RoleId)
            OUTPUT INSERTED.Id
            VALUES
                (@Name, @FirstName, @LastName, @Email, @UserName, @Password, @Phone, @DOB, @City, @State, @Country,
                 @IsSystemUser, @IsActive, @RoleId);
            """;
        const string updateSql = """
            UPDATE dbo.AdminUsers
            SET Name = @Name, FirstName = @FirstName, LastName = @LastName, Email = @Email,
                UserName = @UserName, Phone = @Phone, DOB = @DOB, City = @City, State = @State, Country = @Country,
                IsSystemUser = @IsSystemUser, IsActive = @IsActive, RoleId = @RoleId
            WHERE Id = @Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(user.Id == 0 ? insertSql : updateSql, connection);
        AddAdminUserParameters(command, user);
        if (user.Id == 0)
        {
            if (passwordHash is null)
                throw new ArgumentException("A password hash is required when creating a user.", nameof(passwordHash));

            command.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value = passwordHash;
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        command.Parameters.Add("@Id", SqlDbType.Int).Value = user.Id;
        if (passwordHash is not null)
        {
            command.CommandText = """
                UPDATE dbo.AdminUsers
                SET Name = @Name, FirstName = @FirstName, LastName = @LastName, Email = @Email,
                    UserName = @UserName, [Password] = @Password, Phone = @Phone, DOB = @DOB,
                    City = @City, State = @State, Country = @Country,
                    IsSystemUser = @IsSystemUser, IsActive = @IsActive, RoleId = @RoleId
                WHERE Id = @Id;
                """;
            command.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value = passwordHash;
        }

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? user.Id : 0;
    }

    public async Task<bool> DeleteAdminUserAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM dbo.AdminUsers WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<AdminRole>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id, RoleName, IsActive FROM dbo.[Role] ORDER BY RoleName, Id;";
        var roles = new List<AdminRole>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new AdminRole
            {
                Id = reader.GetInt32(0),
                RoleName = reader.GetString(1),
                IsActive = reader.GetBoolean(2)
            });
        }
        return roles;
    }

    public async Task<int> SaveRoleAsync(int id, string roleName, bool isActive, CancellationToken cancellationToken = default)
    {
        var sql = id == 0
            ? "INSERT INTO dbo.[Role] (RoleName, IsActive) OUTPUT INSERTED.Id VALUES (@RoleName, @IsActive);"
            : "UPDATE dbo.[Role] SET RoleName = @RoleName, IsActive = @IsActive WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 200).Value = roleName;
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
        if (id == 0)
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? id : 0;
    }

    public async Task<bool> DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE RoleId = @Id)
                SELECT 0;
            ELSE
            BEGIN
                DELETE FROM dbo.RolePrivilege WHERE RoleId = @Id;
                DELETE FROM dbo.[Role] WHERE Id = @Id;
                SELECT @@ROWCOUNT;
            END;
            """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<ItemMenu>> GetAllMenusAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, ParentId, ControllerName, ActionName
            FROM dbo.ItemMenus
            WHERE IsActive = 1
            ORDER BY CASE WHEN ParentId IS NULL THEN Id ELSE ParentId END, Id;
            """;
        var menus = new List<ItemMenu>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            menus.Add(new ItemMenu
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ParentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                ControllerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                ActionName = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return menus;
    }

    public async Task<IReadOnlyList<RolePrivilegeInput>> GetRolePrivilegesAsync(int roleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT menu.Id, CAST(COALESCE(privilege.[View], 0) AS bit),
                   CAST(COALESCE(privilege.[Add], 0) AS bit), CAST(COALESCE(privilege.[Edit], 0) AS bit),
                   CAST(COALESCE(privilege.[Update], 0) AS bit), CAST(COALESCE(privilege.[Delete], 0) AS bit)
            FROM dbo.ItemMenus AS menu
            LEFT JOIN dbo.RolePrivilege AS privilege ON privilege.MenuId = menu.Id AND privilege.RoleId = @RoleId
            WHERE menu.IsActive = 1
            ORDER BY CASE WHEN menu.ParentId IS NULL THEN menu.Id ELSE menu.ParentId END, menu.Id;
            """;
        var privileges = new List<RolePrivilegeInput>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            privileges.Add(new RolePrivilegeInput
            {
                MenuId = reader.GetInt32(0),
                View = reader.GetBoolean(1),
                Add = reader.GetBoolean(2),
                Edit = reader.GetBoolean(3),
                Update = reader.GetBoolean(4),
                Delete = reader.GetBoolean(5)
            });
        }
        return privileges;
    }

    public async Task SaveRolePrivilegesAsync(
        int roleId,
        IReadOnlyList<RolePrivilegeInput> privileges,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var deleteCommand = new SqlCommand("DELETE FROM dbo.RolePrivilege WHERE RoleId = @RoleId;", connection, transaction))
        {
            deleteCommand.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO dbo.RolePrivilege (MenuId, RoleId, [View], [Add], [Edit], [Insert], [Update], [Delete])
            SELECT @MenuId, @RoleId, @View, @Add, @Edit, @Add, @Update, @Delete
            WHERE EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Id = @MenuId AND IsActive = 1);
            """;
        foreach (var privilege in privileges)
        {
            await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.Add("@MenuId", SqlDbType.Int).Value = privilege.MenuId;
            insertCommand.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId;
            insertCommand.Parameters.Add("@View", SqlDbType.Bit).Value = privilege.View;
            insertCommand.Parameters.Add("@Add", SqlDbType.Bit).Value = privilege.Add;
            insertCommand.Parameters.Add("@Edit", SqlDbType.Bit).Value = privilege.Edit;
            insertCommand.Parameters.Add("@Update", SqlDbType.Bit).Value = privilege.Update;
            insertCommand.Parameters.Add("@Delete", SqlDbType.Bit).Value = privilege.Delete;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdminUser?> GetActiveUserByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT userAccount.Id, userAccount.FirstName, userAccount.LastName, userAccount.UserName,
                   userAccount.[Password], userAccount.IsActive, userAccount.RoleId, userRole.RoleName
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
            Name = $"{reader.GetString(1)} {reader.GetString(2)}".Trim(),
            FirstName = reader.GetString(1),
            LastName = reader.GetString(2),
            UserName = reader.GetString(3),
            Password = reader.GetString(4),
            IsActive = reader.GetBoolean(5),
            RoleId = reader.GetInt32(6),
            RoleName = reader.GetString(7)
        };
    }

    public async Task<IReadOnlyList<ItemMenu>> GetMenusForUserAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT menu.Id, menu.Name, menu.ParentId, menu.ControllerName, menu.ActionName,
                   CAST(COALESCE(privilege.[View], 0) AS bit),
                   CAST(COALESCE(privilege.[Add], 0) AS bit),
                   CAST(COALESCE(privilege.[Edit], 0) AS bit),
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
                CanAdd = reader.GetBoolean(6),
                CanEdit = reader.GetBoolean(7),
                CanInsert = reader.GetBoolean(8),
                CanUpdate = reader.GetBoolean(9),
                CanDelete = reader.GetBoolean(10)
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
            MenuPermission.Add => "[Add]",
            MenuPermission.Edit => "[Edit]",
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
                    ON privilege.RoleId = userAccount.RoleId
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

    private static void AddAdminUserParameters(SqlCommand command, AdminUserInput user)
    {
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = $"{user.FirstName.Trim()} {user.LastName.Trim()}".Trim();
        command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = user.FirstName.Trim();
        command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = user.LastName.Trim();
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = user.Email.Trim();
        command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = user.UserName.Trim();
        command.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)user.Phone?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@DOB", SqlDbType.Date).Value = (object?)user.DOB?.Date ?? DBNull.Value;
        command.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = (object?)user.City?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@State", SqlDbType.NVarChar, 100).Value = (object?)user.State?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@Country", SqlDbType.NVarChar, 100).Value = (object?)user.Country?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@IsSystemUser", SqlDbType.Bit).Value = user.IsSystemUser;
        command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = user.IsActive;
        command.Parameters.Add("@RoleId", SqlDbType.Int).Value = user.RoleId;
    }
}
