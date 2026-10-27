using JejakBencana.Api.Domain;
using JejakBencana.Api.Validation;

namespace JejakBencana.Api.Tests;

/// <summary>Aturan publikasi SPECS §6. Setiap test menutup satu syarat yang bisa gagal.</summary>
public class PublicationValidatorTests
{
    private static Source ValidSource() => new()
    {
        Title = "Publikasi resmi",
        Url = "https://www.bnpb.go.id/berita/contoh",
        Publisher = "BNPB",
        CheckedAt = new DateOnly(2026, 4, 6),
    };

    private static FundingRecord ValidRecord(Source? source = null) => new()
    {
        InformationType = InformationType.RealizationReported,
        Amount = 537_220_000_000,
        Currency = "IDR",
        Purpose = "Bantuan stimulan rumah rusak",
        Sources = [new RecordSource { Source = source ?? ValidSource() }],
    };

    private static DisasterCase ValidCase() => new()
    {
        Title = "Pemulihan pascabencana",
        Slug = "pemulihan-pascabencana",
        Summary = "Ringkasan singkat.",
        FundingRecords = [ValidRecord()],
    };

    [Fact]
    public void Valid_Case_Has_No_Errors()
    {
        Assert.Empty(PublicationValidator.Validate(ValidCase()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Case_Fields_Are_Rejected(string blank)
    {
        var c = ValidCase();
        c.Title = blank;
        c.Summary = blank;
        c.Slug = blank;

        var codes = PublicationValidator.Validate(c).Select(e => e.Code).ToList();

        Assert.Contains("missing_title", codes);
        Assert.Contains("missing_summary", codes);
        Assert.Contains("missing_slug", codes);
    }

    [Fact]
    public void Zero_Amount_Is_Rejected()
    {
        var c = ValidCase();
        c.FundingRecords[0].Amount = 0;

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "amount_not_positive");
    }

    [Fact]
    public void Negative_Amount_Is_Rejected()
    {
        var c = ValidCase();
        c.FundingRecords[0].Amount = -1;

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "amount_not_positive");
    }

    [Fact]
    public void Record_Without_Source_Is_Rejected()
    {
        var c = ValidCase();
        c.FundingRecords[0].Sources.Clear();

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "missing_source");
    }

    [Fact]
    public void Blank_Purpose_Is_Rejected()
    {
        var c = ValidCase();
        c.FundingRecords[0].Purpose = "  ";

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "missing_purpose");
    }

    [Theory]
    [InlineData("bukan-url")]
    [InlineData("ftp://contoh.test/dokumen")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void Invalid_Source_Url_Is_Rejected(string url)
    {
        var source = ValidSource();
        source.Url = url;
        var c = ValidCase();
        c.FundingRecords[0].Sources = [new RecordSource { Source = source }];

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "invalid_source_url");
    }

    [Fact]
    public void Source_Without_CheckedAt_Is_Rejected()
    {
        var source = ValidSource();
        source.CheckedAt = null;
        var c = ValidCase();
        c.FundingRecords[0].Sources = [new RecordSource { Source = source }];

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "missing_checked_at");
    }

    [Fact]
    public void Case_Without_Funding_Records_Is_Rejected()
    {
        var c = ValidCase();
        c.FundingRecords.Clear();

        Assert.Contains(PublicationValidator.Validate(c), e => e.Code == "no_funding_records");
    }

    [Fact]
    public void Errors_Point_At_The_Offending_Field()
    {
        var c = ValidCase();
        c.FundingRecords[0].Amount = 0;
        c.FundingRecords.Add(ValidRecord());
        c.FundingRecords[1].Purpose = "";

        var errors = PublicationValidator.Validate(c);

        Assert.Contains(errors, e => e.Field == "fundingRecords[0].amount");
        Assert.Contains(errors, e => e.Field == "fundingRecords[1].purpose");
    }

    [Fact]
    public void Long_Amount_Above_Int_Max_Is_Accepted()
    {
        // 537,22 miliar melampaui batas int (2,1 miliar); memastikan model memakai long.
        var c = ValidCase();
        c.FundingRecords[0].Amount = 537_220_000_000;

        Assert.Empty(PublicationValidator.Validate(c));
    }
}
