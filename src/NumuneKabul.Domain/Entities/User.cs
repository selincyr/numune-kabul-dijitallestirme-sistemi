using NumuneKabul.Domain.Common;
using NumuneKabul.Domain.Enums;

namespace NumuneKabul.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.SampleAcceptanceStaff;
}
