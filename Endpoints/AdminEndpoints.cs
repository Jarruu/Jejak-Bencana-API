using JejakBencana.Api.Contracts;
using JejakBencana.Api.Data;
using JejakBencana.Api.Domain;
using JejakBencana.Api.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JejakBencana.Api.Endpoints;

public static class AdminEndpoints
{
    private static readonly string[] AllowedCurrencies = ["IDR"];

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization();

        group.MapPost("/login", Login).AllowAnonymous().WithName("AdminLogin")
            .WithSummary("Mulai sesi admin.")
            .WithDescription("Mengembalikan 401 dengan pesan umum bila kredensial salah, dan 429 bila akun terkunci karena percobaan berulang.");

        group.MapPost("/logout", Logout).WithName("AdminLogout")
            .WithSummary("Akhiri sesi admin.");

        group.MapGet("/cases", GetCases).WithName("AdminGetCases")
            .WithSummary("Daftar kasus termasuk draft.");
        group.MapGet("/cases/{id:guid}", GetCase).WithName("AdminGetCase")
            .WithSummary("Detail kasus beserta catatan dana dan relasi sumber.");
        group.MapPost("/cases", CreateCase).WithName("AdminCreateCase")
            .WithSummary("Buat kasus baru. Status awal draft.");
        group.MapPatch("/cases/{id:guid}", UpdateCase).WithName("AdminUpdateCase")
            .WithSummary("Ubah kasus. Kasus published kembali ke draft.");
        group.MapPost("/cases/{id:guid}/publish", PublishCase).WithName("AdminPublishCase")
            .WithSummary("Terbitkan kasus setelah validasi.")
            .WithDescription("Mengembalikan 422 beserta daftar pelanggaran bila judul/slug/ringkasan kosong, tidak ada catatan dana, nominal bukan bilangan positif, tujuan kosong, catatan dana tanpa sumber, atau URL/checkedAt sumber tidak valid.");
        group.MapPost("/cases/{id:guid}/unpublish", UnpublishCase).WithName("AdminUnpublishCase")
            .WithSummary("Turunkan kasus published ke draft.");

        group.MapPost("/funding-records", CreateFundingRecord).WithName("AdminCreateFundingRecord")
            .WithSummary("Buat catatan dana dan kaitkan sumber.");
        group.MapPatch("/funding-records/{id:guid}", UpdateFundingRecord).WithName("AdminUpdateFundingRecord")
            .WithSummary("Ubah catatan dana.")
            .WithDescription("Field sources bersifat opsional: null berarti daftar sumber tidak diubah, [] mengosongkannya.");
        group.MapDelete("/funding-records/{id:guid}", DeleteFundingRecord).WithName("AdminDeleteFundingRecord")
            .WithSummary("Hapus catatan dana.");

        group.MapGet("/sources", GetSources).WithName("AdminGetSources")
            .WithSummary("Daftar sumber.");
        group.MapPost("/sources", CreateSource).WithName("AdminCreateSource")
            .WithSummary("Buat sumber.");
        group.MapPatch("/sources/{id:guid}", UpdateSource).WithName("AdminUpdateSource")
            .WithSummary("Ubah sumber. Kasus yang memakainya kembali ke draft.");
        group.MapDelete("/sources/{id:guid}", DeleteSource).WithName("AdminDeleteSource")
            .WithSummary("Hapus sumber.");

        group.MapGet("/glossary", GetGlossary).WithName("AdminGetGlossary")
            .WithSummary("Daftar istilah glosarium.");
        group.MapPost("/glossary", CreateGlossaryTerm).WithName("AdminCreateGlossaryTerm")
            .WithSummary("Buat istilah glosarium.");
        group.MapPatch("/glossary/{id:guid}", UpdateGlossaryTerm).WithName("AdminUpdateGlossaryTerm")
            .WithSummary("Ubah istilah glosarium.");
        group.MapDelete("/glossary/{id:guid}", DeleteGlossaryTerm).WithName("AdminDeleteGlossaryTerm")
            .WithSummary("Hapus istilah glosarium.");

