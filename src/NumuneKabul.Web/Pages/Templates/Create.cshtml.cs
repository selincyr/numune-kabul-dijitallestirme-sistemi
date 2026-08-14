using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Templates;

public class CreateModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public CreateModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await EnsureDefaultInstitutionAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var institution = await EnsureDefaultInstitutionAsync();

        if (string.IsNullOrWhiteSpace(Name))
        {
            TempData["ErrorMessage"] = "Şablon adı boş bırakılamaz.";
            return Page();
        }

        var templateExists = await _dbContext.FormTemplates
            .AnyAsync(x =>
                x.InstitutionId == institution.Id &&
                x.Name.ToLower() == Name.Trim().ToLower());

        if (templateExists)
        {
            TempData["ErrorMessage"] = "Bu isimde bir şablon zaten var.";
            return Page();
        }

        var template = new FormTemplate
        {
            InstitutionId = institution.Id,
            Name = Name.Trim(),
            Description = Description?.Trim()
        };

        _dbContext.FormTemplates.Add(template);

        AddAuditLog(
            "TemplateCreate",
            $"Yeni form şablonu oluşturuldu. Şablon adı: {template.Name}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şablon başarıyla oluşturuldu.";

        return RedirectToPage("./Details", new { id = template.Id });
    }

    private async Task<Institution> EnsureDefaultInstitutionAsync()
    {
        var institution = await _dbContext.Institutions
            .FirstOrDefaultAsync(x => x.Name == "Varsayılan Kurum");

        if (institution is not null)
        {
            return institution;
        }

        institution = new Institution
        {
            Name = "Varsayılan Kurum"
        };

        _dbContext.Institutions.Add(institution);
        await _dbContext.SaveChangesAsync();

        return institution;
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