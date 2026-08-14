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
    public int InstitutionId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    public List<Institution> Institutions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await EnsureDefaultInstitutionAsync();
        await LoadInstitutionsAsync();

        InstitutionId = Institutions.FirstOrDefault()?.Id ?? 0;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadInstitutionsAsync();

        var institution = await _dbContext.Institutions
            .FirstOrDefaultAsync(x => x.Id == InstitutionId);

        if (institution is null)
        {
            TempData["ErrorMessage"] = "Geçerli bir kurum seçilmelidir.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            TempData["ErrorMessage"] = "Şablon adı boş bırakılamaz.";
            return Page();
        }

        var normalizedName = Name.Trim();

        var templateExists = await _dbContext.FormTemplates
            .AnyAsync(x =>
                x.InstitutionId == institution.Id &&
                x.Name.ToLower() == normalizedName.ToLower());

        if (templateExists)
        {
            TempData["ErrorMessage"] = "Bu kurum için aynı isimde bir şablon zaten var.";
            return Page();
        }

        var template = new FormTemplate
        {
            InstitutionId = institution.Id,
            Name = normalizedName,
            Description = Description?.Trim()
        };

        _dbContext.FormTemplates.Add(template);

        AddAuditLog(
            "TemplateCreate",
            $"Yeni form şablonu oluşturuldu. Kurum: {institution.Name}, Şablon adı: {template.Name}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şablon başarıyla oluşturuldu.";

        return RedirectToPage("./Details", new { id = template.Id });
    }

    private async Task LoadInstitutionsAsync()
    {
        Institutions = await _dbContext.Institutions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
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