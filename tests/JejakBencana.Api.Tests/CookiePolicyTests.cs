using System.Net.Http.Json;
using JejakBencana.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace JejakBencana.Api.Tests;

/// <summary>
/// Regresi: cookie sesi harus tetap terkirim setelah login. Di production flag Secure wajib
/// aktif, jadi perilaku cookie diuji terpisah dari lingkungan Development.
/// </summary>
public class CookiePolicyTests
{
    [Fact]
    public async Task Production_Cookie_Is_Marked_Secure_And_HttpOnly()
    {
        await using var factory = new ApiFactory().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            await users.CreateAsync(new AdminUser { UserName = "admin@example.com", Email = "admin@example.com" }, "kata-sandi-uji-panjang");
        }

        var response = await client.PostAsJsonAsync("/api/admin/login", new
        {
            email = "admin@example.com",
            password = "kata-sandi-uji-panjang",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("jejak_bencana_admin", setCookie);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
