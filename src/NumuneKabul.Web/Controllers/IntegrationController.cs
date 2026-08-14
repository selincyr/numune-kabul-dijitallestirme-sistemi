using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Controllers;

[ApiController]
[Route("api/integration")]
[Authorize(Policy = "IntegrationOrAdmin")]
public class IntegrationController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IIntegrationService _integrationService;

    public IntegrationController(
        AppDbContext dbContext,
        IIntegrationService integrationService)
    {
        _dbContext = dbContext;
        _integrationService = integrationService;
    }

    [HttpPost("send/{id:int}")]
    public async Task<IActionResult> SendAsync(
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

        var latestXml = await _dbContext.XmlArchives
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

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
                "ApiIntegrationFailed",
                $"{id} numaralı belge API üzerinden entegrasyona gönderilemedi. XML kaydı bulunamadı.");

            await _dbContext.SaveChangesAsync(cancellationToken);

            return BadRequest(new
            {
                message = "Entegrasyona göndermek için önce XML oluşturulmalıdır.",
                documentId = id,
                status = failedJob.Status.ToString(),
                failedJob.LastErrorMessage
            });
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
            "ApiIntegrationStart",
            $"{id} numaralı belge için API üzerinden mock entegrasyon gönderimi başlatıldı.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _integrationService.SendXmlAsync(
                id,
                latestXml.XmlContent,
                cancellationToken);

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
                    "ApiIntegrationSuccess",
                    $"{id} numaralı belge API üzerinden mock REST servisine başarıyla gönderildi.");
            }
            else
            {
                AddAuditLog(
                    "ApiIntegrationFailed",
                    $"{id} numaralı belge API üzerinden mock REST servisine gönderilemedi. Hata: {result.Message}");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = result.Message,
                documentId = id,
                integrationJobId = integrationJob.Id,
                status = integrationJob.Status.ToString(),
                integrationJob.RetryCount,
                integrationJob.LastAttemptDate,
                integrationJob.LastErrorMessage
            });
        }
        catch (Exception ex)
        {
            integrationJob.Status = IntegrationStatus.Failed;
            integrationJob.LastAttemptDate = DateTime.UtcNow;
            integrationJob.LastErrorMessage = ex.Message;

            AddAuditLog(
                "ApiIntegrationError",
                $"{id} numaralı belge API üzerinden entegrasyona gönderilirken hata oluştu: {ex.Message}");

            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(500, new
            {
                message = "Entegrasyon gönderimi sırasında hata oluştu.",
                documentId = id,
                integrationJobId = integrationJob.Id,
                status = integrationJob.Status.ToString(),
                error = ex.Message
            });
        }
    }

    [HttpGet("status/{id:int}")]
    public async Task<IActionResult> GetStatusAsync(
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

        var jobs = await _dbContext.IntegrationJobs
            .AsNoTracking()
            .Where(x => x.PdfId == id)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                x.Id,
                x.PdfId,
                Status = x.Status.ToString(),
                x.RetryCount,
                x.CreatedDate,
                x.LastAttemptDate,
                x.LastErrorMessage
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            documentId = id,
            integrationJobCount = jobs.Count,
            latestJob = jobs.FirstOrDefault(),
            jobs
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