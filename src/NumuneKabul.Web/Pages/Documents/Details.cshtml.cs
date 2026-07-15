using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Documents;

public class DetailsModel : PageModel
{
    private readonly IPdfRenderer _pdfRenderer;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IOcrService _ocrService;
    private readonly IFieldExtractionService _fieldExtractionService;

    public DetailsModel(
        AppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IPdfRenderer pdfRenderer,
        IOcrService ocrService,
        IFieldExtractionService fieldExtractionService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
        _pdfRenderer = pdfRenderer;
        _ocrService = ocrService;
        _fieldExtractionService = fieldExtractionService;
    }

    public PdfDocument? Document { get; private set; }

    public List<OcrResult> OcrResults { get; private set; } = new();

    public List<ExtractedField> ExtractedFields { get; private set; } = new();

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

        OcrResults = await _dbContext.OcrResults
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ToListAsync();

        ExtractedFields = await _dbContext.ExtractedFields
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ThenBy(x => x.FieldName)
            .ToListAsync();

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

    public async Task<IActionResult> OnPostOcrAsync(int id)
    {
        var document = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var renderedPagesDirectory = Path.Combine(
            _environment.ContentRootPath,
            "Storage",
            "RenderedPages",
            $"pdf-{document.Id}");

        if (!Directory.Exists(renderedPagesDirectory))
        {
            TempData["ErrorMessage"] = "OCR işleminden önce PDF sayfalarını PNG olarak hazırlamanız gerekir.";
            return RedirectToPage(new { id });
        }

        var imageFiles = Directory
            .GetFiles(renderedPagesDirectory, "page-*.png")
            .OrderBy(GetPageNumber)
            .ToList();

        if (!imageFiles.Any())
        {
            TempData["ErrorMessage"] = "OCR yapılacak PNG sayfası bulunamadı.";
            return RedirectToPage(new { id });
        }

        var oldResults = await _dbContext.OcrResults
            .Where(x => x.PdfId == id)
            .ToListAsync();

        _dbContext.OcrResults.RemoveRange(oldResults);

        var oldExtractedFields = await _dbContext.ExtractedFields
            .Where(x => x.PdfId == id)
            .ToListAsync();

        _dbContext.ExtractedFields.RemoveRange(oldExtractedFields);

        foreach (var imageFile in imageFiles)
        {
            var pageNo = GetPageNumber(imageFile);

            try
            {
                var rawText = await _ocrService.ExtractTextAsync(imageFile);

                _dbContext.OcrResults.Add(new OcrResult
                {
                    PdfId = id,
                    PageNo = pageNo,
                    RawText = rawText,
                    CreatedDate = DateTime.UtcNow
                });

                var extractedFields = _fieldExtractionService.ExtractFields(rawText, pageNo);

                foreach (var field in extractedFields)
                {
                    _dbContext.ExtractedFields.Add(new ExtractedField
                    {
                        PdfId = id,
                        FieldName = field.FieldName,
                        RawValue = field.RawValue,
                        CorrectedValue = field.RawValue,
                        Confidence = field.Confidence,
                        PageNo = field.PageNo
                    });
                }
            }
            catch (Exception ex)
            {
                _dbContext.OcrResults.Add(new OcrResult
                {
                    PdfId = id,
                    PageNo = pageNo,
                    RawText = string.Empty,
                    ErrorMessage = ex.Message,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"{imageFiles.Count} sayfa için OCR işlemi tamamlandı.";

        return RedirectToPage(new { id });
    }
    
    public async Task<IActionResult> OnPostSaveFieldsAsync(
    int id,
    Dictionary<int, string> correctedValues)
{
    var documentExists = await _dbContext.PdfDocuments
        .AnyAsync(x => x.Id == id);

    if (!documentExists)
    {
        return NotFound();
    }

    var fields = await _dbContext.ExtractedFields
        .Where(x => x.PdfId == id)
        .ToListAsync();

    foreach (var field in fields)
    {
        if (correctedValues.TryGetValue(field.Id, out var correctedValue))
        {
            field.CorrectedValue = correctedValue?.Trim();
        }
    }

    await _dbContext.SaveChangesAsync();

    TempData["SuccessMessage"] = "Alan düzeltmeleri başarıyla kaydedildi.";

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

    private static int GetPageNumber(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var numberPart = fileName.Replace("page-", "");

        return int.TryParse(numberPart, out var pageNo)
            ? pageNo
            : int.MaxValue;
    }
}