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
        Templates = await _dbContext.FormTemplates
            .AsNoTracking()
            .Include(x => x.Institution)
            .Include(x => x.TemplateFields)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}