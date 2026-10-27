using JejakBencana.Api.Contracts;
using JejakBencana.Api.Domain;

namespace JejakBencana.Api.Validation;

/// <summary>
/// Aturan publikasi SPECS §6 / PRD §7.1. Fungsi murni atas entitas yang sudah dimuat,
/// supaya bisa diuji tanpa database.
/// </summary>
public static class PublicationValidator
{
    public static List<ApiError> Validate(DisasterCase disasterCase)
    {
        var errors = new List<ApiError>();

        if (string.IsNullOrWhiteSpace(disasterCase.Title))
            errors.Add(new("missing_title", "title", "Judul kasus wajib diisi."));

        if (string.IsNullOrWhiteSpace(disasterCase.Slug))
            errors.Add(new("missing_slug", "slug", "Slug kasus wajib diisi."));

        if (string.IsNullOrWhiteSpace(disasterCase.Summary))
            errors.Add(new("missing_summary", "summary", "Ringkasan kasus wajib diisi."));

        if (disasterCase.FundingRecords.Count == 0)
        {
            errors.Add(new("no_funding_records", "fundingRecords", "Kasus belum memiliki catatan dana."));
            return errors;
        }

        for (var i = 0; i < disasterCase.FundingRecords.Count; i++)
        {
            var record = disasterCase.FundingRecords[i];
            var path = $"fundingRecords[{i}]";

            // Enum tidak bisa berisi nilai di luar daftar, tetapi data lama/konversi manual bisa.
            if (!Enum.IsDefined(record.InformationType))
                errors.Add(new("invalid_information_type", $"{path}.informationType", "Jenis informasi dana tidak dikenal."));

            if (record.Amount <= 0)
                errors.Add(new("amount_not_positive", $"{path}.amount", "Nominal harus bilangan bulat lebih besar dari nol."));

            if (string.IsNullOrWhiteSpace(record.Purpose))
                errors.Add(new("missing_purpose", $"{path}.purpose", "Tujuan penggunaan dana wajib diisi."));

            if (record.Sources.Count == 0)
                errors.Add(new("missing_source", $"{path}.sources", "Catatan dana belum memiliki sumber."));

            for (var j = 0; j < record.Sources.Count; j++)
            {
                var source = record.Sources[j].Source;
                var sourcePath = $"{path}.sources[{j}]";

                if (source is null || !UrlRules.IsValid(source.Url))
                    errors.Add(new("invalid_source_url", $"{sourcePath}.url", "URL sumber tidak valid."));

                if (source is null || source.CheckedAt is null)
                    errors.Add(new("missing_checked_at", $"{sourcePath}.checkedAt", "Tanggal pemeriksaan sumber wajib diisi."));
            }
        }

        return errors;
    }
}
