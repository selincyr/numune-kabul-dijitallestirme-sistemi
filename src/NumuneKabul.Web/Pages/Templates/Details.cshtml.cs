using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Templates;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public DetailsModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public FormTemplate? Template { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Template = await _dbContext.FormTemplates
            .AsNoTracking()
            .Include(x => x.Institution)
            .Include(x => x.TemplateFields)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Template is null)
        {
            return NotFound();
        }

        Template.TemplateFields = Template.TemplateFields
            .OrderBy(x => x.OrderNo)
            .ThenBy(x => x.FieldName)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteFieldAsync(int id, int fieldId)
    {
        var templateExists = await _dbContext.FormTemplates
            .AnyAsync(x => x.Id == id);

        if (!templateExists)
        {
            return NotFound();
        }

        var field = await _dbContext.TemplateFields
            .FirstOrDefaultAsync(x =>
                x.Id == fieldId &&
                x.TemplateId == id);

        if (field is null)
        {
            return NotFound();
        }

        _dbContext.TemplateFields.Remove(field);

        AddAuditLog(
            "TemplateFieldDelete",
            $"{id} numaralı şablondan alan silindi. Alan adı: {field.FieldName}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şablon alanı başarıyla silindi.";

        return RedirectToPage(new { id });
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