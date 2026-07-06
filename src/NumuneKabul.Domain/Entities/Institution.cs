using NumuneKabul.Domain.Common;

namespace NumuneKabul.Domain.Entities;

public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<FormTemplate> FormTemplates { get; set; } = new List<FormTemplate>();
}
