using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class OcrResult : BaseEntity
{
    public int PdfId { get; set; }

    public PdfDocument? PdfDocument { get; set; }

    public int PageNo { get; set; }

    public string RawText { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? ErrorMessage { get; set; }
}
