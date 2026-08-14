using NumuneKabul.Application.Models;
using NumuneKabul.Domain.Entities;

namespace NumuneKabul.Application.Interfaces;

public interface IFieldExtractionService
{
    List<ExtractedFieldResult> ExtractFields(
        string rawText,
        int pageNo,
        IReadOnlyCollection<TemplateField> templateFields);
}