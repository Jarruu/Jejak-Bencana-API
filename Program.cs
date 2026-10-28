using System.Threading.RateLimiting;
using JejakBencana.Api.Data;
using JejakBencana.Api.Domain;
using JejakBencana.Api.Endpoints;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Default' belum diatur. Set environment variable ConnectionStrings__Default. " +
        "Lihat README.md bagian Konfigurasi.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    // Database terkelola memutus koneksi idle; retry mencegah 500 sesaat saat reconnect.
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

builder.Services
    .AddIdentityCore<AdminUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        // Pembatasan percobaan login (SPECS §15 no.8): 5 gagal -> terkunci 5 menit.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "jejak_bencana_admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Di production cookie hanya boleh dikirim lewat HTTPS; TLS diterminasi reverse proxy
    // sehingga request ke app sendiri selalu HTTP dan SameAsRequest akan melepas flag Secure.
    // Lingkungan lokal (Development/Testing) tetap HTTP agar cookie bisa dipakai tanpa TLS.
    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    // API harus menjawab 401/403, bukan mengalihkan ke halaman login HTML.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

// TLS diterminasi reverse proxy (lihat cookie SecurePolicy). Tanpa ini RemoteIpAddress
// selalu IP proxy, sehingga batas per IP di bawah berlaku untuk semua pengunjung sekaligus.
// KnownIPNetworks/KnownProxies dikosongkan karena proxy berada di jaringan privat container.
// shortcut: X-Forwarded-For dipercaya dari sumber mana pun, jadi rate limit bisa diakali
// dengan memalsukan header. Isi KnownProxies dengan rentang proxy bila hosting sudah pasti.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Lockout akun saja tidak cukup: penyerang bisa mengunci satu-satunya admin dengan 5 request.
// Batas per IP membuat percobaan berulang mahal tanpa mengunci akun.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
            }));
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Jejak Bencana API",
            Version = "v1",
            Description =
                "API informasi Dana Siap Pakai (DSP) untuk penanggulangan bencana.\n\n" +
                "Endpoint publik hanya mengembalikan kasus berstatus `published`. " +
                "Endpoint `/api/admin/*` memerlukan cookie sesi dari `POST /api/admin/login`, kecuali login itu sendiri.\n\n" +
                "Nominal disimpan dan dikirim sebagai bilangan bulat rupiah utuh (contoh: 537220000000), bukan format tampilan.",
        };

        // Kontrak perlu menyatakan bahwa endpoint admin memakai cookie sesi.
        // Keamanan dicatat di level skema; PRD §10 tetap acuan endpoint mana yang wajib sesi.
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["cookieAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "jejak_bencana_admin",
            Description = "Cookie sesi dari POST /api/admin/login. Wajib untuk semua /api/admin/* kecuali login.",
        };

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Dokumen kontrak selalu tersedia agar repo web bisa mengacu ke bentuk request/response.
app.MapOpenApi();

// UI Scalar hanya membaca dokumen OpenAPI yang sudah publik di semua environment;
// ia tidak menambah akses apa pun. Jadi dibiarkan tersedia juga di production.
app.MapScalarApiReference(options => options
    .WithTitle("Jejak Bencana API")
    .WithOpenApiRoutePattern("/openapi/{documentName}.json"));

app.UseExceptionHandler();
app.UseStatusCodePages();

// Harus sebelum UseRateLimiter agar IP asli sudah terbaca.
app.UseForwardedHeaders();

// Health check harus cepat: platform memanggilnya tiap beberapa detik dan memberi timeout
// pendek. Retry koneksi Npgsql bisa memakan puluhan detik, jadi dibatasi 5 detik di sini.
app.MapGet("/health", async (AppDbContext db) =>
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            return await db.Database.CanConnectAsync(timeout.Token)
                ? Results.Ok(new { status = "ok" })
                : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (OperationCanceledException)
        {
            return Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    })
    .WithTags("Sistem");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapPublicEndpoints();
app.MapAdminEndpoints();

