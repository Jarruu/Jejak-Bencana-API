using JejakBencana.Api.Domain;
using JejakBencana.Api.Validation;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JejakBencana.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AdminUser>(options)
{
    public DbSet<DisasterCase> Cases => Set<DisasterCase>();
    public DbSet<FundingRecord> FundingRecords => Set<FundingRecord>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<RecordSource> RecordSources => Set<RecordSource>();
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<DisasterCase>(e =>
        {
            e.ToTable("cases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(FieldLimits.CaseTitle).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(FieldLimits.CaseSlug).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(FieldLimits.CaseSummary).IsRequired();
            e.Property(x => x.Region).HasMaxLength(FieldLimits.CaseRegion);
            e.Property(x => x.Status)
                .HasConversion(s => s.ToCode(), c => CaseStatusCodes.FromCode(c))
                .HasMaxLength(20)
                .IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasMany(x => x.FundingRecords)
                .WithOne(x => x.Case)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FundingRecord>(e =>
        {
            e.ToTable("funding_records");
            e.HasKey(x => x.Id);
            // Kode snake_case disimpan apa adanya supaya nilainya tidak berubah bila urutan enum berubah.
            e.Property(x => x.InformationType)
                .HasConversion(t => t.ToCode(), c => InformationTypeCodes.FromCode(c))
                .HasMaxLength(40)
                .IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Purpose).HasMaxLength(FieldLimits.FundingPurpose).IsRequired();
            e.Property(x => x.PeriodLabel).HasMaxLength(FieldLimits.FundingPeriodLabel);
            e.Property(x => x.ContextNote).HasMaxLength(FieldLimits.FundingContextNote);
            e.HasIndex(x => x.CaseId);
        });

        builder.Entity<Source>(e =>
        {
            e.ToTable("sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(FieldLimits.SourceTitle).IsRequired();
            e.Property(x => x.Url).HasMaxLength(FieldLimits.SourceUrl).IsRequired();
            e.Property(x => x.Publisher).HasMaxLength(FieldLimits.SourcePublisher).IsRequired();
            e.Property(x => x.Note).HasMaxLength(FieldLimits.SourceNote);
        });

        builder.Entity<RecordSource>(e =>
        {
            e.ToTable("record_sources");
            e.HasKey(x => new { x.FundingRecordId, x.SourceId });
            e.Property(x => x.SupportingNote).HasMaxLength(FieldLimits.RecordSourceSupportingNote);
            e.HasOne(x => x.FundingRecord)
                .WithMany(r => r.Sources)
                .HasForeignKey(x => x.FundingRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Source)
                .WithMany(s => s.FundingRecords)
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GlossaryTerm>(e =>
        {
            e.ToTable("glossary_terms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Term).HasMaxLength(FieldLimits.GlossaryTerm).IsRequired();
            e.Property(x => x.Definition).HasMaxLength(FieldLimits.GlossaryDefinition).IsRequired();
            e.HasIndex(x => x.Term).IsUnique();
            e.HasOne(x => x.Source)
                .WithMany()
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
