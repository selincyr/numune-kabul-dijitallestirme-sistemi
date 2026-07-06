using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class TemplateField : BaseEntity
{
    public int TemplateId { get; set; }

    public FormTemplate? Template { get; set; }

    public string FieldName { get; set; } = string.Empty;

    public string Keyword { get; set; } = string.Empty;

    public string Regex { get; set; } = string.Empty;

    public bool Required { get; set; }

    public string DataType { get; set; } = string.Empty;

    public int OrderNo { get; set; }
}
