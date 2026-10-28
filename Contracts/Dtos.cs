namespace JejakBencana.Api.Contracts;

/// <summary>Satu pelanggaran validasi. Bentuk body mengikuti usulan SPECS §8.</summary>
public sealed record ApiError(string Code, string? Field, string Message);

public sealed record ApiErrorResponse(IReadOnlyList<ApiError> Errors)
{
    public static ApiErrorResponse Single(string code, string? field, string message) => new([new ApiError(code, field, message)]);
}

// ---------- Publik ----------

public sealed record PublicSourceDto(
    Guid Id,
    string Title,
    string Url,
    string Publisher,
    DateOnly? PublishedAt,
    DateOnly? CheckedAt,
    string? Note,
    string? SupportingNote);

public sealed record PublicFundingRecordDto(
    Guid Id,
    string InformationType,
    long Amount,
    string Currency,
    string Purpose,
    string? PeriodLabel,
    string? ContextNote,
    IReadOnlyList<PublicSourceDto> Sources);

public sealed record PublicCaseListItemDto(
    string Slug,
    string Title,
    string Summary,
    string? Region,
    DateTimeOffset UpdatedAt);

public sealed record PublicCaseDetailDto(
    string Slug,
    string Title,
    string Summary,
    string? Region,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PublicFundingRecordDto> FundingRecords);

public sealed record PublicGlossaryTermDto(string Term, string Definition, PublicSourceDto? Source);

// ---------- Admin: respons ----------

public sealed record AdminSourceDto(
    Guid Id,
    string Title,
    string Url,
    string Publisher,
    DateOnly? PublishedAt,
    DateOnly? CheckedAt,
    string? Note);

public sealed record AdminRecordSourceDto(Guid SourceId, string? SupportingNote);

public sealed record AdminFundingRecordDto(
    Guid Id,
    Guid CaseId,
    string InformationType,
    long Amount,
    string Currency,
    string Purpose,
    string? PeriodLabel,
    string? ContextNote,
    IReadOnlyList<AdminRecordSourceDto> Sources);

public sealed record AdminCaseListItemDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string? Region,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int FundingRecordCount);

public sealed record AdminCaseDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string? Region,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminFundingRecordDto> FundingRecords);

public sealed record AdminGlossaryTermDto(
    Guid Id,
    string Term,
    string Definition,
    Guid? SourceId,
    AdminSourceDto? Source);

public sealed record LoginResponseDto(string Email);

// ---------- Admin: request ----------
// PATCH mengganti seluruh field yang dapat diedit (bukan partial update).
// Field wajib harus dikirim dan tidak boleh null. Field opsional: kirim null atau "" untuk mengosongkan.
// Pengecualian: `sources` pada catatan dana. null = daftar sumber tidak diubah, [] = kosongkan.

public sealed record CreateCaseRequest(
    string? Title,
    string? Slug,
    string? Summary,
    string? Region);

public sealed record UpdateCaseRequest(
    string? Title,
    string? Slug,
    string? Summary,
    string? Region);

public sealed record RecordSourceInput(Guid SourceId, string? SupportingNote);

public sealed record CreateFundingRecordRequest(
    Guid CaseId,
    string? InformationType,
    long? Amount,
    string? Currency,
    string? Purpose,
    string? PeriodLabel,
    string? ContextNote,
    IReadOnlyList<RecordSourceInput>? Sources);

public sealed record UpdateFundingRecordRequest(
    string? InformationType,
    long? Amount,
    string? Currency,
    string? Purpose,
    string? PeriodLabel,
    string? ContextNote,
    IReadOnlyList<RecordSourceInput>? Sources);

public sealed record CreateSourceRequest(
    string? Title,
    string? Url,
    string? Publisher,
    DateOnly? PublishedAt,
    DateOnly? CheckedAt,
    string? Note);

public sealed record UpdateSourceRequest(
    string? Title,
    string? Url,
    string? Publisher,
    DateOnly? PublishedAt,
    DateOnly? CheckedAt,
    string? Note);

public sealed record CreateGlossaryTermRequest(
    string? Term,
    string? Definition,
    Guid? SourceId);

public sealed record UpdateGlossaryTermRequest(
    string? Term,
    string? Definition,
    Guid? SourceId);

public sealed record LoginRequest(string? Email, string? Password);
