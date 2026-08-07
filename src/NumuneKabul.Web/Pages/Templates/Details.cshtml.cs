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
            .ToList();

        return Page();
    }
}