using NumuneKabul.Application.Models;

namespace NumuneKabul.Application.Interfaces;

public interface IFieldExtractionService
{
    List<ExtractedFieldResult> ExtractFields(string rawText, int pageNo);
}
