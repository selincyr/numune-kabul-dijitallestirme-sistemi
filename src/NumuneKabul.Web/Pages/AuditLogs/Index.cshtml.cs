using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.AuditLogs;

public class IndexModel : PageModel
{
    private readonly AppDbContext _dbContext;

    public IndexModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<AuditLog> AuditLogs { get; private set; } = new();

    public async Task OnGetAsync()
    {
        AuditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.Date)
            .Take(100)
            .ToListAsync();
    }
}