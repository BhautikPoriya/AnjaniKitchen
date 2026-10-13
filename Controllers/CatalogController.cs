using Microsoft.AspNetCore.Mvc;
using AnjaniKitchen.Models;
using AnjaniKitchen.Services;

namespace AnjaniKitchen.Controllers;

public sealed class CatalogController : Controller
{
    private readonly IAdminDataService _adminDataService;

    public CatalogController(IAdminDataService adminDataService)
    {
        _adminDataService = adminDataService;
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

        var model = new DashboardViewModel
        {
            AdminName = adminName,
            RoleName = roleName,
            InitialPage = "categories",
            Menus = await _adminDataService.GetMenusForUserAsync(userId.Value, roleId.Value, cancellationToken),
            Categories = await _adminDataService.GetCategoriesAsync(cancellationToken)
        };
        return View(model);
    }

    [HttpGet]
    public Task<IActionResult> Dishes(CancellationToken cancellationToken)
    {
        return OpenPageAsync("dishes", cancellationToken);
    }

    private async Task<IActionResult> OpenPageAsync(string page, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Catalog", "dishes", MenuPermission.View, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return RedirectToAction("Index", "Home", new { page });
    }
}
