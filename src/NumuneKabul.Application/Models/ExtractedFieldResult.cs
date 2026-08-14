using NumuneKabul.Domain.Enums;

namespace NumuneKabul.Application.Models;

public class ExtractedFieldResult
{
    public string FieldName { get; set; } = string.Empty;

    public string RawValue { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public int PageNo { get; set; }

    public FieldStatus Status { get; set; } = FieldStatus.NotFound;
}
