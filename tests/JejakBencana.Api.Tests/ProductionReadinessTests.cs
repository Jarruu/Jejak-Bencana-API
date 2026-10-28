using JejakBencana.Api.Contracts;
using JejakBencana.Api.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace JejakBencana.Api.Tests;

/// <summary>
/// Regresi: input melebihi batas kolom harus ditolak 422, bukan gagal di SaveChanges (500).
/// </summary>
public class FieldLengthTests
{
    [Fact]
    public async Task Case_Summary_Over_Column_Length_Returns_422_Not_500()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/cases", new
        {
            title = "Judul",
            slug = "judul",
            summary = new string('a', FieldLimits.CaseSummary + 1),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "too_long" && e.Field == "summary");
    }

    [Fact]
    public async Task Source_Url_Over_Column_Length_Returns_422_Not_500()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/sources", new
        {
            title = "Sumber",
            url = "https://a.test/" + new string('a', FieldLimits.SourceUrl),
            publisher = "BNPB",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains(body!.Errors, e => e.Code == "too_long" && e.Field == "url");
    }

    [Fact]
    public async Task Value_Exactly_At_Limit_Is_Accepted()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/cases", new
        {
            title = "Judul",
            slug = "judul",
            summary = new string('a', FieldLimits.CaseSummary),
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

/// <summary>
/// Regresi: admin pertama harus bisa dibuat di production, karena tidak ada endpoint daftar user.
/// </summary>
public class AdminBootstrapTests
{
    [Fact]
    public async Task Production_Creates_First_Admin_From_Configured_Credentials()
    {
        await using var factory = new ApiFactory()
            .WithWebHostBuilder(b => b
                .UseEnvironment("Production")
                .UseSetting("ADMIN_EMAIL", "pertama@example.com")
                .UseSetting("ADMIN_PASSWORD", "kata-sandi-panjang-sekali"));

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/login", new
        {
            email = "pertama@example.com",
            password = "kata-sandi-panjang-sekali",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Production_Does_Not_Add_Admin_When_One_Already_Exists()
    {
        // Dua factory berbagi database yang sama = simulasi restart container dengan admin
        // yang sudah ada. Factory kedua tidak boleh membuat akun dari ADMIN_EMAIL.
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        const string database = "restart-container";

        await using (var first = new ApiFactory(database, root))
        {
            first.CreateClient();
            using var scope = first.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<JejakBencana.Api.Domain.AdminUser>>();
            await users.CreateAsync(
                new JejakBencana.Api.Domain.AdminUser { UserName = "asli@example.com", Email = "asli@example.com" },
                "kata-sandi-panjang-sekali");
        }

        await using var second = new ApiFactory(database, root)
            .WithWebHostBuilder(b => b
                .UseEnvironment("Production")
                .UseSetting("ADMIN_EMAIL", "kedua@example.com")
                .UseSetting("ADMIN_PASSWORD", "kata-sandi-panjang-sekali"));

        var response = await second.CreateClient().PostAsJsonAsync("/api/admin/login", new
        {
            email = "kedua@example.com",
            password = "kata-sandi-panjang-sekali",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
