using NumuneKabul.Application.Models;

namespace NumuneKabul.Application.Interfaces;

public interface IIntegrationService
{
    Task<IntegrationResult> SendXmlAsync(
        int pdfId,
        string xmlContent,
        CancellationToken cancellationToken = default);
}