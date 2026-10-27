using System.Net.Http.Json;
using JejakBencana.Api.Contracts;

namespace JejakBencana.Api.Tests;

public class FundingRecordTests
{
    private static async Task<(HttpClient Client, Guid CaseId, Guid SourceA, Guid SourceB)> SetupAsync(ApiFactory factory)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        async Task<Guid> CreateSource(string title)
        {
            var response = await client.PostAsJsonAsync("/api/admin/sources", new
            {
                title,
                url = "https://www.bnpb.go.id/berita/" + title.Replace(' ', '-'),
                publisher = "BNPB",
                checkedAt = "2026-04-06",
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<AdminSourceDto>())!.Id;
        }

        var a = await CreateSource("Sumber A");
        var b = await CreateSource("Sumber B");

        var caseResponse = await client.PostAsJsonAsync("/api/admin/cases", new
        {
            title = "Kasus uji",
            slug = "kasus-uji",
            summary = "Ringkasan.",
        });
        var created = await caseResponse.Content.ReadFromJsonAsync<AdminCaseDetailDto>();

        return (client, created!.Id, a, b);
    }

    [Fact]
    public async Task Patching_Sources_Replaces_The_Whole_List()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, b) = await SetupAsync(factory);

        var create = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });
        var record = await create.Content.ReadFromJsonAsync<AdminFundingRecordDto>();

        var patch = await client.PatchAsJsonAsync($"/api/admin/funding-records/{record!.Id}", new
        {
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = b, supportingNote = "Tabel B" } },
        });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<AdminFundingRecordDto>();
        var link = Assert.Single(updated!.Sources);
        Assert.Equal(b, link.SourceId);
        Assert.Equal("Tabel B", link.SupportingNote);
    }

    [Fact]
    public async Task Patching_With_The_Same_Source_Does_Not_Conflict()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var create = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });
        var record = await create.Content.ReadFromJsonAsync<AdminFundingRecordDto>();

        var patch = await client.PatchAsJsonAsync($"/api/admin/funding-records/{record!.Id}", new
        {
            informationType = "realization_reported",
            amount = 2_000_000L,
            purpose = "Bantuan diperbarui",
            sources = new[] { new { sourceId = a, supportingNote = "Catatan baru" } },
        });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<AdminFundingRecordDto>();
        Assert.Equal(2_000_000L, updated!.Amount);
        Assert.Equal("Catatan baru", Assert.Single(updated.Sources).SupportingNote);
    }

    [Fact]
    public async Task Patching_Without_Sources_Field_Keeps_Existing_Sources()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var create = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });
        var record = await create.Content.ReadFromJsonAsync<AdminFundingRecordDto>();

        // Hanya memperbaiki nominal; daftar sumber tidak dikirim, jadi tidak boleh hilang.
        var patch = await client.PatchAsJsonAsync($"/api/admin/funding-records/{record!.Id}", new
        {
            informationType = "realization_reported",
            amount = 5_000_000L,
            purpose = "Bantuan",
        });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<AdminFundingRecordDto>();
        Assert.Equal(5_000_000L, updated!.Amount);
        Assert.Equal(a, Assert.Single(updated.Sources).SourceId);
    }

    [Fact]
    public async Task Patching_With_Empty_Sources_Array_Clears_Them()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var create = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });
        var record = await create.Content.ReadFromJsonAsync<AdminFundingRecordDto>();

        var patch = await client.PatchAsJsonAsync($"/api/admin/funding-records/{record!.Id}", new
        {
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<AdminFundingRecordDto>();
        Assert.Empty(updated!.Sources);
    }

    [Fact]
    public async Task Duplicate_Source_In_Payload_Is_Rejected()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a }, new { sourceId = a } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "duplicate_source");
    }

    [Fact]
    public async Task Unknown_Source_Is_Rejected()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, _, _) = await SetupAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = Guid.NewGuid() } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "unknown_source");
    }

    [Fact]
    public async Task Unknown_Information_Type_Is_Rejected()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realisasi",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "invalid_information_type");
    }

    [Fact]
    public async Task Editing_A_Record_Of_A_Published_Case_Demotes_The_Case()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var create = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });
        var record = await create.Content.ReadFromJsonAsync<AdminFundingRecordDto>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/cases/{caseId}/publish", null)).StatusCode);

        var patch = await client.PatchAsJsonAsync($"/api/admin/funding-records/{record!.Id}", new
        {
            informationType = "realization_reported",
            amount = 3_000_000L,
            purpose = "Bantuan revisi",
            sources = new[] { new { sourceId = a } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var adminCase = await client.GetFromJsonAsync<AdminCaseDetailDto>($"/api/admin/cases/{caseId}");
        Assert.Equal("draft", adminCase!.Status);
    }

    [Fact]
    public async Task Deleting_A_Record_Of_A_Published_Case_Demotes_The_Case()
    {
        await using var factory = new ApiFactory();
        var (client, caseId, a, _) = await SetupAsync(factory);

        var create = await client.PostAsJsonAsync("/api/admin/funding-records", new
        {
            caseId,
            informationType = "realization_reported",
            amount = 1_000_000L,
            purpose = "Bantuan",
            sources = new[] { new { sourceId = a } },
        });
        var record = await create.Content.ReadFromJsonAsync<AdminFundingRecordDto>();
        await client.PostAsync($"/api/admin/cases/{caseId}/publish", null);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/admin/funding-records/{record!.Id}")).StatusCode);

        var adminCase = await client.GetFromJsonAsync<AdminCaseDetailDto>($"/api/admin/cases/{caseId}");
        Assert.Equal("draft", adminCase!.Status);
    }
}
