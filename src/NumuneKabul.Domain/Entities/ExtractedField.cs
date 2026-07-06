using NumuneKabul.Domain.Common;
using NumuneKabul.Domain.Enums;

namespace NumuneKabul.Domain.Entities;

public class ExtractedField : BaseEntity
{
    public int PdfId { get; set; }

    public PdfDocument? PdfDocument { get; set; }

    public string FieldName { get; set; } = string.Empty;

    public string? RawValue { get; set; }

    public string? CorrectedValue { get; set; }

    // 0-100 arası güven skoru
    public decimal Confidence { get; set; }

    public int PageNo { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public FieldStatus Status { get; set; } = FieldStatus.NotFound;
}
