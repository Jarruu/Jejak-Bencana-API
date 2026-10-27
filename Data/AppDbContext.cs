using JejakBencana.Api.Domain;
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
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Region).HasMaxLength(200);
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
            e.Property(x => x.Purpose).HasMaxLength(2000).IsRequired();
            e.Property(x => x.PeriodLabel).HasMaxLength(200);
            e.Property(x => x.ContextNote).HasMaxLength(4000);
            e.HasIndex(x => x.CaseId);
        });

        builder.Entity<Source>(e =>
        {
            e.ToTable("sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Publisher).HasMaxLength(300).IsRequired();
            e.Property(x => x.Note).HasMaxLength(2000);
        });

        builder.Entity<RecordSource>(e =>
        {
            e.ToTable("record_sources");
            e.HasKey(x => new { x.FundingRecordId, x.SourceId });
            e.Property(x => x.SupportingNote).HasMaxLength(1000);
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
            e.Property(x => x.Term).HasMaxLength(200).IsRequired();
            e.Property(x => x.Definition).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => x.Term).IsUnique();
            e.HasOne(x => x.Source)
                .WithMany()
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
