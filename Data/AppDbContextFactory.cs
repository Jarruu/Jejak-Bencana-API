using JejakBencana.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JejakBencana.Api;

/// <summary>
/// Dipakai `dotnet ef` agar migrasi bisa dibuat tanpa menjalankan seluruh host.
/// Membaca connection string dari environment variable yang sama dengan runtime.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Tanpa fallback: connection string yang salah ketik atau lupa dimuat harus gagal keras,
        // bukan diam-diam memigrasi database lokal yang kebetulan cocok dengan kredensial default.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Default belum diatur. `dotnet ef` tidak memakai fallback agar " +
                "migrasi tidak masuk ke database yang salah. Lihat README.md bagian Konfigurasi.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
