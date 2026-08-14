using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Institutions;

public class IndexModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public IndexModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<InstitutionListItem> Institutions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadInstitutionsAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var institution = await _dbContext.Institutions
            .FirstOrDefaultAsync(x => x.Id == id);

        if (institution is null)
        {
            return NotFound();
        }

        var hasTemplates = await _dbContext.FormTemplates
            .AnyAsync(x => x.InstitutionId == id);

        var hasDocuments = await _dbContext.PdfDocuments
            .AnyAsync(x => x.InstitutionId == id);

        if (hasTemplates || hasDocuments)
        {
            TempData["ErrorMessage"] =
                "Bu kuruma bağlı şablon veya belge bulunduğu için kurum silinemez.";

            return RedirectToPage();
        }

        _dbContext.Institutions.Remove(institution);

        AddAuditLog(
            "InstitutionDelete",
            $"Kurum silindi. Kurum adı: {institution.Name}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Kurum başarıyla silindi.";

        return RedirectToPage();
    }

    private async Task LoadInstitutionsAsync()
    {
        Institutions = await _dbContext.Institutions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new InstitutionListItem
            {
                Id = x.Id,
                Name = x.Name,
                TemplateCount = _dbContext.FormTemplates.Count(t => t.InstitutionId == x.Id),
                DocumentCount = _dbContext.PdfDocuments.Count(d => d.InstitutionId == x.Id)
            })
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

    public class InstitutionListItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int TemplateCount { get; set; }

        public int DocumentCount { get; set; }
    }
}