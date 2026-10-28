namespace JejakBencana.Api.Validation;

public static class UrlRules
{
    /// <summary>Sumber harus URL absolut http/https. Skema lain (mis. javascript:) ditolak.</summary>
    public static bool IsValid(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public static class SlugRules
{
    public const int MaxLength = FieldLimits.CaseSlug;

    /// <summary>Huruf kecil, angka, dan tanda hubung tunggal. Menjaga slug tetap aman di URL.</summary>
    public static bool IsValid(string? slug) =>
        !string.IsNullOrWhiteSpace(slug)
        && slug.Length <= MaxLength
        && slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
        && !slug.StartsWith('-')
        && !slug.EndsWith('-')
        && !slug.Contains("--");
}
