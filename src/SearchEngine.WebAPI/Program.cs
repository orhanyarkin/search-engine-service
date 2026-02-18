using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SearchEngine.Application;
using SearchEngine.Infrastructure;
using SearchEngine.Infrastructure.Configuration;
using SearchEngine.Infrastructure.Data;
using SearchEngine.Infrastructure.Services;
using SearchEngine.WebAPI.Middleware;
using SearchEngine.WebAPI.OpenApi;
using Serilog;

// Serilog yapılandırması — CorrelationId enrichment ile
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Search Engine API...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Servisleri kaydet
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });

    // JWT kimlik doğrulama
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
    var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
        ?? new JwtSettings();

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();

    // API Versiyonlama
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Dashboard için CORS — development ortamında localhost ile sınırlı
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                      "http://localhost:8080",
                      "https://localhost:8443")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Hiz sinirlandirma
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global sabit pencere: dakikada 100 istek
        options.AddFixedWindowLimiter("global", opt =>
        {
            opt.PermitLimit = 100;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });

        // Arama endpoint'i: dakikada 30 istek (kayan pencere)
        options.AddSlidingWindowLimiter("search", opt =>
        {
            opt.PermitLimit = 30;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.SegmentsPerWindow = 6;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });

        // Auth endpoint'i: dakikada 5 istek (brute-force koruması)
        options.AddFixedWindowLimiter("auth", opt =>
        {
            opt.PermitLimit = 5;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            Log.Warning("Rate limit exceeded for {Path}", context.HttpContext.Request.Path);
            await Task.CompletedTask;
        };
    });

    // Saglik kontrolleri
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgresql")
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis")
        .AddElasticsearch(builder.Configuration["ElasticsearchSettings:Url"] ?? "http://localhost:9200", name: "elasticsearch");

    var app = builder.Build();

    // Middleware pipeline — CorrelationId önce, ExceptionHandling sonra
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Güvenlik header'ları — clickjacking, MIME sniffing ve referrer sızıntılarını önler
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        await next();
    });

    app.UseRateLimiter();
    app.UseCors();
    app.UseStaticFiles();

    // Kimlik doğrulama ve yetkilendirme
    app.UseAuthentication();
    app.UseAuthorization();

    // OpenAPI ve Scalar API Referansi (JWT Bearer desteği ile)
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Search Engine API")
               .WithTheme(ScalarTheme.BluePlanet)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    app.MapControllers();

    // Saglik kontrolu endpoint'i
    app.MapHealthChecks("/health");

    // Kok yolu dashboard'a yonlendir
    app.MapGet("/", () => Results.Redirect("/index.html"));

    // Veritabanı migration ve başlangıç sync (test ortamında atlanır)
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Veritabanı migration tamamlandı.");

        var syncService = scope.ServiceProvider.GetRequiredService<ProviderSyncService>();
        await syncService.SyncAllAsync();
        Log.Information("Başlangıç sağlayıcı sync tamamlandı.");
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uygulama beklenmedik sekilde sonlandi.");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Entegrasyon testlerinde WebApplicationFactory<Program> icin gerekli
public partial class Program;
