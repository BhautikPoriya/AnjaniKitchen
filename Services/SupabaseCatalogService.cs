using AnjaniKitchen.Models;
using Npgsql;
using NpgsqlTypes;

namespace AnjaniKitchen.Services;

public sealed class SupabaseCatalogService : ISupabaseCatalogService
{
    private readonly string? _connectionString;

    public SupabaseCatalogService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Supabase");
    }

    public async Task UpsertCategoryAsync(MenuCategory category, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.categories (id, parent_id, name, details, img, is_active)
            VALUES (@id, @parent_id, @name, @details, @img, @is_active)
            ON CONFLICT (id) DO UPDATE
            SET parent_id = EXCLUDED.parent_id,
                name = EXCLUDED.name,
                details = EXCLUDED.details,
                img = EXCLUDED.img,
                is_active = EXCLUDED.is_active;
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, category.Id);
        command.Parameters.AddWithValue("parent_id", NpgsqlDbType.Bigint, (object?)category.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, category.Name);
        command.Parameters.AddWithValue("details", NpgsqlDbType.Varchar, category.Details);
        command.Parameters.AddWithValue("img", NpgsqlDbType.Text, (object?)category.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("is_active", NpgsqlDbType.Boolean, category.IsActive);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertDishAsync(Dish dish, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.dishes
                (id, name, price, old_price, description, category_id, img, is_new, is_active)
            VALUES
                (@id, @name, @price, @old_price, @description, @category_id, @img, @is_new, @is_active)
            ON CONFLICT (id) DO UPDATE
            SET name = EXCLUDED.name,
                price = EXCLUDED.price,
                old_price = EXCLUDED.old_price,
                description = EXCLUDED.description,
                category_id = EXCLUDED.category_id,
                img = EXCLUDED.img,
                is_new = EXCLUDED.is_new,
                is_active = EXCLUDED.is_active;
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, dish.Id);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, dish.Name);
        command.Parameters.AddWithValue("price", NpgsqlDbType.Numeric, dish.Price);
        command.Parameters.AddWithValue("old_price", NpgsqlDbType.Numeric, (object?)dish.OldPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("description", NpgsqlDbType.Varchar, dish.Description);
        command.Parameters.AddWithValue("category_id", NpgsqlDbType.Bigint, dish.CategoryId);
        command.Parameters.AddWithValue("img", NpgsqlDbType.Text, (object?)dish.ImageUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("is_new", NpgsqlDbType.Boolean, dish.IsNew);
        command.Parameters.AddWithValue("is_active", NpgsqlDbType.Boolean, dish.IsActive);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateDishStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE public.dishes SET is_active = @is_active WHERE id = @id;";

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        command.Parameters.AddWithValue("is_active", NpgsqlDbType.Boolean, isActive);
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException($"Supabase dish {id} was not found while updating its status.");
        }
    }

    public async Task UpdateCategoryStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE public.categories SET is_active = @is_active WHERE id = @id;";

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        command.Parameters.AddWithValue("is_active", NpgsqlDbType.Boolean, isActive);
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException($"Supabase category {id} was not found while updating its status.");
        }
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM public.categories WHERE id = @id;";

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteDishAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM public.dishes WHERE id = @id;";

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set the ConnectionStrings__Supabase environment variable.");
        }

        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
