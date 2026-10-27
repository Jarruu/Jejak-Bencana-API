using JejakBencana.Api.Data;
using JejakBencana.Api.Domain;
using JejakBencana.Api.Endpoints;
using Microsoft.AspNetCore.Identity;
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

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options => options
        .WithTitle("Jejak Bencana API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Sistem");

app.UseAuthentication();
app.UseAuthorization();

app.MapPublicEndpoints();
app.MapAdminEndpoints();

if (app.Environment.IsDevelopment())
{
    await SeedAsync(app);
}

app.Run();

static async Task SeedAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

    var db = services.GetRequiredService<AppDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        await db.Database.MigrateAsync();

    var userManager = services.GetRequiredService<UserManager<AdminUser>>();
    var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
    var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning("ADMIN_EMAIL/ADMIN_PASSWORD belum diatur; akun admin tidak dibuat.");
    }
    else if (await userManager.FindByEmailAsync(email) is null)
    {
        var result = await userManager.CreateAsync(new AdminUser { UserName = email, Email = email }, password);
        if (result.Succeeded)
            logger.LogInformation("Akun admin awal dibuat untuk {Email}.", email);
        else
            logger.LogError("Gagal membuat akun admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
    }

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
