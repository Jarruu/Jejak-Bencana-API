namespace JejakBencana.Api.Domain;

/// <summary>Satu kasus bencana yang dibahas. MVP hanya memakai satu kasus, model tetap mendukung banyak baris.</summary>
public class DisasterCase
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Region { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<FundingRecord> FundingRecords { get; set; } = [];
}

/// <summary>Catatan informasi dana. <see cref="Amount"/> selalu rupiah utuh (bukan format tampilan).</summary>
public class FundingRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CaseId { get; set; }
    public DisasterCase Case { get; set; } = null!;
    public InformationType InformationType { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; } = "IDR";
    public string Purpose { get; set; } = "";
    public string? PeriodLabel { get; set; }
    public string? ContextNote { get; set; }
    public List<RecordSource> Sources { get; set; } = [];
}

/// <summary>Sumber resmi atau dokumen pendukung.</summary>
public class Source
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Publisher { get; set; } = "";
    public DateOnly? PublishedAt { get; set; }
    public DateOnly? CheckedAt { get; set; }
    public string? Note { get; set; }
    public List<RecordSource> FundingRecords { get; set; } = [];
}

/// <summary>Relasi banyak-ke-banyak catatan dana dengan sumber.</summary>
public class RecordSource
{
    public Guid FundingRecordId { get; set; }
    public FundingRecord FundingRecord { get; set; } = null!;
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public string? SupportingNote { get; set; }
}

/// <summary>Istilah glosarium. <see cref="SourceId"/> opsional bila definisi belum bergantung pada dokumen tertentu.</summary>
public class GlossaryTerm
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Term { get; set; } = "";
    public string Definition { get; set; } = "";
    public Guid? SourceId { get; set; }
    public Source? Source { get; set; }
}
