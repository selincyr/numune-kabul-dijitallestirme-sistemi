namespace NumuneKabul.Application.Interfaces;

public interface IPdfRenderer
{
    Task<IReadOnlyList<string>> RenderPdfAsync(
        int pdfId,
        string pdfFilePath,
        string outputRootDirectory,
        CancellationToken cancellationToken = default);
}
