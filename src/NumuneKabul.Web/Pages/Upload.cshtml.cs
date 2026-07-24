using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages;

public class UploadModel : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IPdfRenderer _pdfRenderer;

    public UploadModel(
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

    [BindProperty]
    public IFormFile? UploadedFile { get; set; }

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (UploadedFile is null || UploadedFile.Length == 0)
        {
            ErrorMessage = "Lütfen bir PDF dosyası seçin.";
            return Page();
        }

        var extension = Path.GetExtension(UploadedFile.FileName).ToLowerInvariant();

        if (extension != ".pdf")
        {
            ErrorMessage = "Sadece PDF dosyası yükleyebilirsiniz.";
            return Page();
        }

        var maxFileSizeMb = _configuration.GetValue<int>("FileStorage:MaxFileSizeMb");

        if (UploadedFile.Length > maxFileSizeMb * 1024 * 1024)
        {
            ErrorMessage = $"Dosya boyutu en fazla {maxFileSizeMb} MB olabilir.";
            return Page();
        }

        var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
            ?? "Storage/Uploads";

        var uploadDirectory = Path.Combine(
            _environment.ContentRootPath,
            uploadDirectorySetting);

        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var savedFilePath = Path.Combine(uploadDirectory, storedFileName);

        await using (var fileStream = new FileStream(savedFilePath, FileMode.Create))
        {
            await UploadedFile.CopyToAsync(fileStream);
        }

        var institution = await _dbContext.Institutions.FirstOrDefaultAsync();

        if (institution is null)
        {
            institution = new Institution
            {
                Name = "Varsayılan Kurum"
            };

            _dbContext.Institutions.Add(institution);
            await _dbContext.SaveChangesAsync();
        }

        var pdfDocument = new PdfDocument
        {
            InstitutionId = institution.Id,
            FileName = Path.GetFileName(UploadedFile.FileName),
            StoredFileName = storedFileName
        };

        _dbContext.PdfDocuments.Add(pdfDocument);
        await _dbContext.SaveChangesAsync();

        try
        {
            var renderedPagesRoot = Path.Combine(
                _environment.ContentRootPath,
                "Storage",
                "RenderedPages");

            var renderedFiles = await _pdfRenderer.RenderPdfAsync(
                pdfDocument.Id,
                savedFilePath,
                renderedPagesRoot);

            TempData["SuccessMessage"] =
                $"PDF başarıyla yüklendi. Belge numarası: {pdfDocument.Id}. " +
                $"{renderedFiles.Count} sayfa otomatik olarak PNG formatına dönüştürüldü.";
        }
        catch (Exception ex)
        {
            TempData["SuccessMessage"] =
                $"PDF başarıyla yüklendi. Belge numarası: {pdfDocument.Id}.";

            TempData["ErrorMessage"] =
                $"PDF yüklendi ancak PNG dönüşümü sırasında hata oluştu: {ex.Message}";
        }

        return RedirectToPage("/Documents/Details", new { id = pdfDocument.Id });
    }
}