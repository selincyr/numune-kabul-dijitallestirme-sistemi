using PDFtoImage;
using NumuneKabul.Application.Interfaces;

namespace NumuneKabul.Infrastructure.Services.Pdf;

public class PdfRenderer : IPdfRenderer
{
    public Task<IReadOnlyList<string>> RenderPdfAsync(
        int pdfId,
        string pdfFilePath,
        string outputRootDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException("PDF dosyası bulunamadı.", pdfFilePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var documentOutputDirectory = Path.Combine(
            outputRootDirectory,
            $"pdf-{pdfId}");

        Directory.CreateDirectory(documentOutputDirectory);

        using var pdfStream = File.OpenRead(pdfFilePath);

        var renderedFiles = new List<string>();
        var pageNo = 1;

        foreach (var image in Conversion.ToImages(pdfStream))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputPath = Path.Combine(
                documentOutputDirectory,
                $"page-{pageNo}.png");

            using var outputStream = File.Create(outputPath);

            image.Encode(
                outputStream,
                SkiaSharp.SKEncodedImageFormat.Png,
                quality: 100);

            renderedFiles.Add(outputPath);
            pageNo++;
        }

        return Task.FromResult<IReadOnlyList<string>>(renderedFiles);
    }
}
