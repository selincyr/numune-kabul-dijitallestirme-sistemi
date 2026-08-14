using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class AuditLog : BaseEntity
{
    public int? UserId { get; set; }

    public User? User { get; set; }

    public string Action { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public string Description { get; set; } = string.Empty;
}