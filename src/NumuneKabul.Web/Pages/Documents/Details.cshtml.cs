using NumuneKabul.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Documents;

public class DetailsModel : PageModel
{
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IIntegrationService _integrationService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IOcrService _ocrService;
    private readonly IFieldExtractionService _fieldExtractionService;
    private readonly IXmlGenerationService _xmlGenerationService;

    public DetailsModel(
        AppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IPdfRenderer pdfRenderer,
        IOcrService ocrService,
        IFieldExtractionService fieldExtractionService,
        IXmlGenerationService xmlGenerationService,
        IIntegrationService integrationService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
        _pdfRenderer = pdfRenderer;
        _ocrService = ocrService;
        _fieldExtractionService = fieldExtractionService;
        _xmlGenerationService = xmlGenerationService;
        _integrationService = integrationService;
    }

    public PdfDocument? Document { get; private set; }

    public List<OcrResult> OcrResults { get; private set; } = new();

    public List<ExtractedField> ExtractedFields { get; private set; } = new();

    public List<XmlArchive> XmlArchives { get; private set; } = new();

    public List<IntegrationJob> IntegrationJobs { get; private set; } = new();

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

        XmlArchives = await _dbContext.XmlArchives
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        IntegrationJobs = await _dbContext.IntegrationJobs
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
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

