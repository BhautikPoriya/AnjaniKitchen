using Microsoft.AspNetCore.Mvc;
using AnjaniKitchen.Services;

namespace AnjaniKitchen.Controllers;

public sealed class OrderController : Controller
{
    private readonly IAdminDataService _adminDataService;

    public OrderController(IAdminDataService adminDataService)
    {
        _adminDataService = adminDataService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        if (userId is null || roleId is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!await _adminDataService.HasMenuPermissionAsync(
                userId.Value, roleId.Value, "Order", "Index", MenuPermission.View, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return RedirectToAction("Index", "Home", new { page = "orders" });
    }
}
