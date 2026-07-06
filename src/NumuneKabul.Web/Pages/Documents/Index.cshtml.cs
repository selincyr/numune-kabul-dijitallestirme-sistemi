using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Documents;

public class IndexModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public IndexModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<PdfDocument> Documents { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Documents = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Include(x => x.Institution)
            .OrderByDescending(x => x.UploadDate)
            .ToListAsync();
    }
}