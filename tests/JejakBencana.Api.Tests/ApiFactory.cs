using JejakBencana.Api.Data;
using JejakBencana.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JejakBencana.Api.Tests;

/// <summary>
/// Menjalankan host asli dengan database in-memory, supaya pipeline, autentikasi,
/// dan endpoint yang diuji sama dengan yang dipakai di runtime.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"jejak-bencana-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting berlaku sebelum Program.cs membaca konfigurasi; ConfigureAppConfiguration
        // pada factory minimal-hosting baru diterapkan saat Build().
        builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=test;Username=test;Password=test");

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning).AddConsole());

        builder.ConfigureServices(services =>
        {
            // Hapus DbContextOptions, AppDbContext, dan konfigurasi provider (Npgsql) milik aplikasi.
            // Tanpa langkah terakhir, Npgsql dan InMemory sama-sama terdaftar dan host gagal start.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(AppDbContext)
                    || (d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email = "admin@example.com", string password = "kata-sandi-uji-panjang")
    {
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                var result = await users.CreateAsync(new AdminUser { UserName = email, Email = email }, password);
                Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var response = await client.PostAsJsonAsync("/api/admin/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }
}
