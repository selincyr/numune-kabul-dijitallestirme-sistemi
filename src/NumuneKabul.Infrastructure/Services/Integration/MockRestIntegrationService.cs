using NumuneKabul.Application.Interfaces;
using NumuneKabul.Application.Models;

namespace NumuneKabul.Infrastructure.Services.Integration;

public class MockRestIntegrationService : IIntegrationService
{
    public async Task<IntegrationResult> SendXmlAsync(
        int pdfId,
        string xmlContent,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);

        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return new IntegrationResult
            {
                IsSuccess = false,
                Message = "XML içeriği boş olduğu için mock entegrasyon gönderimi başarısız oldu."
            };
        }

        if (!xmlContent.Contains("<NumuneKabulBelgesi"))
        {
            return new IntegrationResult
            {
                IsSuccess = false,
                Message = "XML formatı beklenen NumuneKabulBelgesi yapısına uygun değil."
            };
        }

        return new IntegrationResult
        {
            IsSuccess = true,
            Message = $"Belge {pdfId} için XML mock REST servisine başarıyla gönderildi."
        };
    }
}