    public async Task<IActionResult> OnGetRenderedPageAsync(int id, int pageNo)
    {
        var document = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var renderedPagePath = Path.Combine(
            _environment.ContentRootPath,
            "Storage",
            "RenderedPages",
            $"pdf-{document.Id}",
            $"page-{pageNo}.png");

        if (!System.IO.File.Exists(renderedPagePath))
        {
            return NotFound("Sayfa görüntüsü bulunamadı.");
        }

        var stream = new FileStream(
            renderedPagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        return File(stream, "image/png");
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

        AddAuditLog(
            "PdfRender",
            $"{document.Id} numaralı belge için PNG sayfaları yeniden oluşturuldu. Oluşturulan sayfa sayısı: {renderedFiles.Count}.");

        await _dbContext.SaveChangesAsync();

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
            TempData["ErrorMessage"] =
                "OCR işleminden önce PDF sayfalarını PNG olarak hazırlamanız gerekir.";

            AddAuditLog(
                "OcrError",
                $"{document.Id} numaralı belge için OCR başlatılamadı. PNG sayfa klasörü bulunamadı.");

            await _dbContext.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        var imageFiles = Directory
            .GetFiles(renderedPagesDirectory, "page-*.png")
            .OrderBy(GetPageNumber)
            .ToList();

        if (!imageFiles.Any())
        {
            TempData["ErrorMessage"] = "OCR yapılacak PNG sayfası bulunamadı.";

            AddAuditLog(
                "OcrError",
                $"{document.Id} numaralı belge için OCR başlatılamadı. OCR yapılacak PNG sayfası bulunamadı.");

            await _dbContext.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        var template = await GetTemplateForDocumentAsync(document);

        var oldResults = await _dbContext.OcrResults
            .Where(x => x.PdfId == id)
            .ToListAsync();

        _dbContext.OcrResults.RemoveRange(oldResults);

        var oldExtractedFields = await _dbContext.ExtractedFields
            .Where(x => x.PdfId == id)
            .ToListAsync();

        _dbContext.ExtractedFields.RemoveRange(oldExtractedFields);

        var errorPageCount = 0;

        foreach (var imageFile in imageFiles)
        {
            var pageNo = GetPageNumber(imageFile);

            try
            {
                var rawText = await _ocrService.ExtractTextAsync(imageFile);

                var ocrWords = await _ocrService.ExtractWordsAsync(imageFile, pageNo);

                _dbContext.OcrResults.Add(new OcrResult
                {
                    PdfId = id,
                    PageNo = pageNo,
                    RawText = rawText,
                    CreatedDate = DateTime.UtcNow
                });

                var extractedFields = _fieldExtractionService.ExtractFields(
                    rawText,
                    pageNo,
                    template.TemplateFields.ToList());

                foreach (var field in extractedFields)
                {
                    var coordinate = FindFieldCoordinates(field.RawValue, ocrWords);

                    _dbContext.ExtractedFields.Add(new ExtractedField
                    {
                        PdfId = id,
                        FieldName = field.FieldName,
                        RawValue = field.RawValue,
                        CorrectedValue = field.Status == FieldStatus.NotFound
                            ? string.Empty
                            : field.RawValue,
                        Confidence = field.Confidence,
                        PageNo = field.PageNo,
                        X = coordinate?.X,
                        Y = coordinate?.Y,
                        Width = coordinate?.Width,
                        Height = coordinate?.Height,
                        Status = field.Status
                    });
                }
            }
            catch (Exception ex)
            {
                errorPageCount++;

                _dbContext.OcrResults.Add(new OcrResult
                {
                    PdfId = id,
                    PageNo = pageNo,
                    RawText = string.Empty,
                    ErrorMessage = ex.Message,
                    CreatedDate = DateTime.UtcNow
                });

                AddAuditLog(
                    "OcrPageError",
                    $"{document.Id} numaralı belgenin {pageNo}. sayfasında OCR hatası oluştu: {ex.Message}");
            }
        }

        AddAuditLog(
            "OCR",
            $"{document.Id} numaralı belge için OCR işlemi çalıştırıldı. Toplam sayfa: {imageFiles.Count}, hatalı sayfa: {errorPageCount}.");

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

        var changedFieldCount = 0;

        foreach (var field in fields)
        {
            if (correctedValues.TryGetValue(field.Id, out var correctedValue))
            {
                var newValue = correctedValue?.Trim() ?? string.Empty;
                var oldValue = field.CorrectedValue?.Trim() ?? string.Empty;

                if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
                {
                    changedFieldCount++;
                }

                field.CorrectedValue = newValue;

                if (!string.IsNullOrWhiteSpace(newValue))
                {
                    field.Status = FieldStatus.Verified;
                }
                else if (field.Status == FieldStatus.Verified)
                {
                    field.Status = FieldStatus.NeedsReview;
                }
            }
        }

        AddAuditLog(
            "ManualCorrection",
            $"{id} numaralı belge için manuel alan düzeltmesi kaydedildi. Güncellenen alan sayısı: {changedFieldCount}.");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Alan düzeltmeleri başarıyla kaydedildi.";

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateXmlAsync(int id)
    {
        var document = await _dbContext.PdfDocuments
            .Include(x => x.Institution)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var fields = await _dbContext.ExtractedFields
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ThenBy(x => x.FieldName)
            .ToListAsync();

        if (!fields.Any())
        {
            TempData["ErrorMessage"] =
                "XML oluşturmak için önce OCR ve alan çıkarma işlemi yapılmalıdır.";

            AddAuditLog(
                "XmlCreateError",
                $"{id} numaralı belge için XML oluşturulamadı. ExtractedFields kaydı bulunamadı.");

            await _dbContext.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        var ocrResults = await _dbContext.OcrResults
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ToListAsync();

        var xmlContent = _xmlGenerationService.GenerateXml(
            document,
            fields,
            ocrResults);

        var xmlArchive = new XmlArchive
        {
            PdfId = id,
            XmlContent = xmlContent,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.XmlArchives.Add(xmlArchive);

        AddAuditLog(
            "XmlCreate",
            $"{id} numaralı belge için XML oluşturuldu ve XmlArchives tablosuna kaydedildi.");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "XML başarıyla oluşturuldu ve arşivlendi.";

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSendIntegrationAsync(int id)
    {
        var document = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var latestXml = await _dbContext.XmlArchives
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync();

        if (latestXml is null)
        {
            var failedJob = new IntegrationJob
            {
                PdfId = id,
                Status = IntegrationStatus.Failed,
                RetryCount = 0,
                CreatedDate = DateTime.UtcNow,
                LastAttemptDate = DateTime.UtcNow,
                LastErrorMessage = "XML kaydı bulunamadı."
            };

            _dbContext.IntegrationJobs.Add(failedJob);

            AddAuditLog(
                "IntegrationFailed",
                $"{id} numaralı belge mock entegrasyona gönderilemedi. XML kaydı bulunamadı.");

            await _dbContext.SaveChangesAsync();

            TempData["ErrorMessage"] =
                "Mock entegrasyona gönderim için önce XML oluşturulmalıdır.";

            return RedirectToPage(new { id });
        }

        var integrationJob = new IntegrationJob
        {
            PdfId = id,
            Status = IntegrationStatus.Processing,
            RetryCount = 0,
            CreatedDate = DateTime.UtcNow,
            LastAttemptDate = DateTime.UtcNow
        };

        _dbContext.IntegrationJobs.Add(integrationJob);

        AddAuditLog(
            "IntegrationStart",
            $"{id} numaralı belge için mock entegrasyon gönderimi başlatıldı.");

        await _dbContext.SaveChangesAsync();

        try
        {
            var result = await _integrationService.SendXmlAsync(
                id,
                latestXml.XmlContent);

            integrationJob.Status = result.IsSuccess
                ? IntegrationStatus.Success
                : IntegrationStatus.Failed;

            integrationJob.LastAttemptDate = DateTime.UtcNow;
            integrationJob.LastErrorMessage = result.IsSuccess
                ? null
                : result.Message;

            if (result.IsSuccess)
            {
                AddAuditLog(
                    "IntegrationSuccess",
                    $"{id} numaralı belge mock REST servisine başarıyla gönderildi.");

                TempData["SuccessMessage"] =
                    "XML mock REST servisine başarıyla gönderildi.";
            }
            else
            {
                AddAuditLog(
                    "IntegrationFailed",
                    $"{id} numaralı belge mock REST servisine gönderilemedi. Hata: {result.Message}");

                TempData["ErrorMessage"] = result.Message;
            }
        }
        catch (Exception ex)
        {
            integrationJob.Status = IntegrationStatus.Failed;
            integrationJob.LastAttemptDate = DateTime.UtcNow;
            integrationJob.LastErrorMessage = ex.Message;

            AddAuditLog(
                "IntegrationError",
                $"{id} numaralı belge mock entegrasyona gönderilirken hata oluştu: {ex.Message}");

            TempData["ErrorMessage"] =
                $"Mock entegrasyon gönderimi sırasında hata oluştu: {ex.Message}";
        }

        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRetryIntegrationAsync(int id, int jobId)
    {
        var document = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (document is null)
        {
            return NotFound();
        }

        var integrationJob = await _dbContext.IntegrationJobs
            .FirstOrDefaultAsync(x => x.Id == jobId && x.PdfId == id);

        if (integrationJob is null)
        {
            return NotFound();
        }

        var latestXml = await _dbContext.XmlArchives
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync();

        integrationJob.RetryCount++;
        integrationJob.LastAttemptDate = DateTime.UtcNow;
        integrationJob.Status = IntegrationStatus.Processing;
        integrationJob.LastErrorMessage = null;

        AddAuditLog(
            "IntegrationRetry",
            $"{id} numaralı belge için mock entegrasyon yeniden gönderimi başlatıldı. Deneme sayısı: {integrationJob.RetryCount}.");

        await _dbContext.SaveChangesAsync();

        if (latestXml is null)
        {
            integrationJob.Status = IntegrationStatus.Failed;
            integrationJob.LastAttemptDate = DateTime.UtcNow;
            integrationJob.LastErrorMessage = "XML kaydı bulunamadı.";

            AddAuditLog(
                "IntegrationRetryFailed",
                $"{id} numaralı belge yeniden gönderilemedi. XML kaydı bulunamadı.");

            await _dbContext.SaveChangesAsync();

            TempData["ErrorMessage"] =
                "Yeniden gönderim için önce XML oluşturulmalıdır.";

            return RedirectToPage(new { id });
        }

        try
        {
            var result = await _integrationService.SendXmlAsync(
                id,
                latestXml.XmlContent);

            integrationJob.Status = result.IsSuccess
                ? IntegrationStatus.Success
                : IntegrationStatus.Failed;

            integrationJob.LastAttemptDate = DateTime.UtcNow;
            integrationJob.LastErrorMessage = result.IsSuccess
                ? null
                : result.Message;

            if (result.IsSuccess)
            {
                AddAuditLog(
                    "IntegrationRetrySuccess",
                    $"{id} numaralı belge mock REST servisine yeniden başarıyla gönderildi.");

                TempData["SuccessMessage"] =
                    "XML mock REST servisine yeniden başarıyla gönderildi.";
            }
            else
            {
                AddAuditLog(
                    "IntegrationRetryFailed",
                    $"{id} numaralı belge yeniden gönderilemedi. Hata: {result.Message}");

                TempData["ErrorMessage"] = result.Message;
            }
        }
        catch (Exception ex)
        {
            integrationJob.Status = IntegrationStatus.Failed;
            integrationJob.LastAttemptDate = DateTime.UtcNow;
            integrationJob.LastErrorMessage = ex.Message;

            AddAuditLog(
                "IntegrationRetryError",
                $"{id} numaralı belge yeniden gönderilirken hata oluştu: {ex.Message}");

            TempData["ErrorMessage"] =
                $"Yeniden gönderim sırasında hata oluştu: {ex.Message}";
        }

        await _dbContext.SaveChangesAsync();

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

        AddAuditLog(
            "PdfDelete",
            $"{document.Id} numaralı belge silindi. Dosya adı: {document.FileName}");

        _dbContext.PdfDocuments.Remove(document);
        await _dbContext.SaveChangesAsync();

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        TempData["SuccessMessage"] = "Belge başarıyla silindi.";

        return RedirectToPage("./Index");
    }

    private async Task<FormTemplate> GetTemplateForDocumentAsync(PdfDocument document)
    {
        if (document.TemplateId.HasValue)
        {
            var selectedTemplate = await _dbContext.FormTemplates
                .Include(x => x.TemplateFields)
                .FirstOrDefaultAsync(x =>
                    x.Id == document.TemplateId.Value &&
                    x.InstitutionId == document.InstitutionId);

            if (selectedTemplate is not null)
            {
                return selectedTemplate;
            }
        }

        var defaultTemplate = await GetOrCreateDefaultTemplateAsync(document.InstitutionId);

        document.TemplateId = defaultTemplate.Id;

        await _dbContext.SaveChangesAsync();

        return defaultTemplate;
    }

    private async Task<FormTemplate> GetOrCreateDefaultTemplateAsync(int institutionId)
    {
        var template = await _dbContext.FormTemplates
            .Include(x => x.TemplateFields)
            .FirstOrDefaultAsync(x => x.InstitutionId == institutionId);

        if (template is null)
        {
            template = new FormTemplate
            {
                InstitutionId = institutionId,
                Name = "Varsayılan Numune Kabul Şablonu",
                Description = "OCR sonrası beklenen alanların şablon bazlı sınıflandırılması için varsayılan şablon."
            };

            _dbContext.FormTemplates.Add(template);
            await _dbContext.SaveChangesAsync();
        }

        if (!template.TemplateFields.Any())
        {
            var templateFields = new List<TemplateField>
            {
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "T.C. Kimlik No",
                    Keyword = "T.C.",
                    Regex = @"\b([1-9][0-9]{10})\b",
                    Required = true,
                    DataType = "TCKN",
                    OrderNo = 1
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Hasta Adı Soyadı",
                    Keyword = "Hasta",
                    Regex = @"(?:Hasta\s+Adı\s+Soyadı|Hasta\s+Adı|Adı\s+Soyadı|Ad\s+Soyad)\s*[:\-]?\s*(.+)",
                    Required = true,
                    DataType = "Text",
                    OrderNo = 2
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Doğum Tarihi",
                    Keyword = "Doğum",
                    Regex = @"(?:Doğum\s+Tarihi|Dogum\s+Tarihi|Doğum\s+Tar\.?)\s*[:\-]?\s*([0-9]{1,2}[./-][0-9]{1,2}[./-][0-9]{2,4})",
                    Required = false,
                    DataType = "Date",
                    OrderNo = 3
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Cinsiyet",
                    Keyword = "Cinsiyet",
                    Regex = @"(?:Cinsiyet|Cinsiyeti)\s*[:\-]?\s*(Kadın|Erkek|Kadin|E|K)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 4
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Kurum",
                    Keyword = "Kurum",
                    Regex = @"(?:Kurum|Kurumu|Hastane)\s*[:\-]?\s*(.+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 5
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Doktor",
                    Keyword = "Doktor",
                    Regex = @"(?:Doktor|Hekim|İstemi\s+Yapan\s+Doktor|Istemi\s+Yapan\s+Doktor)\s*[:\-]?\s*(.+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 6
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Protokol No",
                    Keyword = "Protokol",
                    Regex = @"(?:Protokol\s+No|İşlem\s+No|Islem\s+No|Dosya\s+No)\s*[:\-]?\s*([A-Za-z0-9\-/]+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 7
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Numune Barkodu",
                    Keyword = "Barkod",
                    Regex = @"(?:Barkod|Numune\s+Barkodu)\s*[:\-]?\s*([A-Za-z0-9\-/]+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 8
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Numune Türü",
                    Keyword = "Numune",
                    Regex = @"(?:Numune\s+Türü|Numune\s+Cinsi|Materyal)\s*[:\-]?\s*(.+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 9
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Test Adı",
                    Keyword = "Test",
                    Regex = @"(?:Test\s+Adı|Tetkik|İstenen\s+Tetkik|Istenen\s+Tetkik)\s*[:\-]?\s*(.+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 10
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Numune Kabul Tarihi",
                    Keyword = "Kabul",
                    Regex = @"(?:Numune\s+Kabul\s+Tarihi|Kabul\s+Tarihi|Alım\s+Tarihi|Alim\s+Tarihi)\s*[:\-]?\s*([0-9]{1,2}[./-][0-9]{1,2}[./-][0-9]{2,4})",
                    Required = false,
                    DataType = "Date",
                    OrderNo = 11
                },
                new()
                {
                    TemplateId = template.Id,
                    FieldName = "Açıklama",
                    Keyword = "Açıklama",
                    Regex = @"(?:Açıklama|Aciklama|Not)\s*[:\-]?\s*(.+)",
                    Required = false,
                    DataType = "Text",
                    OrderNo = 12
                }
            };

            _dbContext.TemplateFields.AddRange(templateFields);
            await _dbContext.SaveChangesAsync();

            template = await _dbContext.FormTemplates
                .Include(x => x.TemplateFields)
                .FirstAsync(x => x.Id == template.Id);
        }

        return template;
    }

    private static FieldCoordinate? FindFieldCoordinates(
        string? fieldValue,
        IReadOnlyList<OcrWordResult> words)
    {
        if (string.IsNullOrWhiteSpace(fieldValue) || words.Count == 0)
        {
            return null;
        }

        var normalizedFieldValue = NormalizeForCoordinateMatch(fieldValue);

        if (string.IsNullOrWhiteSpace(normalizedFieldValue))
        {
            return null;
        }

        var validWords = words
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        if (!validWords.Any())
        {
            return null;
        }

        var fieldTokens = normalizedFieldValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (fieldTokens.Length == 0)
        {
            return null;
        }

        var minWindowSize = Math.Max(1, fieldTokens.Length);
        var maxWindowSize = Math.Min(validWords.Count, fieldTokens.Length + 4);

        for (var windowSize = minWindowSize; windowSize <= maxWindowSize; windowSize++)
        {
            for (var startIndex = 0; startIndex <= validWords.Count - windowSize; startIndex++)
            {
                var selectedWords = validWords
                    .Skip(startIndex)
                    .Take(windowSize)
                    .ToList();

                var joinedText = string.Join(" ", selectedWords.Select(x => x.Text));
                var normalizedJoinedText = NormalizeForCoordinateMatch(joinedText);

                if (string.IsNullOrWhiteSpace(normalizedJoinedText))
                {
                    continue;
                }

                var isMatch =
                    normalizedJoinedText.Contains(normalizedFieldValue) ||
                    normalizedFieldValue.Contains(normalizedJoinedText);

                if (!isMatch)
                {
                    continue;
                }

                return CreateCoordinate(selectedWords);
            }
        }

        var firstToken = fieldTokens
            .FirstOrDefault(x => x.Length >= 3);

        if (firstToken is null)
        {
            return null;
        }

        var fallbackWord = validWords
            .FirstOrDefault(x =>
                NormalizeForCoordinateMatch(x.Text).Contains(firstToken));

        return fallbackWord is null
            ? null
            : CreateCoordinate(new List<OcrWordResult> { fallbackWord });
    }

    private static FieldCoordinate CreateCoordinate(IReadOnlyList<OcrWordResult> words)
    {
        var minX = words.Min(x => x.X);
        var minY = words.Min(x => x.Y);
        var maxX = words.Max(x => x.X + x.Width);
        var maxY = words.Max(x => x.Y + x.Height);

        return new FieldCoordinate
        {
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY
        };
    }

    private static string NormalizeForCoordinateMatch(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace("İ", "i")
            .Replace("Ğ", "g")
            .Replace("Ü", "u")
            .Replace("Ş", "s")
            .Replace("Ö", "o")
            .Replace("Ç", "c")
            .Replace(":", " ")
            .Replace(";", " ")
            .Replace(",", " ")
            .Replace(".", " ")
            .Replace("-", " ")
            .Replace("/", " ")
            .Replace("(", " ")
            .Replace(")", " ")
            .Trim();
    }

    private class FieldCoordinate
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    private void AddAuditLog(string action, string description)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            Description = description,
            Date = DateTime.UtcNow
        });
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