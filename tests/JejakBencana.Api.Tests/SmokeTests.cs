using System.Net;
using System.Net.Http.Json;

namespace JejakBencana.Api.Tests;

public class ApiFactoryTests
{
    [Fact]
    public async Task Health_IsReachable()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Client_Can_Read_Public_Endpoints()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var cases = await client.GetAsync("/api/cases");
        Assert.True(cases.IsSuccessStatusCode, await cases.Content.ReadAsStringAsync());

        var glossary = await client.GetAsync("/api/glossary");
        Assert.True(glossary.IsSuccessStatusCode, await glossary.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_Endpoints_Reject_Anonymous_With_401()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var responses = new[]
        {
            await client.GetAsync("/api/admin/cases"),
            await client.PostAsJsonAsync("/api/admin/cases", new { title = "x", slug = "x", summary = "x" }),
            await client.PostAsJsonAsync("/api/admin/sources", new { title = "x", url = "https://a.test", publisher = "p" }),
            await client.PostAsJsonAsync("/api/admin/glossary", new { term = "x", definition = "x" }),
            await client.PostAsync($"/api/admin/cases/{Guid.NewGuid()}/publish", null),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }
}
