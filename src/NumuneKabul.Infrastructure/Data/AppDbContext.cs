using Microsoft.EntityFrameworkCore;
using NumuneKabul.Domain.Entities;

namespace NumuneKabul.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<PdfDocument> PdfDocuments => Set<PdfDocument>();
    public DbSet<OcrResult> OcrResults => Set<OcrResult>();
    public DbSet<ExtractedField> ExtractedFields => Set<ExtractedField>();
    public DbSet<XmlArchive> XmlArchives => Set<XmlArchive>();
    public DbSet<IntegrationJob> IntegrationJobs => Set<IntegrationJob>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();

            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();

            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);

            entity.HasOne(x => x.Institution)
                .WithMany(x => x.FormTemplates)
                .HasForeignKey(x => x.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TemplateField>(entity =>
        {
            entity.Property(x => x.FieldName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Keyword).HasMaxLength(300);
            entity.Property(x => x.Regex).HasMaxLength(2000);
            entity.Property(x => x.DataType).HasMaxLength(100);

            entity.HasOne(x => x.Template)
                .WithMany(x => x.TemplateFields)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.TemplateId, x.FieldName }).IsUnique();
        });

        modelBuilder.Entity<PdfDocument>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.StoredFileName).HasMaxLength(500).IsRequired();

            entity.HasOne(x => x.Institution)
                .WithMany()
                .HasForeignKey(x => x.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Template)
                .WithMany()
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OcrResult>(entity =>
        {
            entity.HasOne(x => x.PdfDocument)
                .WithMany(x => x.OcrResults)
                .HasForeignKey(x => x.PdfId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExtractedField>(entity =>
        {
            entity.Property(x => x.FieldName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Confidence).HasPrecision(5, 2);

            entity.HasOne(x => x.PdfDocument)
                .WithMany(x => x.ExtractedFields)
                .HasForeignKey(x => x.PdfId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<XmlArchive>(entity =>
        {
            entity.HasOne(x => x.PdfDocument)
                .WithMany(x => x.XmlArchives)
                .HasForeignKey(x => x.PdfId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationJob>(entity =>
        {
            entity.HasOne(x => x.PdfDocument)
                .WithMany(x => x.IntegrationJobs)
                .HasForeignKey(x => x.PdfId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
