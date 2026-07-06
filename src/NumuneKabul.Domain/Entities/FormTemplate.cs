using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class FormTemplate : BaseEntity
{
    public int InstitutionId { get; set; }

    public Institution? Institution { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ICollection<TemplateField> TemplateFields { get; set; } = new List<TemplateField>();
}
