using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class AppUser : BaseEntity
{
    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.Personnel;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginDate { get; set; }
}

public static class UserRoles
{
    public const string Admin = "Admin";

    public const string Personnel = "Personnel";

    public const string IntegrationService = "IntegrationService";
}