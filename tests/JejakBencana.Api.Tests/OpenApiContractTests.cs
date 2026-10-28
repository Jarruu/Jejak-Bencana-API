using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace JejakBencana.Api.Tests;

/// <summary>Kontrak API yang diacu repo web: semua endpoint terdokumentasi dan skema cookie terdaftar.</summary>
public class OpenApiContractTests
{
    [Fact]
    public async Task Document_Contains_All_Endpoints_And_Cookie_Scheme()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var paths = root.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        foreach (var expected in new[]
                 {
                     "/api/cases", "/api/cases/{slug}", "/api/glossary",
                     "/api/admin/login", "/api/admin/logout",
                     "/api/admin/cases", "/api/admin/cases/{id}", "/api/admin/cases/{id}/publish",
                     "/api/admin/funding-records", "/api/admin/funding-records/{id}",
                     "/api/admin/sources", "/api/admin/sources/{id}",
                     "/api/admin/glossary", "/api/admin/glossary/{id}",
                 })
        {
            Assert.Contains(expected, paths);
        }

        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("cookieAuth", out _));
    }

    [Fact]
    public async Task Scalar_Ui_Is_Served_In_Production()
    {
        await using var factory = new ApiFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
