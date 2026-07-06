using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class XmlArchive : BaseEntity
{
    public int PdfId { get; set; }

    public PdfDocument? PdfDocument { get; set; }

    public string XmlContent { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
