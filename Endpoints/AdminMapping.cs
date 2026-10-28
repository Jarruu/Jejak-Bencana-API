using JejakBencana.Api.Contracts;
using JejakBencana.Api.Data;
using JejakBencana.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JejakBencana.Api.Endpoints;

public static class AdminMapping
{
    public static AdminSourceDto ToDto(this Source s) => new(
        s.Id, s.Title, s.Url, s.Publisher, s.PublishedAt, s.CheckedAt, s.Note);

    public static AdminFundingRecordDto ToDto(this FundingRecord r) => new(
        r.Id,
        r.CaseId,
        r.InformationType.ToCode(),
        r.Amount,
        r.Currency,
        r.Purpose,
        r.PeriodLabel,
        r.ContextNote,
        r.Sources.Select(rs => new AdminRecordSourceDto(rs.SourceId, rs.SupportingNote)).ToList());

    public static AdminCaseDetailDto ToDetailDto(this DisasterCase c) => new(
        c.Id, c.Title, c.Slug, c.Summary, c.Region, c.Status.ToCode(), c.CreatedAt, c.UpdatedAt,
        c.FundingRecords.Select(r => r.ToDto()).ToList());

    public static AdminGlossaryTermDto ToDto(this GlossaryTerm t) => new(
        t.Id, t.Term, t.Definition, t.SourceId, t.Source?.ToDto());
}

public static class CaseStatusGuard
{
    /// <summary>
    /// Perubahan pada kasus Published menurunkannya ke Draft (keputusan SPECS §15 no.6),
    /// supaya data yang belum lolos validasi publikasi tidak tampil ke publik.
    /// </summary>
    public static async Task DemoteIfPublishedAsync(AppDbContext db, Guid caseId, CancellationToken ct)
    {
        var current = await db.Cases.FirstOrDefaultAsync(c => c.Id == caseId, ct);
        if (current is null || current.Status != CaseStatus.Published) return;

        current.Status = CaseStatus.Draft;
        current.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Menurunkan setiap kasus yang memakai sumber ini, karena isi sumber ikut berubah.</summary>
    public static async Task DemoteCasesUsingSourceAsync(AppDbContext db, Guid sourceId, CancellationToken ct)
    {
        var caseIds = await db.RecordSources
            .Where(rs => rs.SourceId == sourceId)
            .Select(rs => rs.FundingRecord.CaseId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var caseId in caseIds)
            await DemoteIfPublishedAsync(db, caseId, ct);
    }
}
