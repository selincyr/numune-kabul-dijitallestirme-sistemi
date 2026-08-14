using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Controllers;

[ApiController]
[Route("api/xml")]
public class XmlController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IXmlGenerationService _xmlGenerationService;

    public XmlController(
        AppDbContext dbContext,
        IXmlGenerationService xmlGenerationService)
    {
        _dbContext = dbContext;
        _xmlGenerationService = xmlGenerationService;
    }

    [HttpPost("create/{id:int}")]
    public async Task<IActionResult> CreateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.PdfDocuments
            .Include(x => x.Institution)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound(new
            {
                message = "Belge bulunamadı."
            });
        }

        var fields = await _dbContext.ExtractedFields
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ThenBy(x => x.FieldName)
            .ToListAsync(cancellationToken);

        if (!fields.Any())
        {
            AddAuditLog(
                "ApiXmlCreateError",
                $"{id} numaralı belge için API üzerinden XML oluşturulamadı. ExtractedFields kaydı bulunamadı.");

            await _dbContext.SaveChangesAsync(cancellationToken);

            return BadRequest(new
            {
                message = "XML oluşturmak için önce OCR ve alan çıkarma işlemi yapılmalıdır."
            });
        }

        var ocrResults = await _dbContext.OcrResults
            .Where(x => x.PdfId == id)
            .OrderBy(x => x.PageNo)
            .ToListAsync(cancellationToken);

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
            "ApiXmlCreate",
            $"{id} numaralı belge için API üzerinden XML oluşturuldu ve XmlArchives tablosuna kaydedildi.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "XML başarıyla oluşturuldu ve arşivlendi.",
            documentId = id,
            xmlArchiveId = xmlArchive.Id,
            xmlArchive.CreatedDate,
            xmlContent
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var documentExists = await _dbContext.PdfDocuments
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!documentExists)
        {
            return NotFound(new
            {
                message = "Belge bulunamadı."
            });
        }

        var xmlArchives = await _dbContext.XmlArchives
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                x.Id,
                x.PdfId,
                x.CreatedDate,
                x.XmlContent
            })
            .ToListAsync(cancellationToken);

        if (!xmlArchives.Any())
        {
            return NotFound(new
            {
                message = "Bu belge için XML kaydı bulunamadı."
            });
        }

        return Ok(new
        {
            documentId = id,
            xmlArchiveCount = xmlArchives.Count,
            latestXml = xmlArchives.First(),
            xmlArchives
        });
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