// Admin pertama harus bisa dibuat di semua environment: tidak ada endpoint daftar user,
// jadi tanpa ini database production yang kosong tidak bisa dipakai sama sekali.
// Kegagalan di sini tidak menggagalkan startup: /health melaporkan 503 sampai database
// terjangkau, dan seeder dicoba lagi pada restart berikutnya.
try
{
    await SeedAsync(app);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Seeder gagal saat startup. Periksa koneksi database; /health akan melaporkan 503.");
}

app.Run();

static async Task SeedAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

    var db = services.GetRequiredService<AppDbContext>();

    // Migrasi otomatis hanya di Development. Di production migrasi dijalankan dari pipeline
    // (README bagian deploy) agar beberapa instance tidak balapan memigrasi database yang sama.
    if (db.Database.IsRelational())
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            if (app.Environment.IsDevelopment())
                await db.Database.MigrateAsync();
            else
                logger.LogError("Ada migrasi yang belum diterapkan. Jalankan `dotnet ef database update` sebelum deploy.");
        }
    }

    var userManager = services.GetRequiredService<UserManager<AdminUser>>();
    var email = app.Configuration["ADMIN_EMAIL"];
    var password = app.Configuration["ADMIN_PASSWORD"];

    // Hanya bila belum ada user sama sekali, supaya kredensial di environment tidak
    // menimpa/menambah akun setelah admin asli dibuat.
    if (!await userManager.Users.AnyAsync())
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogError("Belum ada akun admin dan ADMIN_EMAIL/ADMIN_PASSWORD kosong; panel admin tidak bisa dipakai.");
        }
        else
        {
            var result = await userManager.CreateAsync(new AdminUser { UserName = email, Email = email }, password);
            if (result.Succeeded)
                logger.LogInformation("Akun admin awal dibuat untuk {Email}.", email);
            else
                logger.LogError("Gagal membuat akun admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    // Data contoh hanya untuk pengembangan.
    if (!app.Environment.IsDevelopment())
        return;

    if (await db.Cases.AnyAsync())
        return;

    // Data contoh SPECS §13.2: berlabel jelas, amount 0, status Draft. Jangan diterbitkan.
    var now = DateTimeOffset.UtcNow;
    var sampleSource = new Source
    {
        Title = "DATA CONTOH — JANGAN DITERBITKAN",
        Url = "https://example.com/data-contoh",
        Publisher = "Data contoh lokal",
        CheckedAt = null,
        Note = "Placeholder pengembangan. Ganti dengan URL resmi yang sudah diperiksa sebelum dipakai.",
    };

    var sampleCase = new DisasterCase
    {
        Title = "Kasus Contoh — belum dipublikasikan",
        Slug = "kasus-contoh",
        Summary = "DATA CONTOH — JANGAN DITERBITKAN. Ringkasan ini hanya untuk pengembangan tampilan.",
        Status = CaseStatus.Draft,
        CreatedAt = now,
        UpdatedAt = now,
        FundingRecords =
        [
            new FundingRecord
            {
                InformationType = InformationType.RealizationReported,
                Amount = 0,
                Currency = "IDR",
                Purpose = "Isi setelah sumber diverifikasi",
                PeriodLabel = "Belum ditetapkan",
                ContextNote = "DATA CONTOH — JANGAN DITERBITKAN",
                Sources = [new RecordSource { Source = sampleSource, SupportingNote = "Placeholder" }],
            },
        ],
    };

    db.Cases.Add(sampleCase);
    db.GlossaryTerms.AddRange(
        new GlossaryTerm
        {
            Term = "DSP (Dana Siap Pakai)",
            Definition = "Dana untuk penanggulangan bencana yang menjadi fokus produk ini.",
        },
        new GlossaryTerm
        {
            Term = "Realisasi yang dilaporkan sumber",
            Definition = "Angka realisasi sebagaimana dilaporkan dokumen sumber, belum tentu penggunaan akhir oleh penerima.",
        });

    await db.SaveChangesAsync();
    logger.LogInformation("Data contoh draft dibuat.");
}
