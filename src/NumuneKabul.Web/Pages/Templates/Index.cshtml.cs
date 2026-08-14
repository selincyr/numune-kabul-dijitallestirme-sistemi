using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Templates;

public class IndexModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public IndexModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<FormTemplate> Templates { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadTemplatesAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var template = await _dbContext.FormTemplates
            .Include(x => x.TemplateFields)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (template is null)
        {
            return NotFound();
        }

        var relatedDocuments = await _dbContext.PdfDocuments
            .Where(x => x.TemplateId == id)
            .ToListAsync();

        foreach (var document in relatedDocuments)
        {
            document.TemplateId = null;
        }

        _dbContext.TemplateFields.RemoveRange(template.TemplateFields);
        _dbContext.FormTemplates.Remove(template);

        AddAuditLog(
            "TemplateDelete",
            $"Form şablonu silindi. Şablon adı: {template.Name}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şablon başarıyla silindi.";

        return RedirectToPage();
    }

    private async Task LoadTemplatesAsync()
    {
        Templates = await _dbContext.FormTemplates
            .AsNoTracking()
            .Include(x => x.Institution)
            .Include(x => x.TemplateFields)
            .OrderBy(x => x.Institution!.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();
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