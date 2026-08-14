using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Templates;

public class EditFieldModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public EditFieldModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string TemplateName { get; private set; } = string.Empty;

    public class InputModel
    {
        public int Id { get; set; }

        public int TemplateId { get; set; }

        [Required(ErrorMessage = "Alan adı zorunludur.")]
        public string FieldName { get; set; } = string.Empty;

        public string Keyword { get; set; } = string.Empty;

        public string Regex { get; set; } = string.Empty;

        public bool Required { get; set; }

        public string DataType { get; set; } = string.Empty;

        public int OrderNo { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var field = await _dbContext.TemplateFields
            .AsNoTracking()
            .Include(x => x.Template)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (field is null)
        {
            return NotFound();
        }

        TemplateName = field.Template?.Name ?? string.Empty;

        Input = new InputModel
        {
            Id = field.Id,
            TemplateId = field.TemplateId,
            FieldName = field.FieldName,
            Keyword = field.Keyword,
            Regex = field.Regex,
            Required = field.Required,
            DataType = field.DataType,
            OrderNo = field.OrderNo
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            var template = await _dbContext.FormTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == Input.TemplateId);

            TemplateName = template?.Name ?? string.Empty;

            return Page();
        }

        var field = await _dbContext.TemplateFields
            .FirstOrDefaultAsync(x => x.Id == Input.Id);

        if (field is null)
        {
            return NotFound();
        }

        field.FieldName = Input.FieldName.Trim();
        field.Keyword = Input.Keyword?.Trim() ?? string.Empty;
        field.Regex = Input.Regex?.Trim() ?? string.Empty;
        field.Required = Input.Required;
        field.DataType = Input.DataType?.Trim() ?? string.Empty;
        field.OrderNo = Input.OrderNo;

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şablon alanı başarıyla güncellendi.";

        return RedirectToPage("./Details", new { id = field.TemplateId });
    }
}