        return app;
    }

    // ---------------- Autentikasi ----------------

    private static async Task<IResult> Login(
        LoginRequest request,
        SignInManager<AdminUser> signInManager,
        UserManager<AdminUser> userManager)
    {
        // Pesan sama untuk email salah, kata sandi salah, dan akun tidak dikenal.
        const string genericMessage = "Email atau kata sandi salah.";

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Fail(400, "invalid_credentials", "email", genericMessage);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // shortcut: email tidak dikenal dijawab tanpa hashing, jadi waktunya lebih singkat
            // daripada kata sandi salah. Perbaiki dengan dummy hash bila enumerasi akun jadi risiko nyata.
            return Fail(401, "invalid_credentials", "email", genericMessage);
        }

        // lockoutOnFailure: lima percobaan gagal mengunci akun sementara (lihat konfigurasi di Program.cs).
        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Fail(429, "too_many_attempts", null, "Terlalu banyak percobaan masuk. Coba lagi beberapa menit lagi.");

        if (result.IsNotAllowed)
            return Fail(403, "not_allowed", null, "Akun ini tidak diizinkan masuk.");

        if (!result.Succeeded)
            return Fail(401, "invalid_credentials", "email", genericMessage);

        return Results.Ok(new LoginResponseDto(user.Email ?? ""));
    }

    private static async Task<IResult> Logout(SignInManager<AdminUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    // ---------------- Kasus ----------------

    private static async Task<IResult> GetCases(AppDbContext db, CancellationToken ct)
    {
        var cases = await db.Cases
            .AsNoTracking()
            .Include(c => c.FundingRecords)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);

        return Results.Ok(cases.Select(c => c.ToListDto()).ToList());
    }

    private static async Task<IResult> GetCase(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Cases
            .AsNoTracking()
            .Include(c => c.FundingRecords)
                .ThenInclude(r => r.Sources)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return entity is null ? NotFoundCase() : Results.Ok(entity.ToDetailDto());
    }

    private static async Task<IResult> CreateCase(CreateCaseRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateCaseFields(request.Title, request.Slug, request.Summary, out var slug);
        if (errors.Count > 0) return Validation(errors);

        if (await db.Cases.AnyAsync(c => c.Slug == slug, ct))
            return Fail(400, "duplicate_slug", "slug", "Slug sudah dipakai kasus lain.");

        var now = DateTimeOffset.UtcNow;
        var entity = new DisasterCase
        {
            Title = request.Title!.Trim(),
            Slug = slug,
            Summary = request.Summary!.Trim(),
            Region = Normalize(request.Region),
            Status = CaseStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Cases.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/cases/{entity.Id}", entity.ToDetailDto());
    }

    private static async Task<IResult> UpdateCase(Guid id, UpdateCaseRequest request, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Cases
            .Include(c => c.FundingRecords)
                .ThenInclude(r => r.Sources)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return NotFoundCase();

        var errors = ValidateCaseFields(request.Title, request.Slug, request.Summary, out var slug);
        if (errors.Count > 0) return Validation(errors);

        if (await db.Cases.AnyAsync(c => c.Slug == slug && c.Id != id, ct))
            return Fail(400, "duplicate_slug", "slug", "Slug sudah dipakai kasus lain.");

        entity.Title = request.Title!.Trim();
        entity.Slug = slug;
        entity.Summary = request.Summary!.Trim();
        entity.Region = Normalize(request.Region);
        entity.Status = CaseStatus.Draft; // edit pada kasus terbit menurunkan status
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(entity.ToDetailDto());
    }

    private static async Task<IResult> PublishCase(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Cases
            .Include(c => c.FundingRecords)
                .ThenInclude(r => r.Sources)
                    .ThenInclude(rs => rs.Source)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null) return NotFoundCase();

        var errors = PublicationValidator.Validate(entity);
        if (errors.Count > 0) return Validation(errors);

        entity.Status = CaseStatus.Published;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(entity.ToDetailDto());
    }

    private static async Task<IResult> UnpublishCase(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFoundCase();

        entity.Status = CaseStatus.Draft;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ---------------- Catatan dana ----------------

    private static async Task<IResult> CreateFundingRecord(CreateFundingRecordRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Cases.AnyAsync(c => c.Id == request.CaseId, ct))
            return Fail(400, "unknown_case", "caseId", "Kasus tidak ditemukan.");

        var errors = ValidateFundingFields(request.InformationType, request.Amount, request.Currency, out var type, out var currency);
        if (errors.Count > 0) return Validation(errors);

        var sources = await ResolveSourcesAsync(db, request.Sources, ct);
        if (sources.Errors.Count > 0) return Validation(sources.Errors);

        var entity = new FundingRecord
        {
            CaseId = request.CaseId,
            InformationType = type,
            Amount = request.Amount,
            Currency = currency,
            Purpose = Normalize(request.Purpose) ?? "",
            PeriodLabel = Normalize(request.PeriodLabel),
            ContextNote = Normalize(request.ContextNote),
            Sources = sources.Value,
        };

        db.FundingRecords.Add(entity);
        await CaseStatusGuard.DemoteIfPublishedAsync(db, request.CaseId, ct);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/funding-records/{entity.Id}", entity.ToDto());
    }

    private static async Task<IResult> UpdateFundingRecord(Guid id, UpdateFundingRecordRequest request, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.FundingRecords
            .Include(r => r.Sources)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity is null) return NotFoundFundingRecord();

        var errors = ValidateFundingFields(request.InformationType, request.Amount, request.Currency, out var type, out var currency);
        if (errors.Count > 0) return Validation(errors);

        var sources = await ResolveSourcesAsync(db, request.Sources, ct);
        if (sources.Errors.Count > 0) return Validation(sources.Errors);

        entity.InformationType = type;
        entity.Amount = request.Amount!.Value;
        entity.Currency = currency;
        entity.Purpose = Normalize(request.Purpose) ?? "";
        entity.PeriodLabel = Normalize(request.PeriodLabel);
        entity.ContextNote = Normalize(request.ContextNote);

        // PATCH mengganti seluruh daftar sumber (lihat catatan di DTO). Dihitung sebagai diff,
        // bukan Clear()+Add(), supaya link yang dipertahankan tidak dihapus lalu disisipkan ulang
        // dengan primary key yang sama (bentrok tracking di provider relational).
        if (request.Sources is not null)
        {
            var desired = sources.Value.ToDictionary(l => l.SourceId);

            foreach (var existing in entity.Sources.ToList())
            {
                if (!desired.Remove(existing.SourceId, out var replacement))
                {
                    entity.Sources.Remove(existing);
                    continue;
                }

                existing.SupportingNote = replacement.SupportingNote;
            }

            foreach (var link in desired.Values)
                entity.Sources.Add(link);
        }

        await CaseStatusGuard.DemoteIfPublishedAsync(db, entity.CaseId, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(entity.ToDto());
    }

    private static async Task<IResult> DeleteFundingRecord(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.FundingRecords.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return NotFoundFundingRecord();

        db.FundingRecords.Remove(entity);
        await CaseStatusGuard.DemoteIfPublishedAsync(db, entity.CaseId, ct);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ---------------- Sumber ----------------

    private static async Task<IResult> GetSources(AppDbContext db, CancellationToken ct)
    {
        var sources = await db.Sources.AsNoTracking().OrderBy(s => s.Title).ToListAsync(ct);
        return Results.Ok(sources.Select(s => s.ToDto()).ToList());
    }

    private static async Task<IResult> CreateSource(CreateSourceRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateSourceFields(request.Title, request.Url, request.Publisher);
        if (errors.Count > 0) return Validation(errors);

        var entity = new Source
        {
            Title = request.Title!.Trim(),
            Url = request.Url!.Trim(),
            Publisher = request.Publisher!.Trim(),
            PublishedAt = request.PublishedAt,
            CheckedAt = request.CheckedAt,
            Note = Normalize(request.Note),
        };

        db.Sources.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/sources/{entity.Id}", entity.ToDto());
    }

    private static async Task<IResult> UpdateSource(Guid id, UpdateSourceRequest request, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Sources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFoundSource();

        var errors = ValidateSourceFields(request.Title, request.Url, request.Publisher);
        if (errors.Count > 0) return Validation(errors);

        entity.Title = request.Title!.Trim();
        entity.Url = request.Url!.Trim();
        entity.Publisher = request.Publisher!.Trim();
        entity.PublishedAt = request.PublishedAt;
        entity.CheckedAt = request.CheckedAt;
        entity.Note = Normalize(request.Note);

        await CaseStatusGuard.DemoteCasesUsingSourceAsync(db, id, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(entity.ToDto());
    }

    private static async Task<IResult> DeleteSource(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Sources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFoundSource();

        await CaseStatusGuard.DemoteCasesUsingSourceAsync(db, id, ct);
        db.Sources.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ---------------- Glosarium ----------------

    private static async Task<IResult> GetGlossary(AppDbContext db, CancellationToken ct)
    {
        var terms = await db.GlossaryTerms
            .AsNoTracking()
            .Include(t => t.Source)
            .OrderBy(t => t.Term)
            .ToListAsync(ct);

        return Results.Ok(terms.Select(t => t.ToDto()).ToList());
    }

    private static async Task<IResult> CreateGlossaryTerm(CreateGlossaryTermRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateGlossaryFields(request.Term, request.Definition);
        if (errors.Count > 0) return Validation(errors);

        if (request.SourceId is { } sourceId && !await db.Sources.AnyAsync(s => s.Id == sourceId, ct))
            return Fail(400, "unknown_source", "sourceId", "Sumber tidak ditemukan.");

        var term = request.Term!.Trim();
        if (await db.GlossaryTerms.AnyAsync(t => t.Term == term, ct))
            return Fail(400, "duplicate_term", "term", "Istilah sudah ada di glosarium.");

        var entity = new GlossaryTerm
        {
            Term = term,
            Definition = request.Definition!.Trim(),
            SourceId = request.SourceId,
        };

        db.GlossaryTerms.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/glossary/{entity.Id}", entity.ToDto());
    }

    private static async Task<IResult> UpdateGlossaryTerm(Guid id, UpdateGlossaryTermRequest request, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.GlossaryTerms.Include(t => t.Source).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFoundGlossary();

        var errors = ValidateGlossaryFields(request.Term, request.Definition);
        if (errors.Count > 0) return Validation(errors);

        if (request.SourceId is { } sourceId && !await db.Sources.AnyAsync(s => s.Id == sourceId, ct))
            return Fail(400, "unknown_source", "sourceId", "Sumber tidak ditemukan.");

        var term = request.Term!.Trim();
        if (await db.GlossaryTerms.AnyAsync(t => t.Term == term && t.Id != id, ct))
            return Fail(400, "duplicate_term", "term", "Istilah sudah ada di glosarium.");

        entity.Term = term;
        entity.Definition = request.Definition!.Trim();
        entity.SourceId = request.SourceId;

        await db.SaveChangesAsync(ct);
        return Results.Ok(entity.ToDto());
    }

    private static async Task<IResult> DeleteGlossaryTerm(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.GlossaryTerms.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return NotFoundGlossary();

        db.GlossaryTerms.Remove(entity);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ---------------- Validasi dan respons ----------------

    private static List<ApiError> ValidateCaseFields(string? title, string? slug, string? summary, out string normalizedSlug)
    {
        var errors = new List<ApiError>();
        normalizedSlug = (slug ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(title))
            errors.Add(new("missing_title", "title", "Judul kasus wajib diisi."));

        if (!SlugRules.IsValid(normalizedSlug))
            errors.Add(new("invalid_slug", "slug", "Slug wajib diisi dan hanya boleh berisi huruf kecil, angka, dan tanda hubung."));

        if (string.IsNullOrWhiteSpace(summary))
            errors.Add(new("missing_summary", "summary", "Ringkasan kasus wajib diisi."));

        return errors;
    }

    private static List<ApiError> ValidateFundingFields(
        string? informationType,
        long? amount,
        string? currency,
        out InformationType type,
        out string normalizedCurrency)
    {
        var errors = new List<ApiError>();
        normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? "IDR" : currency.Trim().ToUpperInvariant();

        if (!InformationTypeCodes.TryFromCode(informationType, out type))
            errors.Add(new("invalid_information_type", "informationType",
                "Jenis informasi harus salah satu dari: allocation, disbursement, realization_reported, planned."));

        if (amount is null)
            errors.Add(new("missing_amount", "amount", "Nominal wajib diisi."));
        else if (amount < 0)
            errors.Add(new("negative_amount", "amount", "Nominal tidak boleh negatif."));

        if (!AllowedCurrencies.Contains(normalizedCurrency))
            errors.Add(new("invalid_currency", "currency", $"Mata uang harus salah satu dari: {string.Join(", ", AllowedCurrencies)}."));

        return errors;
    }

    private static List<ApiError> ValidateSourceFields(string? title, string? url, string? publisher)
    {
        var errors = new List<ApiError>();

        if (string.IsNullOrWhiteSpace(title))
            errors.Add(new("missing_title", "title", "Judul sumber wajib diisi."));

        if (!UrlRules.IsValid(url))
            errors.Add(new("invalid_url", "url", "URL wajib berupa alamat http atau https yang valid."));

        if (string.IsNullOrWhiteSpace(publisher))
            errors.Add(new("missing_publisher", "publisher", "Penerbit wajib diisi."));

        return errors;
    }

    private static List<ApiError> ValidateGlossaryFields(string? term, string? definition)
    {
        var errors = new List<ApiError>();

        if (string.IsNullOrWhiteSpace(term))
            errors.Add(new("missing_term", "term", "Istilah wajib diisi."));

        if (string.IsNullOrWhiteSpace(definition))
            errors.Add(new("missing_definition", "definition", "Definisi wajib diisi."));

        return errors;
    }

    private sealed record ResolvedSources(List<RecordSource> Value, List<ApiError> Errors);

    private static async Task<ResolvedSources> ResolveSourcesAsync(
        AppDbContext db,
        IReadOnlyList<RecordSourceInput>? inputs,
        CancellationToken ct)
    {
        var errors = new List<ApiError>();
        var links = new List<RecordSource>();
        if (inputs is null or { Count: 0 }) return new(links, errors);

        var ids = inputs.Select(i => i.SourceId).Distinct().ToList();
        var existing = await db.Sources.Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (!existing.Contains(input.SourceId))
            {
                errors.Add(new("unknown_source", $"sources[{i}].sourceId", "Sumber tidak ditemukan."));
                continue;
            }

            if (links.Any(l => l.SourceId == input.SourceId))
            {
                errors.Add(new("duplicate_source", $"sources[{i}].sourceId", "Sumber yang sama dicantumkan lebih dari sekali."));
                continue;
            }

            links.Add(new RecordSource
            {
                SourceId = input.SourceId,
                SupportingNote = Normalize(input.SupportingNote),
            });
        }

        return new(links, errors);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult Validation(List<ApiError> errors) =>
        Results.Json(new ApiErrorResponse(errors), statusCode: StatusCodes.Status422UnprocessableEntity);

    private static IResult Fail(int statusCode, string code, string? field, string message) =>
        Results.Json(ApiErrorResponse.Single(code, field, message), statusCode: statusCode);

    private static IResult NotFoundCase() => Fail(404, "case_not_found", null, "Kasus tidak ditemukan.");
    private static IResult NotFoundFundingRecord() => Fail(404, "funding_record_not_found", null, "Catatan dana tidak ditemukan.");
    private static IResult NotFoundSource() => Fail(404, "source_not_found", null, "Sumber tidak ditemukan.");
    private static IResult NotFoundGlossary() => Fail(404, "glossary_term_not_found", null, "Istilah tidak ditemukan.");
}
