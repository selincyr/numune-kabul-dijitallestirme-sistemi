using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Institutions;

public class CreateModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public CreateModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            TempData["ErrorMessage"] = "Kurum adı boş bırakılamaz.";
            return Page();
        }

        var normalizedName = Name.Trim();

        var institutionExists = await _dbContext.Institutions
            .AnyAsync(x => x.Name.ToLower() == normalizedName.ToLower());

        if (institutionExists)
        {
            TempData["ErrorMessage"] = "Bu isimde bir kurum zaten var.";
            return Page();
        }

        var institution = new Institution
        {
            Name = normalizedName
        };

        _dbContext.Institutions.Add(institution);

        AddAuditLog(
            "InstitutionCreate",
            $"Yeni kurum oluşturuldu. Kurum adı: {institution.Name}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Kurum başarıyla oluşturuldu.";

        return RedirectToPage("./Index");
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