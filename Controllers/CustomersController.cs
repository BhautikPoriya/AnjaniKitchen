using AnjaniKitchen.Models;
using AnjaniKitchen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AnjaniKitchen.Controllers;

public sealed class CustomersController : Controller
{
    private const string MenuController = "Customers";
    private readonly IAdminDataService _adminDataService;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;

    public CustomersController(IAdminDataService adminDataService, IPasswordHasher<AdminUser> passwordHasher)
    {
        _adminDataService = adminDataService;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var roleId))
            return RedirectToAction("Index", "Home");
        if (!await HasPermission(userId, roleId, nameof(Users), MenuPermission.View, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        await PrepareAdminLayoutAsync(userId, roleId, cancellationToken);
        return View(new AdminUsersViewModel
        {
            Users = await _adminDataService.GetAdminUsersAsync(cancellationToken),
            Roles = await _adminDataService.GetRolesAsync(cancellationToken),
            Permissions = await GetMenuPermission(userId, roleId, nameof(Users), cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> UserForm(int? id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var roleId))
            return RedirectToAction("Index", "Home");
        var permission = id is null ? MenuPermission.Add : MenuPermission.Edit;
        if (!await HasPermission(userId, roleId, nameof(Users), permission, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        var roles = await _adminDataService.GetRolesAsync(cancellationToken);
        var user = id is null
            ? new AdminUserInput()
            : await ToInputAsync(id.Value, cancellationToken);
        if (user is null)
            return NotFound();

        await PrepareAdminLayoutAsync(userId, roleId, cancellationToken);
        return View(new AdminUserFormViewModel { User = user, Roles = roles, IsEdit = id is not null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser([Bind(Prefix = "User")] AdminUserInput user, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var currentUserId, out var roleId))
            return Unauthorized();
        var permission = user.Id == 0 ? MenuPermission.Add : MenuPermission.Update;
        if (!await HasPermission(currentUserId, roleId, nameof(Users), permission, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        if (user.Id == 0 && string.IsNullOrWhiteSpace(user.Password))
            ModelState.AddModelError("User.Password", "A password is required for a new user.");
        else if (!string.IsNullOrEmpty(user.Password) && user.Password.Length < 8)
            ModelState.AddModelError("User.Password", "Password must contain at least 8 characters.");

        var roles = await _adminDataService.GetRolesAsync(cancellationToken);
        if (!roles.Any(role => role.Id == user.RoleId && role.IsActive))
            ModelState.AddModelError("User.RoleId", "Choose an active role.");

        if (!ModelState.IsValid)
        {
            await PrepareAdminLayoutAsync(currentUserId, roleId, cancellationToken);
            return View("UserForm", new AdminUserFormViewModel { User = user, Roles = roles, IsEdit = user.Id != 0 });
        }

        var userIdentity = new AdminUser { Id = user.Id, Name = $"{user.FirstName} {user.LastName}".Trim() };
        var passwordHash = string.IsNullOrEmpty(user.Password)
            ? null
            : _passwordHasher.HashPassword(userIdentity, user.Password);
        try
        {
            var savedId = await _adminDataService.SaveAdminUserAsync(user, passwordHash, cancellationToken);
            if (savedId == 0)
                return NotFound();
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            ModelState.AddModelError(string.Empty, "That email address or username is already in use.");
            await PrepareAdminLayoutAsync(currentUserId, roleId, cancellationToken);
            return View("UserForm", new AdminUserFormViewModel { User = user, Roles = roles, IsEdit = user.Id != 0 });
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var currentUserId, out var roleId))
            return Unauthorized();
        if (id == currentUserId)
            return BadRequest("You cannot delete the account you are currently using.");
        if (!await HasPermission(currentUserId, roleId, nameof(Users), MenuPermission.Delete, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        if (!await _adminDataService.DeleteAdminUserAsync(id, cancellationToken))
            return NotFound();
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var roleId))
            return RedirectToAction("Index", "Home");
        if (!await HasPermission(userId, roleId, nameof(Roles), MenuPermission.View, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        await PrepareAdminLayoutAsync(userId, roleId, cancellationToken);
        return View(new AdminRolesViewModel
        {
            Roles = await _adminDataService.GetRolesAsync(cancellationToken),
            Permissions = await GetMenuPermission(userId, roleId, nameof(Roles), cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> RoleForm(int? id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var roleId))
            return RedirectToAction("Index", "Home");
        var permission = id is null ? MenuPermission.Add : MenuPermission.Edit;
        if (!await HasPermission(userId, roleId, nameof(Roles), permission, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        var role = id is null
            ? new AdminRole { IsActive = true }
            : (await _adminDataService.GetRolesAsync(cancellationToken)).FirstOrDefault(item => item.Id == id.Value);
        await PrepareAdminLayoutAsync(userId, roleId, cancellationToken);
        return role is null ? NotFound() : View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRole(int id, string roleName, bool isActive, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var currentRoleId))
            return Unauthorized();
        var permission = id == 0 ? MenuPermission.Add : MenuPermission.Update;
        if (!await HasPermission(userId, currentRoleId, nameof(Roles), permission, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        var cleanedName = roleName?.Trim() ?? string.Empty;
        if (cleanedName.Length is 0 or > 200)
        {
            ModelState.AddModelError(nameof(roleName), "Enter a role name between 1 and 200 characters.");
            await PrepareAdminLayoutAsync(userId, currentRoleId, cancellationToken);
            return View("RoleForm", new AdminRole { Id = id, RoleName = cleanedName, IsActive = isActive });
        }

        if (await _adminDataService.SaveRoleAsync(id, cleanedName, isActive, cancellationToken) == 0)
            return NotFound();
        return RedirectToAction(nameof(Roles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var roleId))
            return Unauthorized();
        if (!await HasPermission(userId, roleId, nameof(Roles), MenuPermission.Delete, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!await _adminDataService.DeleteRoleAsync(id, cancellationToken))
        {
            TempData["CustomerAdminError"] = "This role is assigned to users or no longer exists, so it cannot be deleted.";
        }
        return RedirectToAction(nameof(Roles));
    }

    [HttpGet]
    public async Task<IActionResult> RolePrivileges(int? roleId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var currentRoleId))
            return RedirectToAction("Index", "Home");
        if (!await HasPermission(userId, currentRoleId, nameof(RolePrivileges), MenuPermission.View, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        var roles = await _adminDataService.GetRolesAsync(cancellationToken);
        var selectedRoleId = roleId is not null && roles.Any(role => role.Id == roleId) ? roleId : null;
        var privileges = selectedRoleId is null
            ? Array.Empty<RolePrivilegeInput>()
            : await _adminDataService.GetRolePrivilegesAsync(selectedRoleId.Value, cancellationToken);
        await PrepareAdminLayoutAsync(userId, currentRoleId, cancellationToken);
        return View(new RolePrivilegesViewModel
        {
            Roles = roles,
            Menus = await _adminDataService.GetAllMenusAsync(cancellationToken),
            Privileges = privileges.ToDictionary(item => item.MenuId),
            SelectedRoleId = selectedRoleId,
            CanUpdate = await HasPermission(userId, currentRoleId, nameof(RolePrivileges), MenuPermission.Update, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRolePrivileges(int roleId, List<RolePrivilegeInput> privileges, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var currentRoleId))
            return Unauthorized();
        if (!await HasPermission(userId, currentRoleId, nameof(RolePrivileges), MenuPermission.Update, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden);

        var roles = await _adminDataService.GetRolesAsync(cancellationToken);
        if (!roles.Any(role => role.Id == roleId))
            return BadRequest("Choose a valid role.");
        var menuIds = (await _adminDataService.GetAllMenusAsync(cancellationToken)).Select(menu => menu.Id).ToHashSet();
        if (privileges.Any(item => !menuIds.Contains(item.MenuId)) || privileges.Select(item => item.MenuId).Distinct().Count() != privileges.Count)
            return BadRequest("The submitted menu permissions are invalid.");

        await _adminDataService.SaveRolePrivilegesAsync(roleId, privileges, cancellationToken);
        return RedirectToAction(nameof(RolePrivileges), new { roleId });
    }

    private async Task<AdminUserInput?> ToInputAsync(int id, CancellationToken cancellationToken)
    {
        var user = (await _adminDataService.GetAdminUsersAsync(cancellationToken)).FirstOrDefault(item => item.Id == id);
        return user is null ? null : new AdminUserInput
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserName = user.UserName,
            Phone = user.Phone,
            DOB = user.DOB,
            City = user.City,
            State = user.State,
            Country = user.Country,
            IsSystemUser = user.IsSystemUser,
            IsActive = user.IsActive,
            RoleId = user.RoleId
        };
    }

    private async Task<ItemMenu> GetMenuPermission(int userId, int roleId, string actionName, CancellationToken cancellationToken)
    {
        return (await _adminDataService.GetMenusForUserAsync(userId, roleId, cancellationToken))
            .FirstOrDefault(menu =>
                string.Equals(menu.ControllerName, MenuController, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(menu.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
            ?? new ItemMenu();
    }

    private async Task PrepareAdminLayoutAsync(int userId, int roleId, CancellationToken cancellationToken)
    {
        ViewData["AdminName"] = HttpContext.Session.GetString("KitchenAdminName") ?? "Admin";
        ViewData["RoleName"] = HttpContext.Session.GetString("KitchenRoleName") ?? string.Empty;
        ViewData["Menus"] = await _adminDataService.GetMenusForUserAsync(userId, roleId, cancellationToken);
    }

    private Task<bool> HasPermission(int userId, int roleId, string actionName, MenuPermission permission, CancellationToken cancellationToken)
    {
        return _adminDataService.HasMenuPermissionAsync(userId, roleId, MenuController, actionName, permission, cancellationToken);
    }

    private bool TryGetIdentity(out int userId, out int roleId)
    {
        var userValue = HttpContext.Session.GetInt32("KitchenAdminId");
        var roleValue = HttpContext.Session.GetInt32("KitchenRoleId");
        userId = userValue.GetValueOrDefault();
        roleId = roleValue.GetValueOrDefault();
        return userValue is not null && roleValue is not null;
    }
}
