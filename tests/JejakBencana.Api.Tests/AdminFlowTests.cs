using System.Net;
using System.Net.Http.Json;
using JejakBencana.Api.Contracts;
using JejakBencana.Api.Domain;

namespace JejakBencana.Api.Tests;

/// <summary>AC01, AC03, AC04, AC05, AC10 dari SPECS §11.</summary>
public class AdminFlowTests
{
    private static async Task<(HttpClient Client, Guid CaseId, Guid SourceId)> SetupCaseWithSourceAsync(ApiFactory factory)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var sourceResponse = await client.PostAsJsonAsync("/api/admin/sources", new
        {
            title = "Publikasi BNPB",
            url = "https://www.bnpb.go.id/berita/contoh",
            publisher = "BNPB",
            checkedAt = "2026-04-06",
        });
        Assert.Equal(HttpStatusCode.Created, sourceResponse.StatusCode);
        var source = await sourceResponse.Content.ReadFromJsonAsync<AdminSourceDto>();

        var caseResponse = await client.PostAsJsonAsync("/api/admin/cases", new
        {
            title = "Pemulihan pascabencana",
            slug = "pemulihan-pascabencana",
            summary = "Ringkasan singkat.",
        });
        Assert.Equal(HttpStatusCode.Created, caseResponse.StatusCode);
        var created = await caseResponse.Content.ReadFromJsonAsync<AdminCaseDetailDto>();

