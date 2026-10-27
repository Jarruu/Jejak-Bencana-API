using JejakBencana.Api.Contracts;
using JejakBencana.Api.Data;
using JejakBencana.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JejakBencana.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Publik");

        group.MapGet("/cases", async (AppDbContext db, CancellationToken ct) =>
        {
            var cases = await db.Cases
                .AsNoTracking()
                .Where(c => c.Status == CaseStatus.Published)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new PublicCaseListItemDto(c.Slug, c.Title, c.Summary, c.Region, c.UpdatedAt))
                .ToListAsync(ct);

            return Results.Ok(cases);
        })
        .WithName("GetPublicCases")
        .WithSummary("Daftar kasus yang sudah diterbitkan.");

        group.MapGet("/cases/{slug}", async (string slug, AppDbContext db, CancellationToken ct) =>
        {
            // Dimuat sebagai entitas lalu dipetakan di memori: ToCode() tidak bisa diterjemahkan EF ke SQL.
            var entity = await db.Cases
                .AsNoTracking()
                .Include(c => c.FundingRecords.OrderBy(r => r.InformationType))
                    .ThenInclude(r => r.Sources)
                        .ThenInclude(rs => rs.Source)
                .Where(c => c.Status == CaseStatus.Published && c.Slug == slug)
                .FirstOrDefaultAsync(ct);

            if (entity is null)
                return Results.Json(
                    ApiErrorResponse.Single("case_not_found", null, "Kasus tidak ditemukan."),
                    statusCode: StatusCodes.Status404NotFound);

            var detail = new PublicCaseDetailDto(
                entity.Slug,
                entity.Title,
                entity.Summary,
                entity.Region,
                entity.UpdatedAt,
                entity.FundingRecords.Select(r => new PublicFundingRecordDto(
                        r.Id,
                        r.InformationType.ToCode(),
                        r.Amount,
                        r.Currency,
                        r.Purpose,
                        r.PeriodLabel,
                        r.ContextNote,
                        r.Sources.Select(rs => new PublicSourceDto(
                                rs.Source.Id,
                                rs.Source.Title,
                                rs.Source.Url,
                                rs.Source.Publisher,
                                rs.Source.PublishedAt,
                                rs.Source.CheckedAt,
                                rs.Source.Note,
                                rs.SupportingNote))
                            .ToList()))
                    .ToList());

            return Results.Ok(detail);
        })
        .WithName("GetPublicCaseBySlug")
        .WithSummary("Detail kasus yang sudah diterbitkan. Draft selalu 404.");

        group.MapGet("/glossary", async (AppDbContext db, CancellationToken ct) =>
        {
            var terms = await db.GlossaryTerms
                .AsNoTracking()
                .OrderBy(t => t.Term)
                .Select(t => new PublicGlossaryTermDto(
                    t.Term,
                    t.Definition,
                    t.Source == null
                        ? null
                        : new PublicSourceDto(
                            t.Source.Id,
                            t.Source.Title,
                            t.Source.Url,
                            t.Source.Publisher,
                            t.Source.PublishedAt,
                            t.Source.CheckedAt,
                            t.Source.Note,
                            null)))
                .ToListAsync(ct);

            return Results.Ok(terms);
        })
        .WithName("GetPublicGlossary")
        .WithSummary("Daftar istilah glosarium.");

        return app;
    }
}
