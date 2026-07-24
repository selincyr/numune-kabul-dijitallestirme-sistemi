using NumuneKabul.Domain.Entities;

namespace NumuneKabul.Application.Interfaces;

public interface IXmlGenerationService
{
    string GenerateXml(PdfDocument document, List<ExtractedField> fields);
}
