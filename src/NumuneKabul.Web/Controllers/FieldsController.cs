using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Controllers;

[ApiController]
[Route("api/fields")]
[Authorize(Policy = "PersonnelOrAdmin")]
public class FieldsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public FieldsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFieldsAsync(
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

        var fields = await _dbContext.ExtractedFields
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
            fieldCount = fields.Count,
            fields
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFieldsAsync(
        int id,
        [FromBody] UpdateFieldsRequest request,
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

        if (request.Fields.Count == 0)
        {
            return BadRequest(new
            {
                message = "Güncellenecek alan bilgisi gönderilmelidir."
            });
        }

        var fieldIds = request.Fields
            .Select(x => x.FieldId)
            .Distinct()
            .ToList();

        var fields = await _dbContext.ExtractedFields
            .Where(x => x.PdfId == id && fieldIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (!fields.Any())
        {
            return NotFound(new
            {
                message = "Güncellenecek alan bulunamadı."
            });
        }

        var updatedCount = 0;

        foreach (var field in fields)
        {
            var updateItem = request.Fields
                .FirstOrDefault(x => x.FieldId == field.Id);

            if (updateItem is null)
            {
                continue;
            }

            var newValue = updateItem.CorrectedValue?.Trim() ?? string.Empty;
            var oldValue = field.CorrectedValue?.Trim() ?? string.Empty;

            if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
            {
                updatedCount++;
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

        AddAuditLog(
            "ApiFieldUpdate",
            $"{id} numaralı belge için API üzerinden alan güncellemesi yapıldı. Güncellenen alan sayısı: {updatedCount}.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Alanlar başarıyla güncellendi.",
            documentId = id,
            updatedCount
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

    public class UpdateFieldsRequest
    {
        public List<UpdateFieldItem> Fields { get; set; } = new();
    }

    public class UpdateFieldItem
    {
        public int FieldId { get; set; }

        public string? CorrectedValue { get; set; }
    }
}