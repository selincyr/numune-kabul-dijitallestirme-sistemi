using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Institutions;

public class EditModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public EditModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var institution = await _dbContext.Institutions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (institution is null)
        {
            return NotFound();
        }

        Id = institution.Id;
        Name = institution.Name;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var institution = await _dbContext.Institutions
            .FirstOrDefaultAsync(x => x.Id == Id);

        if (institution is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            TempData["ErrorMessage"] = "Kurum adı boş bırakılamaz.";
            return Page();
        }

        var normalizedName = Name.Trim();

        var sameNameExists = await _dbContext.Institutions
            .AnyAsync(x =>
                x.Id != Id &&
                x.Name.ToLower() == normalizedName.ToLower());

        if (sameNameExists)
        {
            TempData["ErrorMessage"] = "Bu isimde başka bir kurum zaten var.";
            return Page();
        }

        var oldName = institution.Name;

        institution.Name = normalizedName;

        AddAuditLog(
            "InstitutionUpdate",
            $"Kurum güncellendi. Eski ad: {oldName}, Yeni ad: {institution.Name}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Kurum başarıyla güncellendi.";

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