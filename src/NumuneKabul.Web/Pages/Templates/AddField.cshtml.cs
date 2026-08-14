using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Templates;

public class AddFieldModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public AddFieldModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public FormTemplate? Template { get; private set; }

    [BindProperty]
    public int TemplateId { get; set; }

    [BindProperty]
    public string FieldName { get; set; } = string.Empty;

    [BindProperty]
    public string Keyword { get; set; } = string.Empty;

    [BindProperty]
    public string Regex { get; set; } = string.Empty;

    [BindProperty]
    public bool Required { get; set; }

    [BindProperty]
    public string DataType { get; set; } = "Text";

    [BindProperty]
    public int OrderNo { get; set; }

    public async Task<IActionResult> OnGetAsync(int templateId)
    {
        Template = await _dbContext.FormTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == templateId);

        if (Template is null)
        {
            return NotFound();
        }

        TemplateId = templateId;

        var lastOrderNo = await _dbContext.TemplateFields
            .Where(x => x.TemplateId == templateId)
            .Select(x => (int?)x.OrderNo)
            .MaxAsync();

        OrderNo = (lastOrderNo ?? 0) + 1;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Template = await _dbContext.FormTemplates
            .FirstOrDefaultAsync(x => x.Id == TemplateId);

        if (Template is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(FieldName))
        {
            TempData["ErrorMessage"] = "Alan adı boş bırakılamaz.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Regex))
        {
            TempData["ErrorMessage"] = "Regex kuralı boş bırakılamaz.";
            return Page();
        }

        var field = new TemplateField
        {
            TemplateId = TemplateId,
            FieldName = FieldName.Trim(),
            Keyword = Keyword?.Trim() ?? string.Empty,
            Regex = Regex.Trim(),
            Required = Required,
            DataType = string.IsNullOrWhiteSpace(DataType)
                ? "Text"
                : DataType.Trim(),
            OrderNo = OrderNo
        };

        _dbContext.TemplateFields.Add(field);

        AddAuditLog(
            "TemplateFieldCreate",
            $"{TemplateId} numaralı şablona yeni alan eklendi. Alan adı: {field.FieldName}");

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şablon alanı başarıyla eklendi.";

        return RedirectToPage("./Details", new { id = TemplateId });
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