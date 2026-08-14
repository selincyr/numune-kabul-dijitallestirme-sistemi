using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Application.Models;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Controllers;

[ApiController]
[Route("api/ocr")]
public class OcrController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IOcrService _ocrService;
    private readonly IFieldExtractionService _fieldExtractionService;

    public OcrController(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        IOcrService ocrService,
        IFieldExtractionService fieldExtractionService)
    {
        _dbContext = dbContext;
        _environment = environment;
        _ocrService = ocrService;
        _fieldExtractionService = fieldExtractionService;
    }

    [HttpPost("start/{id:int}")]
    public async Task<IActionResult> StartAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound(new
            {
                message = "Belge bulunamadı."
            });
        }

        var renderedPagesDirectory = Path.Combine(
            _environment.ContentRootPath,
            "Storage",
            "RenderedPages",
            $"pdf-{document.Id}");

        if (!Directory.Exists(renderedPagesDirectory))
        {
            AddAuditLog(
                "ApiOcrError",
                $"{document.Id} numaralı belge için API OCR başlatılamadı. PNG sayfa klasörü bulunamadı.");

            await _dbContext.SaveChangesAsync(cancellationToken);

            return BadRequest(new
            {
                message = "OCR işleminden önce PDF sayfalarının PNG olarak hazırlanması gerekir."
            });
        }

        var imageFiles = Directory
            .GetFiles(renderedPagesDirectory, "page-*.png")
            .OrderBy(GetPageNumber)
            .ToList();

        if (!imageFiles.Any())
        {
            AddAuditLog(
                "ApiOcrError",
                $"{document.Id} numaralı belge için API OCR başlatılamadı. PNG sayfa bulunamadı.");

            await _dbContext.SaveChangesAsync(cancellationToken);

            return BadRequest(new
            {
                message = "OCR yapılacak PNG sayfası bulunamadı."
            });
        }

        var template = await GetTemplateForDocumentAsync(
            document,
            cancellationToken);

        var oldOcrResults = await _dbContext.OcrResults
            .Where(x => x.PdfId == id)
            .ToListAsync(cancellationToken);

        _dbContext.OcrResults.RemoveRange(oldOcrResults);

        var oldExtractedFields = await _dbContext.ExtractedFields
            .Where(x => x.PdfId == id)
            .ToListAsync(cancellationToken);

        _dbContext.ExtractedFields.RemoveRange(oldExtractedFields);

        var errorPageCount = 0;

        foreach (var imageFile in imageFiles)
        {
            var pageNo = GetPageNumber(imageFile);

            try
            {
                var rawText = await _ocrService.ExtractTextAsync(
                    imageFile,
                    cancellationToken);

                var ocrWords = await _ocrService.ExtractWordsAsync(
                    imageFile,
                    pageNo,
                    cancellationToken);

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
                    "ApiOcrPageError",
                    $"{document.Id} numaralı belgenin {pageNo}. sayfasında API OCR hatası oluştu: {ex.Message}");
            }
        }

        AddAuditLog(
            "ApiOCR",
            $"{document.Id} numaralı belge için API üzerinden OCR çalıştırıldı. Toplam sayfa: {imageFiles.Count}, hatalı sayfa: {errorPageCount}.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "OCR işlemi tamamlandı.",
            documentId = id,
            totalPageCount = imageFiles.Count,
            errorPageCount
        });
    }

    [HttpGet("result/{id:int}")]
    public async Task<IActionResult> GetResultAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var documentExists = await _dbContext.PdfDocuments
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!documentExists)
        {
            return NotFound(new
            {
                message = "Belge bulunamadı."
            });
        }

        var ocrResults = await _dbContext.OcrResults
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .Select(x => new
            {
                x.Id,
                x.PdfId,
                x.PageNo,
                x.RawText,
                x.ErrorMessage,
                x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var extractedFields = await _dbContext.ExtractedFields
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ThenBy(x => x.FieldName)
            .Select(x => new
            {
                x.Id,
                x.PdfId,
                x.FieldName,
                x.RawValue,
                x.CorrectedValue,
                x.Confidence,
                x.PageNo,
                x.X,
                x.Y,
                x.Width,
                x.Height,
                Status = x.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            documentId = id,
            ocrResults,
            extractedFields
        });
    }

    private async Task<FormTemplate> GetTemplateForDocumentAsync(
        PdfDocument document,
        CancellationToken cancellationToken)
    {
        if (document.TemplateId.HasValue)
        {
            var selectedTemplate = await _dbContext.FormTemplates
                .Include(x => x.TemplateFields)
                .FirstOrDefaultAsync(x =>
                    x.Id == document.TemplateId.Value &&
                    x.InstitutionId == document.InstitutionId,
                    cancellationToken);

            if (selectedTemplate is not null)
            {
                return selectedTemplate;
            }
        }

        var defaultTemplate = await GetOrCreateDefaultTemplateAsync(
            document.InstitutionId,
            cancellationToken);

        document.TemplateId = defaultTemplate.Id;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return defaultTemplate;
    }

    private async Task<FormTemplate> GetOrCreateDefaultTemplateAsync(
        int institutionId,
        CancellationToken cancellationToken)
    {
        var template = await _dbContext.FormTemplates
            .Include(x => x.TemplateFields)
            .FirstOrDefaultAsync(x => x.InstitutionId == institutionId, cancellationToken);

        if (template is null)
        {
            template = new FormTemplate
            {
                InstitutionId = institutionId,
                Name = "Varsayılan Numune Kabul Şablonu",
                Description = "OCR sonrası beklenen alanların şablon bazlı sınıflandırılması için varsayılan şablon."
            };

            _dbContext.FormTemplates.Add(template);
            await _dbContext.SaveChangesAsync(cancellationToken);
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
            await _dbContext.SaveChangesAsync(cancellationToken);

            template = await _dbContext.FormTemplates
                .Include(x => x.TemplateFields)
                .FirstAsync(x => x.Id == template.Id, cancellationToken);
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

    private static int GetPageNumber(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var numberPart = fileName.Replace("page-", "");

        return int.TryParse(numberPart, out var pageNo)
            ? pageNo
            : int.MaxValue;
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

    private class FieldCoordinate
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}