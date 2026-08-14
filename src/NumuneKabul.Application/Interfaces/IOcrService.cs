using NumuneKabul.Application.Models;

namespace NumuneKabul.Application.Interfaces;

public interface IOcrService
{
    Task<string> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<List<OcrWordResult>> ExtractWordsAsync(
        string imagePath,
        int pageNo,
        CancellationToken cancellationToken = default);
}