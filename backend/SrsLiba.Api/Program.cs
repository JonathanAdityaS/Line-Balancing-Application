// ============================================================
// Program.cs — Titik masuk (entry point) aplikasi ASP.NET Core.
// Tugas utama:
//   1. Registrasi semua service ke Dependency Injection container
//   2. Menjalankan CSV seeder saat startup (isi database awal)
//   3. Menyusun HTTP request pipeline (Swagger, CORS, routing)
// ============================================================

// Import EF Core, layer internal (Data/Repositories/Services), dan lisensi QuestPDF
using Microsoft.EntityFrameworkCore;
using SrsLiba.Api.Data;
using SrsLiba.Api.Repositories;
using SrsLiba.Api.Services;
using QuestPDF.Infrastructure;

// Membuat builder aplikasi web — membaca appsettings.json + environment variables
var builder = WebApplication.CreateBuilder(args);

// --- Registrasi layanan dasar ASP.NET Core ---
builder.Services.AddControllers();          // Deteksi & bind semua Controller (Kpi, Master, Export)
builder.Services.AddEndpointsApiExplorer(); // Metadata API untuk Swagger/OpenAPI
builder.Services.AddSwaggerGen();           // Generator dokumen Swagger UI (khusus Development)
builder.Services.AddMemoryCache();          // IMemoryCache in-process: cache master data & hasil dashboard
builder.Services.AddProblemDetails();       // Format error response standar RFC 7807 (JSON ProblemDetails)

// --- CORS: izinkan frontend Angular (dev server port 4200) memanggil API ini ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --- Health check: endpoint /health memeriksa koneksi database (pakai DbContext) ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

// --- Database: SQLite lokal untuk cache/data aplikasi (bukan database produksi) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AppDb")));

// --- Dependency Injection layer aplikasi ---
builder.Services.AddScoped<IProductionRepository, ProductionRepository>(); // Query database
builder.Services.AddScoped<IMasterDataService, MasterDataService>();       // Master lookup + cache 5 menit
builder.Services.AddScoped<IKpiService, KpiService>();                     // Orkestrasi KPI + cache 30 detik
builder.Services.AddScoped<IExportService, ExportService>();               // Export Excel/PDF
builder.Services.AddScoped<ISyncService, SyncService>();                   // Tarik MSSQL existing → SQLite (read-only)
// Binding konfigurasi "Kpi:TargetTaktSeconds" dari appsettings.json ke class KpiOptions
builder.Services.Configure<KpiOptions>(builder.Configuration.GetSection(KpiOptions.SectionName));

// QuestPDF mode Community (gratis) — wajib diset sebelum generate PDF
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// --- Sinkronisasi Database saat startup (sesuai SRS: MSSQL existing → SQLite cache) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    var sync = scope.ServiceProvider.GetRequiredService<ISyncService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Pastikan schema & tabel ada di SQLite
    await db.Database.EnsureCreatedAsync();

    // 1) Coba tarik dari MSSQL existing (read-only). Berhasil → data segar dari perusahaan.
    var synced = await sync.TrySyncFromMssqlAsync();

    // 2) Gagal → fallback seed dari CSV dummy (tetap jalan di clone/demo).
    if (!synced)
    {
        var csvFolder = Path.Combine(env.ContentRootPath, "Data", "DummyCsv");
        await CsvSeeder.SeedAsync(db, csvFolder);
        logger.LogInformation("Data bersumber dari CSV dummy (fallback).");
    }
}

// --- HTTP request pipeline (urutan penting) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();     // Endpoint JSON Swagger: /swagger/v1/swagger.json
    app.UseSwaggerUI();   // Halaman UI interaktif: /swagger
}

app.UseExceptionHandler(); // Error global → response ProblemDetails rapi (tanpa stack trace)
app.UseCors("Frontend");   // Terapkan kebijakan CORS di atas
app.UseHttpsRedirection(); // Redirect HTTP → HTTPS
app.MapControllers();      // Routing semua controller: /api/kpi, /api/master, /api/export
app.MapHealthChecks("/health"); // Endpoint cek kesehatan: /health

app.Run(); // Jalankan server (blocking sampai aplikasi dihentikan)
