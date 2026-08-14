using NumuneKabul.Domain.Common;
using NumuneKabul.Domain.Enums;

namespace NumuneKabul.Domain.Entities;

public class IntegrationJob : BaseEntity
{
    public int PdfId { get; set; }

    public PdfDocument? PdfDocument { get; set; }

    public IntegrationStatus Status { get; set; } = IntegrationStatus.Pending;

    public int RetryCount { get; set; }

    public string? LastErrorMessage { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? LastAttemptDate { get; set; }
}