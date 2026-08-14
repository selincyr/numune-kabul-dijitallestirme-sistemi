using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;
using NumuneKabul.Infrastructure.Data;

namespace NumuneKabul.Web.Pages.Documents;

public class UploadModel : PageModel
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IPdfRenderer _pdfRenderer;

    public UploadModel(
        AppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IPdfRenderer pdfRenderer)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
        _pdfRenderer = pdfRenderer;
    }

    [BindProperty]
    public IFormFile? PdfFile { get; set; }

    [BindProperty]
    public int? SelectedTemplateId { get; set; }

    public List<FormTemplate> Templates { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await EnsureDefaultTemplateAsync();
        await LoadTemplatesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await EnsureDefaultTemplateAsync();
        await LoadTemplatesAsync();

        if (PdfFile is null || PdfFile.Length == 0)
        {
            TempData["ErrorMessage"] = "PDF dosyası seçilmelidir.";
            return Page();
        }

        var extension = Path.GetExtension(PdfFile.FileName);

        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Sadece PDF dosyası yüklenebilir.";
            return Page();
        }

        var institution = await GetOrCreateDefaultInstitutionAsync();

        var selectedTemplateId = await ResolveTemplateIdAsync(
            institution.Id,
            SelectedTemplateId);

        var uploadDirectorySetting = _configuration["FileStorage:UploadDirectory"]
            ?? "Storage/Uploads";

        var uploadDirectory = Path.Combine(
            _environment.ContentRootPath,
            uploadDirectorySetting);

        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{Guid.NewGuid():N}.pdf";

        var filePath = Path.Combine(
            uploadDirectory,
            storedFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await PdfFile.CopyToAsync(stream);
        }

        var document = new PdfDocument
        {
            InstitutionId = institution.Id,
            TemplateId = selectedTemplateId,
            FileName = PdfFile.FileName,
            StoredFileName = storedFileName,
            UploadDate = DateTime.UtcNow,
            Status = PdfStatus.Uploaded
        };

        _dbContext.PdfDocuments.Add(document);

        AddAuditLog(
            "PdfUpload",
            $"PDF yüklendi. Dosya adı: {PdfFile.FileName}");

        await _dbContext.SaveChangesAsync();

        try
        {
            var renderedPagesRoot = Path.Combine(
                _environment.ContentRootPath,
                "Storage",
                "RenderedPages");

            var renderedFiles = await _pdfRenderer.RenderPdfAsync(
                document.Id,
                filePath,
                renderedPagesRoot);

            AddAuditLog(
                "PdfRender",
                $"{document.Id} numaralı belge için {renderedFiles.Count} sayfa PNG olarak oluşturuldu.");

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            AddAuditLog(
                "PdfRenderError",
                $"{document.Id} numaralı belge için PNG oluşturma hatası: {ex.Message}");

            await _dbContext.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = "PDF başarıyla yüklendi.";

        return RedirectToPage("./Details", new { id = document.Id });
    }

    private async Task LoadTemplatesAsync()
    {
        Templates = await _dbContext.FormTemplates
            .AsNoTracking()
            .Include(x => x.Institution)
            .OrderBy(x => x.Institution!.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    private async Task<Institution> GetOrCreateDefaultInstitutionAsync()
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

    private async Task EnsureDefaultTemplateAsync()
    {
        var institution = await GetOrCreateDefaultInstitutionAsync();

        var templateExists = await _dbContext.FormTemplates
            .AnyAsync(x => x.InstitutionId == institution.Id);

        if (templateExists)
        {
            return;
        }

        var template = new FormTemplate
        {
            InstitutionId = institution.Id,
            Name = "Varsayılan Numune Kabul Şablonu",
            Description = "PDF yükleme sırasında seçilebilir varsayılan form şablonu."
        };

        _dbContext.FormTemplates.Add(template);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<int?> ResolveTemplateIdAsync(
        int institutionId,
        int? templateId)
    {
        if (templateId.HasValue)
        {
            var selectedTemplateExists = await _dbContext.FormTemplates
                .AnyAsync(x =>
                    x.Id == templateId.Value &&
                    x.InstitutionId == institutionId);

            if (selectedTemplateExists)
            {
                return templateId.Value;
            }
        }

        var defaultTemplate = await _dbContext.FormTemplates
            .Where(x => x.InstitutionId == institutionId)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();

        return defaultTemplate?.Id;
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