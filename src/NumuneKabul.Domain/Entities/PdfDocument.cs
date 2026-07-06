using NumuneKabul.Domain.Common;
using NumuneKabul.Domain.Enums;

namespace NumuneKabul.Domain.Entities;

public class PdfDocument : BaseEntity
{
    public int InstitutionId { get; set; }

    public Institution? Institution { get; set; }

    public int? TemplateId { get; set; }

    public FormTemplate? Template { get; set; }

    // Kullanıcının yüklediği orijinal dosya adı
    public string FileName { get; set; } = string.Empty;

    // Sunucuda GUID ile saklanan güvenli dosya adı
    public string StoredFileName { get; set; } = string.Empty;

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public PdfStatus Status { get; set; } = PdfStatus.Uploaded;

    public ICollection<OcrResult> OcrResults { get; set; } = new List<OcrResult>();

    public ICollection<ExtractedField> ExtractedFields { get; set; } = new List<ExtractedField>();

    public ICollection<XmlArchive> XmlArchives { get; set; } = new List<XmlArchive>();

    public ICollection<IntegrationJob> IntegrationJobs { get; set; } = new List<IntegrationJob>();
}
