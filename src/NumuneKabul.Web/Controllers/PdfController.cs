using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Controllers;

[ApiController]
[Route("api/pdf")]
public class PdfController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IPdfRenderer _pdfRenderer;

    public PdfController(
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

    [HttpPost("upload")]
    public async Task<IActionResult> UploadAsync(
        [FromForm] IFormFile file,
        [FromForm] int? institutionId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "PDF dosyası seçilmelidir."
            });
        }

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Sadece PDF dosyası yüklenebilir."
            });
        }

        var institution = await GetOrCreateInstitutionAsync(
            institutionId,
            cancellationToken);

        var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
            ?? "Storage/Uploads";

        var uploadDirectory = Path.Combine(
            _environment.ContentRootPath,
            uploadDirectorySetting);

        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{Guid.NewGuid():N}.pdf";

        var filePath = Path.Combine(
            uploadDirectory,
            storedFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var document = new PdfDocument
        {
            InstitutionId = institution.Id,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            UploadDate = DateTime.UtcNow,
            Status = PdfStatus.Uploaded
        };

        _dbContext.PdfDocuments.Add(document);

        AddAuditLog(
            "ApiPdfUpload",
            $"API üzerinden PDF yüklendi. Dosya adı: {file.FileName}");

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var renderedPagesRoot = Path.Combine(
                _environment.ContentRootPath,
                "Storage",
                "RenderedPages");

            var renderedFiles = await _pdfRenderer.RenderPdfAsync(
                document.Id,
                filePath,
                renderedPagesRoot,
                cancellationToken);

            AddAuditLog(
                "ApiPdfRender",
                $"API üzerinden yüklenen {document.Id} numaralı belge için {renderedFiles.Count} sayfa PNG olarak oluşturuldu.");

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AddAuditLog(
                "ApiPdfRenderError",
                $"API üzerinden yüklenen {document.Id} numaralı belge için PNG oluşturma hatası: {ex.Message}");

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Created(
            $"/api/pdf/{document.Id}",
            new
            {
                document.Id,
                document.FileName,
                document.StoredFileName,
                document.InstitutionId,
                InstitutionName = institution.Name,
                document.UploadDate,
                Status = document.Status.ToString()
            });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Include(x => x.Institution)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound(new
            {
                message = "Belge bulunamadı."
            });
        }

        var ocrCount = await _dbContext.OcrResults
            .AsNoTracking()
            .CountAsync(x => x.PdfId == id, cancellationToken);

        var extractedFieldCount = await _dbContext.ExtractedFields
            .AsNoTracking()
            .CountAsync(x => x.PdfId == id, cancellationToken);

        var xmlArchiveCount = await _dbContext.XmlArchives
            .AsNoTracking()
            .CountAsync(x => x.PdfId == id, cancellationToken);

        return Ok(new
        {
            document.Id,
            document.FileName,
            document.StoredFileName,
            document.UploadDate,
            Status = document.Status.ToString(),
            Institution = document.Institution is null
                ? null
                : new
                {
                    document.Institution.Id,
                    document.Institution.Name
                },
            OcrResultCount = ocrCount,
            ExtractedFieldCount = extractedFieldCount,
            XmlArchiveCount = xmlArchiveCount
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(
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

        var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
            ?? "Storage/Uploads";

        var filePath = Path.Combine(
            _environment.ContentRootPath,
            uploadDirectorySetting,
            document.StoredFileName);

        var renderedPagesDirectory = Path.Combine(
            _environment.ContentRootPath,
            "Storage",
            "RenderedPages",
            $"pdf-{document.Id}");

        _dbContext.PdfDocuments.Remove(document);

        AddAuditLog(
            "ApiPdfDelete",
            $"API üzerinden {document.Id} numaralı belge silindi. Dosya adı: {document.FileName}");

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        if (Directory.Exists(renderedPagesDirectory))
        {
            Directory.Delete(renderedPagesDirectory, recursive: true);
        }

        return Ok(new
        {
            message = "Belge başarıyla silindi.",
            id
        });
    }

    private async Task<Institution> GetOrCreateInstitutionAsync(
        int? institutionId,
        CancellationToken cancellationToken)
    {
        if (institutionId.HasValue)
        {
            var existingInstitution = await _dbContext.Institutions
                .FirstOrDefaultAsync(x => x.Id == institutionId.Value, cancellationToken);

            if (existingInstitution is not null)
            {
                return existingInstitution;
            }
        }

        var defaultInstitution = await _dbContext.Institutions
            .FirstOrDefaultAsync(x => x.Name == "Varsayılan Kurum", cancellationToken);

        if (defaultInstitution is not null)
        {
            return defaultInstitution;
        }

        defaultInstitution = new Institution
        {
            Name = "Varsayılan Kurum"
        };

        _dbContext.Institutions.Add(defaultInstitution);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return defaultInstitution;
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
}