        return (client, created!.Id, source!.Id);
    }

    [Fact]
    public async Task New_Case_Starts_As_Draft_And_Is_Hidden_From_Public()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, _) = await SetupCaseWithSourceAsync(factory);

        var adminCase = await client.GetFromJsonAsync<AdminCaseDetailDto>($"/api/admin/cases/{caseId}");
        Assert.Equal("draft", adminCase!.Status);

        // AC03: draft tidak muncul di endpoint publik.
        var publicList = await client.GetFromJsonAsync<List<PublicCaseListItemDto>>("/api/cases");
        Assert.Empty(publicList!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/cases/pemulihan-pascabencana")).StatusCode);
    }

    [Fact]
    public async Task Publish_Without_Funding_Record_Returns_422()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, _) = await SetupCaseWithSourceAsync(factory);

        var response = await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "no_funding_records");
    }

    [Fact]
    public async Task Publish_With_Zero_Amount_Returns_422_And_Keeps_Draft()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, sourceId) = await SetupCaseWithSourceAsync(factory);

        var recordResponse = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 0,
            purpose = "Bantuan stimulan",
            sources = new[] { new { sourceId } },
        });
        Assert.Equal(HttpStatusCode.Created, recordResponse.StatusCode);

        var response = await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "amount_not_positive");

        var adminCase = await client.GetFromJsonAsync<AdminCaseDetailDto>($"/api/admin/cases/{caseId}");
        Assert.Equal("draft", adminCase!.Status);
    }

    [Fact]
    public async Task Publish_With_Record_Without_Source_Returns_422()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, _) = await SetupCaseWithSourceAsync(factory);

        await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 537_220_000_000L,
            purpose = "Bantuan stimulan",
        });

        var response = await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "missing_source");
    }

    [Fact]
    public async Task Complete_Case_Can_Be_Published_And_Read_Publicly()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, sourceId) = await SetupCaseWithSourceAsync(factory);

        await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 537_220_000_000L,
            purpose = "Bantuan stimulan rumah rusak",
            periodLabel = "hingga 6 April 2026",
            sources = new[] { new { sourceId, supportingNote = "Tabel realisasi" } },
        });

        var publish = await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        // AC01 + AC02: pengunjung anonim membaca detail lengkap dengan sumber.
        var anonymous = factory.CreateClient();
        var detail = await anonymous.GetFromJsonAsync<PublicCaseDetailDto>("/api/cases/pemulihan-pascabencana");

        Assert.NotNull(detail);
        var record = Assert.Single(detail!.FundingRecords);
        Assert.Equal("realization_reported", record.InformationType);
        Assert.Equal(537_220_000_000L, record.Amount);
        Assert.Equal("BNPB", Assert.Single(record.Sources).Publisher);
        Assert.Equal(new DateOnly(2026, 4, 6), record.Sources[0].CheckedAt);
    }

    [Fact]
    public async Task Editing_A_Published_Case_Returns_It_To_Draft()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, sourceId) = await SetupCaseWithSourceAsync(factory);

        await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId } },
        });
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/cases/{caseId}/publish", null)).StatusCode);

        var patch = await client.PatchAsJsonAsync($"/api/admin/cases/{caseId}", new
        {
            title = "Judul diperbarui",
            slug = "pemulihan-pascabencana",
            summary = "Ringkasan diperbarui.",
        });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<AdminCaseDetailDto>();
        Assert.Equal("draft", updated!.Status);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/cases/pemulihan-pascabencana")).StatusCode);
    }

    [Fact]
    public async Task Editing_A_Source_Used_By_A_Published_Case_Demotes_That_Case()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, sourceId) = await SetupCaseWithSourceAsync(factory);

        await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId } },
        });
        await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);

        var patch = await client.PatchAsJsonAsync($"/api/admin/sources/{sourceId}", new
        {
            title = "Publikasi BNPB (revisi)",
            url = "https://www.bnpb.go.id/berita/revisi",
            publisher = "BNPB",
            checkedAt = "2026-04-07",
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var adminCase = await client.GetFromJsonAsync<AdminCaseDetailDto>($"/api/admin/cases/{caseId}");
        Assert.Equal("draft", adminCase!.Status);
    }

    [Fact]
    public async Task Planned_Record_Is_Not_Mixed_Into_Realization()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, sourceId) = await SetupCaseWithSourceAsync(factory);

        await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 500_000_000L,
            purpose = "Realisasi",
            sources = new[] { new { sourceId } },
        });
        await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "planned",
            amount = 900_000_000L,
            purpose = "Rencana",
            sources = new[] { new { sourceId } },
        });
        await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);

        var detail = await factory.CreateClient().GetFromJsonAsync<PublicCaseDetailDto>("/api/cases/pemulihan-pascabencana");

        // AC06: setiap jenis tetap terpisah; tidak ada angka gabungan di kontrak publik.
        Assert.Equal(2, detail!.FundingRecords.Count);
        Assert.Equal(500_000_000L, detail.FundingRecords.Single(r => r.InformationType == "realization_reported").Amount);
        Assert.Equal(900_000_000L, detail.FundingRecords.Single(r => r.InformationType == "planned").Amount);
    }

    [Fact]
    public async Task Duplicate_Slug_Is_Rejected()
    {
        await using var factory = new ApiFactory();
        var (client, _, _) = await SetupCaseWithSourceAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/cases", new
        {
            title = "Kasus lain",
            slug = "pemulihan-pascabencana",
            summary = "Ringkasan.",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "duplicate_slug");
    }

    [Fact]
    public async Task Invalid_Source_Url_Is_Rejected_On_Create()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/sources", new
        {
            title = "Sumber",
            url = "bukan-url",
            publisher = "BNPB",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "invalid_url");
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_Generic_401()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.PostAsync("/api/admin/logout", null);

        var response = await client.PostAsJsonAsync("/api/admin/login", new
        {
            email = "admin@example.com",
            password = "salah-total",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email tidak", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Repeated_Failed_Logins_Lock_The_Account()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.PostAsync("/api/admin/logout", null);

        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/api/admin/login", new
            {
                email = "admin@example.com",
                password = "salah-total",
            });
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Logout_Ends_The_Session()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/cases")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/admin/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/cases")).StatusCode);
    }

    [Fact]
    public async Task Glossary_Is_Managed_And_Publicly_Readable()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/admin/glossary", new
        {
            term = "DSP",
            definition = "Dana Siap Pakai.",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var term = await create.Content.ReadFromJsonAsync<AdminGlossaryTermDto>();

        var duplicate = await client.PostAsJsonAsync("/api/admin/glossary", new { term = "DSP", definition = "Lain." });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var publicTerms = await factory.CreateClient().GetFromJsonAsync<List<PublicGlossaryTermDto>>("/api/glossary");
        Assert.Contains(publicTerms!, t => t.Term == "DSP");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/admin/glossary/{term!.Id}")).StatusCode);
        publicTerms = await factory.CreateClient().GetFromJsonAsync<List<PublicGlossaryTermDto>>("/api/glossary");
        Assert.DoesNotContain(publicTerms!, t => t.Term == "DSP");
    }
}
