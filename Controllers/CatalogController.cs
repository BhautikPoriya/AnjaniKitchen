using Microsoft.AspNetCore.Mvc;
using AnjaniKitchen.Models;
using AnjaniKitchen.Services;

namespace AnjaniKitchen.Controllers;

public sealed class CatalogController : Controller
{
    private readonly IAdminDataService _adminDataService;
    private readonly ISupabaseCatalogService _supabaseCatalogService;
    private readonly ICategoryImageStorage _categoryImageStorage;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        IAdminDataService adminDataService,
        ISupabaseCatalogService supabaseCatalogService,
        ICategoryImageStorage categoryImageStorage,
        ILogger<CatalogController> logger)
    {
        _adminDataService = adminDataService;
        _supabaseCatalogService = supabaseCatalogService;
        _categoryImageStorage = categoryImageStorage;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        var adminName = HttpContext.Session.GetString("KitchenAdminName");
        var roleName = HttpContext.Session.GetString("KitchenRoleName");
        if (userId is null || roleId is null || adminName is null || roleName is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "categories", MenuPermission.View, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var categories = await _adminDataService.GetCategoriesAsync(cancellationToken);
        foreach (var category in categories)
        {
            category.ImageUrl = _categoryImageStorage.GetImageUrl(category.Img);
        }

        var model = new DashboardViewModel
        {
            AdminName = adminName,
            RoleName = roleName,
            InitialPage = "categories",
            Menus = await _adminDataService.GetMenusForUserAsync(userId.Value, roleId.Value, cancellationToken),
            Categories = categories
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> CreateCategory(
        [FromForm] CategoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before creating a category." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "categories", MenuPermission.Insert, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to create categories." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Enter a category name, details, and an image." });
        }

        var name = request.Name.Trim();
        var details = request.Details.Trim();
        if (name.Length == 0 || details.Length == 0)
        {
            return BadRequest(new { error = "Category name and details cannot be empty." });
        }

        IReadOnlyList<MenuCategory> categories = Array.Empty<MenuCategory>();
        if (request.ParentId is not null)
        {
            categories = await _adminDataService.GetCategoriesAsync(cancellationToken);
            if (!categories.Any(category => category.Id == request.ParentId && category.ParentId is null))
            {
                return BadRequest(new { error = "Choose a valid parent category for the subcategory." });
            }
        }

        var image = request.Image;
        if (image is null && request.ParentId is null)
        {
            return BadRequest(new { error = "Choose an image for the main category." });
        }

        if (image is not null)
        {
            var imageError = ValidateCategoryImage(image);
            if (imageError is not null)
            {
                return BadRequest(new { error = imageError });
            }
        }

        string? imagePublicId = null;
        if (image is not null)
        {
            try
            {
                imagePublicId = await _categoryImageStorage.UploadAsync(image, cancellationToken);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or CategoryImageStorageException or HttpRequestException
                || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return CloudinaryError(exception, "upload", request.Name);
            }
        }

        MenuCategory category;
        try
        {
            category = await _adminDataService.CreateCategoryAsync(name, details, imagePublicId, request.ParentId, cancellationToken);
        }
        catch
        {
            if (imagePublicId is not null)
            {
                await TryDeleteCloudinaryImageAsync(imagePublicId);
            }

            throw;
        }

        category.ImageUrl = _categoryImageStorage.GetImageUrl(category.Img);
        string? parentSyncWarning = null;
        if (category.ParentId is int parentId)
        {
            var parent = categories.FirstOrDefault(item => item.Id == parentId);
            if (parent is not null)
            {
                parent.ImageUrl = _categoryImageStorage.GetImageUrl(parent.Img);
                parentSyncWarning = await SyncCategoryToSupabaseAsync(parent, "sync parent for", cancellationToken);
            }
        }
        var supabaseWarning = await SyncCategoryToSupabaseAsync(category, "create", cancellationToken);
        return CreatedAtAction(nameof(Categories), new { id = category.Id }, new
        {
            category.Id,
            category.ParentId,
            category.Name,
            category.Details,
            category.ImageUrl,
            category.IsActive,
            warning = CombineWarnings(parentSyncWarning, supabaseWarning)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> UpdateCategory(
        int id,
        [FromForm] CategoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before updating a category." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "categories", MenuPermission.Update, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to update categories." });
        }

        if (!ModelState.IsValid || id <= 0)
        {
            return BadRequest(new { error = "Enter a valid category name and details." });
        }

        var name = request.Name.Trim();
        var details = request.Details.Trim();
        if (name.Length == 0 || details.Length == 0)
        {
            return BadRequest(new { error = "Category name and details cannot be empty." });
        }

        string? newImagePublicId = null;
        if (request.Image is not null)
        {
            var imageError = ValidateCategoryImage(request.Image);
            if (imageError is not null)
            {
                return BadRequest(new { error = imageError });
            }

            try
            {
                newImagePublicId = await _categoryImageStorage.UploadAsync(request.Image, cancellationToken);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or CategoryImageStorageException or HttpRequestException
                || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return CloudinaryError(exception, "upload", request.Name);
            }
        }

        CategoryUpdateResult? update;
        try
        {
            update = await _adminDataService.UpdateCategoryAsync(
                id,
                name,
                details,
                newImagePublicId,
                request.RemoveImage && newImagePublicId is null,
                cancellationToken);
        }
        catch
        {
            if (newImagePublicId is not null)
            {
                await TryDeleteCloudinaryImageAsync(newImagePublicId);
            }

            throw;
        }

        if (update is null)
        {
            if (newImagePublicId is not null)
            {
                await TryDeleteCloudinaryImageAsync(newImagePublicId);
            }

            return NotFound(new { error = "Category was not found." });
        }

        update.Category.ImageUrl = _categoryImageStorage.GetImageUrl(update.Category.Img);
        var imageWasChanged = newImagePublicId is not null || request.RemoveImage;
        var imageCleanupWarning = imageWasChanged && !string.IsNullOrWhiteSpace(update.PreviousImagePublicId)
            ? await TryDeleteCloudinaryImageAsync(update.PreviousImagePublicId)
            : null;
        var supabaseWarning = await SyncCategoryToSupabaseAsync(update.Category, "update", cancellationToken);

        return Ok(new
        {
            update.Category.Id,
            update.Category.ParentId,
            update.Category.Name,
            update.Category.Details,
            update.Category.ImageUrl,
            update.Category.IsActive,
            warning = CombineWarnings(imageCleanupWarning, supabaseWarning)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before deleting a category." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "categories", MenuPermission.Delete, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to delete categories." });
        }

        var deletion = await _adminDataService.DeleteCategoryAsync(id, cancellationToken);
        if (!deletion.Deleted)
        {
            if (deletion.HasDishes)
            {
                return Conflict(new { error = "This category has dishes. Move or delete those dishes before deleting the category." });
            }

            return NotFound(new { error = "Category was not found." });
        }

        var warning = string.IsNullOrWhiteSpace(deletion.ImagePublicId)
            ? null
            : await TryDeleteCloudinaryImageAsync(deletion.ImagePublicId);
        var supabaseWarning = await DeleteCategoryFromSupabaseAsync(id, cancellationToken);
        return Ok(new { deleted = true, warning = CombineWarnings(warning, supabaseWarning) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategoryStatus(
        int id,
        [FromForm] bool isActive,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before updating a category." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "categories", MenuPermission.Update, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to update categories." });
        }

        if (id <= 0)
        {
            return BadRequest(new { error = "Choose a valid category." });
        }

        var updatedStatus = await _adminDataService.UpdateCategoryStatusAsync(id, isActive, cancellationToken);
        if (updatedStatus is null)
        {
            return NotFound(new { error = "Category was not found." });
        }

        var supabaseWarning = await UpdateCategoryStatusInSupabaseAsync(id, updatedStatus.Value, cancellationToken);
        return Ok(new { isActive = updatedStatus.Value, warning = supabaseWarning });
    }

    private static string? ValidateCategoryImage(IFormFile image)
    {
        var extension = Path.GetExtension(image.FileName);
        var isJpeg = string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        var isPng = string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase);
        var expectedContentType = isJpeg ? "image/jpeg" : isPng ? "image/png" : null;
        if (image.Length == 0
            || image.Length > 5 * 1024 * 1024
            || expectedContentType is null
            || (!string.IsNullOrWhiteSpace(image.ContentType)
                && !string.Equals(image.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase)))
        {
            return "Choose a JPG, JPEG, or PNG image up to 5 MB.";
        }

        Span<byte> signature = stackalloc byte[8];
        using var stream = image.OpenReadStream();
        var bytesRead = stream.Read(signature);
        var hasJpegSignature = bytesRead >= 3
            && signature[0] == 0xFF
            && signature[1] == 0xD8
            && signature[2] == 0xFF;
        var hasPngSignature = bytesRead == 8
            && signature.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        if (isJpeg && !hasJpegSignature || isPng && !hasPngSignature)
        {
            return "The selected file is not a valid JPG, JPEG, or PNG image.";
        }

        return null;
    }

    private IActionResult CloudinaryError(Exception exception, string operation, string categoryName)
    {
        if (exception is InvalidOperationException)
        {
            _logger.LogError(exception, "Cloudinary is not configured for category image {Operation}.", operation);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }

        _logger.LogError(exception, "Cloudinary failed to {Operation} an image for category {CategoryName}.", operation, categoryName);
        return StatusCode(StatusCodes.Status502BadGateway, new
        {
            error = exception is HttpRequestException or TaskCanceledException
                ? "Cloudinary could not be reached. Please try again."
                : "The image could not be processed by Cloudinary. Please try again."
        });
    }

    private async Task<string?> TryDeleteCloudinaryImageAsync(string publicId)
    {
        try
        {
            await _categoryImageStorage.DeleteAsync(publicId, CancellationToken.None);
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or CategoryImageStorageException or HttpRequestException)
        {
            _logger.LogError(exception, "Could not remove image {ImagePublicId} from Cloudinary.", publicId);
            return "Saved, but the previous image could not be removed from Cloudinary. Check the application logs.";
        }
    }

    [HttpGet]
    public async Task<IActionResult> Dishes(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        var adminName = HttpContext.Session.GetString("KitchenAdminName");
        var roleName = HttpContext.Session.GetString("KitchenRoleName");
        if (userId is null || roleId is null || adminName is null || roleName is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "dishes", MenuPermission.View, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var dishes = await _adminDataService.GetDishesAsync(cancellationToken);
        foreach (var dish in dishes)
        {
            dish.ImageUrl = _categoryImageStorage.GetImageUrl(dish.Img);
        }

        var model = new DashboardViewModel
        {
            AdminName = adminName,
            RoleName = roleName,
            InitialPage = "dishes",
            Menus = await _adminDataService.GetMenusForUserAsync(userId.Value, roleId.Value, cancellationToken),
            Categories = await _adminDataService.GetCategoriesAsync(cancellationToken),
            Dishes = dishes
        };
        return View("~/Views/Home/Index.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> CreateDish(
        [FromForm] DishUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before creating a dish." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "dishes", MenuPermission.Insert, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to create dishes." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Enter a dish name, valid price, and category." });
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return BadRequest(new { error = "Dish name cannot be empty." });
        }

        var categories = await _adminDataService.GetCategoriesAsync(cancellationToken);
        var category = categories.FirstOrDefault(item => item.Id == request.CategoryId);
        if (category is null)
        {
            return BadRequest(new { error = "Choose a valid category for the dish." });
        }

        if (request.Image is null)
        {
            return BadRequest(new { error = "Choose an image for the dish." });
        }

        var imageError = ValidateCategoryImage(request.Image);
        if (imageError is not null)
        {
            return BadRequest(new { error = imageError });
        }

        string imagePublicId;
        try
        {
            imagePublicId = await _categoryImageStorage.UploadAsync(request.Image, cancellationToken, "dishes");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or CategoryImageStorageException or HttpRequestException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return CloudinaryError(exception, "upload", request.Name);
        }

        Dish dish;
        try
        {
            dish = await _adminDataService.CreateDishAsync(request, name, imagePublicId, cancellationToken);
        }
        catch
        {
            await TryDeleteCloudinaryImageAsync(imagePublicId);
            throw;
        }

        dish.ImageUrl = _categoryImageStorage.GetImageUrl(dish.Img);
        var supabaseWarning = await SyncDishToSupabaseAsync(dish, categories, "create", cancellationToken);
        return CreatedAtAction(nameof(Dishes), new { id = dish.Id }, new
        {
            dish.Id,
            dish.Name,
            dish.Price,
            dish.OldPrice,
            dish.Description,
            dish.CategoryId,
            dish.CategoryName,
            dish.ParentCategoryName,
            dish.ImageUrl,
            dish.IsNew,
            dish.IsActive,
            warning = supabaseWarning
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_500_000)]
    public async Task<IActionResult> UpdateDish(
        int id,
        [FromForm] DishUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before updating a dish." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "dishes", MenuPermission.Update, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to update dishes." });
        }

        if (id <= 0 || !ModelState.IsValid)
        {
            return BadRequest(new { error = "Enter a valid dish name, price, and category." });
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return BadRequest(new { error = "Dish name cannot be empty." });
        }

        var categories = await _adminDataService.GetCategoriesAsync(cancellationToken);
        if (!categories.Any(item => item.Id == request.CategoryId))
        {
            return BadRequest(new { error = "Choose a valid category for the dish." });
        }

        string? newImagePublicId = null;
        if (request.Image is not null)
        {
            var imageError = ValidateCategoryImage(request.Image);
            if (imageError is not null)
            {
                return BadRequest(new { error = imageError });
            }

            try
            {
                newImagePublicId = await _categoryImageStorage.UploadAsync(request.Image, cancellationToken, "dishes");
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or CategoryImageStorageException or HttpRequestException
                || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return CloudinaryError(exception, "upload", request.Name);
            }
        }

        DishUpdateResult? update;
        try
        {
            update = await _adminDataService.UpdateDishAsync(
                id,
                request,
                name,
                newImagePublicId,
                request.RemoveImage && newImagePublicId is null,
                cancellationToken);
        }
        catch
        {
            if (newImagePublicId is not null)
            {
                await TryDeleteCloudinaryImageAsync(newImagePublicId);
            }

            throw;
        }

        if (update is null)
        {
            if (newImagePublicId is not null)
            {
                await TryDeleteCloudinaryImageAsync(newImagePublicId);
            }

            return NotFound(new { error = "Dish was not found." });
        }

        update.Dish.ImageUrl = _categoryImageStorage.GetImageUrl(update.Dish.Img);
        var imageCleanupWarning = (newImagePublicId is not null || request.RemoveImage)
            && !string.IsNullOrWhiteSpace(update.PreviousImagePublicId)
                ? await TryDeleteCloudinaryImageAsync(update.PreviousImagePublicId)
                : null;
        var supabaseWarning = await SyncDishToSupabaseAsync(update.Dish, categories, "update", cancellationToken);

        return Ok(new
        {
            update.Dish.Id,
            update.Dish.Name,
            update.Dish.Price,
            update.Dish.OldPrice,
            update.Dish.Description,
            update.Dish.CategoryId,
            update.Dish.CategoryName,
            update.Dish.ParentCategoryName,
            update.Dish.ImageUrl,
            update.Dish.IsNew,
            update.Dish.IsActive,
            warning = CombineWarnings(imageCleanupWarning, supabaseWarning)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDishStatus(
        int id,
        [FromForm] bool isActive,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before updating a dish." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "dishes", MenuPermission.Update, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to update dishes." });
        }

        if (id <= 0)
        {
            return BadRequest(new { error = "Choose a valid dish." });
        }

        var updatedStatus = await _adminDataService.UpdateDishStatusAsync(id, isActive, cancellationToken);
        if (updatedStatus is null)
        {
            return NotFound(new { error = "Dish was not found." });
        }

        var supabaseWarning = await SyncDishStatusToSupabaseAsync(id, updatedStatus.Value, cancellationToken);
        return Ok(new { isActive = updatedStatus.Value, warning = supabaseWarning });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDish(int id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return Unauthorized(new { error = "Please sign in again before deleting a dish." });
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "dishes", MenuPermission.Delete, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have permission to delete dishes." });
        }

        var deletion = await _adminDataService.DeleteDishAsync(id, cancellationToken);
        if (!deletion.Deleted)
        {
            return NotFound(new { error = "Dish was not found." });
        }

        var warning = string.IsNullOrWhiteSpace(deletion.ImagePublicId)
            ? null
            : await TryDeleteCloudinaryImageAsync(deletion.ImagePublicId);
        var supabaseWarning = await DeleteDishFromSupabaseAsync(id, cancellationToken);
        return Ok(new { deleted = true, warning = CombineWarnings(warning, supabaseWarning) });
    }

    private async Task<string?> DeleteCategoryFromSupabaseAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _supabaseCatalogService.DeleteCategoryAsync(id, cancellationToken);
            return null;
        }
        catch (Exception exception) when (IsSupabaseSyncException(exception))
        {
            _logger.LogError(exception, "Could not delete category {CategoryId} from Supabase.", id);
            return "Deleted from the app database, but could not delete this category from Supabase. Check the Supabase connection and logs.";
        }
    }

    private async Task<string?> DeleteDishFromSupabaseAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _supabaseCatalogService.DeleteDishAsync(id, cancellationToken);
            return null;
        }
        catch (Exception exception) when (IsSupabaseSyncException(exception))
        {
            _logger.LogError(exception, "Could not delete dish {DishId} from Supabase.", id);
            return "Deleted from the app database, but could not delete this dish from Supabase. Check the Supabase connection and logs.";
        }
    }

    private async Task<string?> SyncCategoryToSupabaseAsync(
        MenuCategory category,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _supabaseCatalogService.UpsertCategoryAsync(category, cancellationToken);
            return null;
        }
        catch (Exception exception) when (IsSupabaseSyncException(exception))
        {
            _logger.LogError(exception, "Could not {Operation} category {CategoryId} in Supabase.", operation, category.Id);
            return "Saved in the app database, but could not sync this category to Supabase. Check the Supabase connection and logs.";
        }
    }

    private async Task<string?> SyncDishToSupabaseAsync(
        Dish dish,
        IReadOnlyList<MenuCategory> categories,
        string operation,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string?>();
        var category = categories.FirstOrDefault(item => item.Id == dish.CategoryId);
        if (category is null)
        {
            _logger.LogError("Could not sync dish {DishId}: category {CategoryId} was not found.", dish.Id, dish.CategoryId);
            return "Saved in the app database, but its category could not be synced to Supabase.";
        }

        if (category.ParentId is int parentId)
        {
            var parent = categories.FirstOrDefault(item => item.Id == parentId);
            if (parent is not null)
            {
                parent.ImageUrl = _categoryImageStorage.GetImageUrl(parent.Img);
                warnings.Add(await SyncCategoryToSupabaseAsync(parent, "sync parent for dish", cancellationToken));
            }
        }

        category.ImageUrl = _categoryImageStorage.GetImageUrl(category.Img);
        warnings.Add(await SyncCategoryToSupabaseAsync(category, "sync category for dish", cancellationToken));

        try
        {
            await _supabaseCatalogService.UpsertDishAsync(dish, cancellationToken);
        }
        catch (Exception exception) when (IsSupabaseSyncException(exception))
        {
            _logger.LogError(exception, "Could not {Operation} dish {DishId} in Supabase.", operation, dish.Id);
            warnings.Add("Saved in the app database, but could not sync this dish to Supabase. Check the Supabase connection and logs.");
        }

        return CombineWarnings(warnings.ToArray());
    }

    private async Task<string?> SyncDishStatusToSupabaseAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            await _supabaseCatalogService.UpdateDishStatusAsync(id, isActive, cancellationToken);
            return null;
        }
        catch (Exception exception) when (IsSupabaseSyncException(exception))
        {
            _logger.LogError(exception, "Could not update the status of dish {DishId} in Supabase.", id);
            return "Dish status was saved in the app database, but could not be synced to Supabase. Check the Supabase connection and logs.";
        }
    }

    private async Task<string?> UpdateCategoryStatusInSupabaseAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            await _supabaseCatalogService.UpdateCategoryStatusAsync(id, isActive, cancellationToken);
            return null;
        }
        catch (Exception exception) when (IsSupabaseSyncException(exception))
        {
            _logger.LogError(exception, "Could not update the status of category {CategoryId} in Supabase.", id);
            return "Category status was saved in the app database, but could not be synced to Supabase. Check the Supabase connection and logs.";
        }
    }

    private static bool IsSupabaseSyncException(Exception exception)
    {
        return exception is Npgsql.NpgsqlException
            or InvalidOperationException
            or ArgumentException
            or TimeoutException;
    }

    private static string? CombineWarnings(params string?[] warnings)
    {
        var messages = warnings.Where(warning => !string.IsNullOrWhiteSpace(warning));
        var combined = string.Join(" ", messages);
        return combined.Length == 0 ? null : combined;
    }

}
