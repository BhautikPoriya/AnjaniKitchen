using System.Diagnostics;
using AnjaniKitchen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AnjaniKitchen.Models;

namespace AnjaniKitchen.Controllers;

public class HomeController : Controller
{
    private readonly IAdminDataService _adminDataService;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;

    public HomeController(IAdminDataService adminDataService, IPasswordHasher<AdminUser> passwordHasher)
    {
        _adminDataService = adminDataService;
        _passwordHasher = passwordHasher;
    }

    public async Task<IActionResult> Index(string? page, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleId = HttpContext.Session.GetInt32("KitchenRoleId");
        var adminName = HttpContext.Session.GetString("KitchenAdminName");
        var roleName = HttpContext.Session.GetString("KitchenRoleName");
        if (userId is null || roleId is null || adminName is null || roleName is null)
        {
            return View("Login");
        }

        var menus = await _adminDataService.GetMenusForUserAsync(userId.Value, roleId.Value, cancellationToken);
        var initialPage = page?.ToLowerInvariant() switch
        {
            null or "" => GetDefaultPage(menus),
            "dashboard" => "dashboard",
            "orders" => "orders",
            "categories" => "categories",
            "dishes" => "dishes",
            _ => null
        };
        if (initialPage is "categories" or "dishes")
        {
            var catalogAction = initialPage == "categories"
                ? nameof(CatalogController.Categories)
                : nameof(CatalogController.Dishes);
            return RedirectToAction(catalogAction, "Catalog");
        }

        if (initialPage is null || FindPageMenu(menus, initialPage) is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var model = new DashboardViewModel
        {
            AdminName = adminName,
            RoleName = roleName,
            InitialPage = initialPage,
            Menus = menus
        };
        return View(model);
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        return RedirectToAction(nameof(Index), new { page = "dashboard" });
    }

    private static string? GetDefaultPage(IReadOnlyList<ItemMenu> menus)
    {
        return new[] { "dashboard", "orders", "categories", "dishes" }
            .FirstOrDefault(page => FindPageMenu(menus, page) is not null);
    }

    private static ItemMenu? FindPageMenu(IReadOnlyList<ItemMenu> menus, string page)
    {
        var (controller, action) = page switch
        {
            "dashboard" => ("Home", "Dashboard"),
            "orders" => ("Order", "Index"),
            "categories" => ("Catalog", "categories"),
            "dishes" => ("Catalog", "dishes"),
            _ => (string.Empty, string.Empty)
        };

        return menus.FirstOrDefault(menu =>
            menu.CanView &&
            string.Equals(menu.ControllerName, controller, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(menu.ActionName, action, StringComparison.OrdinalIgnoreCase));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(password))
        {
            var user = await _adminDataService.GetActiveUserByUserNameAsync(username, cancellationToken);
            if (user is not null &&
                _passwordHasher.VerifyHashedPassword(user, user.Password, password) != PasswordVerificationResult.Failed)
            {
                HttpContext.Session.SetInt32("KitchenAdminId", user.Id);
                HttpContext.Session.SetInt32("KitchenRoleId", user.RoleId);
                HttpContext.Session.SetString("KitchenAdminName", user.Name);
                HttpContext.Session.SetString("KitchenRoleName", user.RoleName);
                return RedirectToAction(nameof(Index));
            }
        }

        ViewData["LoginError"] = "That username and password do not match.";
        return View("Login");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
