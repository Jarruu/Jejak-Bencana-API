namespace JejakBencana.Api.Domain;

/// <summary>Status publikasi sebuah kasus. Disimpan sebagai string huruf kecil di database.</summary>
public enum CaseStatus
{
    Draft,
    Published,
}

/// <summary>
/// Jenis informasi dana. Kode internal mengikuti PRD §9.1 dan tidak boleh diubah tanpa
/// menyelaraskan kontrak API (lihat <see cref="InformationTypeCodes"/>).
/// </summary>
public enum InformationType
{
    Allocation,
    Disbursement,
    RealizationReported,
    Planned,
}

public static class CaseStatusCodes
{
    public static string ToCode(this CaseStatus status) => status switch
    {
        CaseStatus.Draft => "draft",
        CaseStatus.Published => "published",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status kasus tidak dikenal."),
    };

    public static CaseStatus FromCode(string code) => code switch
    {
        "draft" => CaseStatus.Draft,
        "published" => CaseStatus.Published,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Kode status kasus tidak dikenal."),
    };
}

public static class InformationTypeCodes
{
    public static string ToCode(this InformationType type) => type switch
    {
        InformationType.Allocation => "allocation",
        InformationType.Disbursement => "disbursement",
        InformationType.RealizationReported => "realization_reported",
        InformationType.Planned => "planned",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Jenis informasi tidak dikenal."),
    };

    public static InformationType FromCode(string code) => code switch
    {
        "allocation" => InformationType.Allocation,
        "disbursement" => InformationType.Disbursement,
        "realization_reported" => InformationType.RealizationReported,
        "planned" => InformationType.Planned,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Kode jenis informasi tidak dikenal."),
    };

    public static bool TryFromCode(string? code, out InformationType type)
    {
        switch (code)
        {
            case "allocation": type = InformationType.Allocation; return true;
            case "disbursement": type = InformationType.Disbursement; return true;
            case "realization_reported": type = InformationType.RealizationReported; return true;
            case "planned": type = InformationType.Planned; return true;
            default: type = default; return false;
        }
    }
}
