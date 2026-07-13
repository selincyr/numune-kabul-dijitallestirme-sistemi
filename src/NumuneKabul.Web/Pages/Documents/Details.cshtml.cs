using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;
using NumuneKabul.Application.Interfaces;

namespace NumuneKabul.Web.Pages.Documents;

public class DetailsModel : PageModel
{
    private readonly IPdfRenderer _pdfRenderer;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DetailsModel(
        AppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IPdfRenderer pdfRenderer)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
        _pdfRenderer = pdfRenderer;
    }

    public PdfDocument? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Document = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Include(x => x.Institution)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Document is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnGetPreviewAsync(int id)
    {
        var document = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
            ?? "Storage/Uploads";

        var filePath = Path.Combine(
            _environment.ContentRootPath,
            uploadDirectorySetting,
            document.StoredFileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("PDF dosyası bulunamadı.");
        }

        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        return File(stream, "application/pdf");
    }
    public async Task<IActionResult> OnPostRenderAsync(int id)
{
    var document = await _dbContext.PdfDocuments
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (document is null)
    {
        return NotFound();
    }

    var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
        ?? "Storage/Uploads";

    var pdfFilePath = Path.Combine(
        _environment.ContentRootPath,
        uploadDirectorySetting,
        document.StoredFileName);

    var renderedPagesRoot = Path.Combine(
        _environment.ContentRootPath,
        "Storage",
        "RenderedPages");

    var renderedFiles = await _pdfRenderer.RenderPdfAsync(
        document.Id,
        pdfFilePath,
        renderedPagesRoot);

    TempData["SuccessMessage"] =
        $"{renderedFiles.Count} sayfa PNG olarak hazırlandı.";

    return RedirectToPage(new { id });
}
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var document = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
            ?? "Storage/Uploads";

        var filePath = Path.Combine(
            _environment.ContentRootPath,
            uploadDirectorySetting,
            document.StoredFileName);

        _dbContext.PdfDocuments.Remove(document);
        await _dbContext.SaveChangesAsync();

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        TempData["SuccessMessage"] = "Belge başarıyla silindi.";
        return RedirectToPage("./Index");
    }
}
