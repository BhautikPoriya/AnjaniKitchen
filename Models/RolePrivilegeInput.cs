namespace AnjaniKitchen.Models;

public sealed class RolePrivilegeInput
{
    public int MenuId { get; set; }
    public bool View { get; set; }
    public bool Add { get; set; }
    public bool Edit { get; set; }
    public bool Update { get; set; }
    public bool Delete { get; set; }
}
