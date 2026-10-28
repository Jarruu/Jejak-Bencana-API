namespace JejakBencana.Api.Validation;

/// <summary>
/// Batas panjang kolom database. Dipakai bersama oleh <c>AppDbContext</c> dan validasi input,
/// supaya nilai terlalu panjang ditolak sebagai 422 sebelum SaveChanges gagal (500).
/// </summary>
public static class FieldLimits
{
    public const int CaseTitle = 300;
    public const int CaseSlug = 200;
    public const int CaseSummary = 2000;
    public const int CaseRegion = 200;

    public const int FundingPurpose = 2000;
    public const int FundingPeriodLabel = 200;
    public const int FundingContextNote = 4000;

    public const int SourceTitle = 500;
    public const int SourceUrl = 2000;
    public const int SourcePublisher = 300;
    public const int SourceNote = 2000;

    public const int RecordSourceSupportingNote = 1000;

    public const int GlossaryTerm = 200;
    public const int GlossaryDefinition = 4000;
}
