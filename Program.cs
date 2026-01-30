using System.IO;
using System.Text;
using InventarioSilo.Data;
using InventarioSilo.Seeding;
using InventarioSilo.Settings;
using InventarioSilo.GraphQL.Queries;
using InventarioSilo.GraphQL.Mutations;
using InventarioSilo.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

LoadDotEnvFile(System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

builder.Services.Configure<AdminSeedSettings>(
    builder.Configuration.GetSection("AdminSeed"));

builder.Services.AddSingleton(sp =>
{
    var settings = builder.Configuration
        .GetSection("MongoDbSettings")
        .Get<MongoDbSettings>();

    return new MongoDbContext(settings!);
});

builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<PasswordHasher>();

var jwtSettings = builder.Configuration
    .GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings no configurado.");

var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://inventario-silo.onrender.com")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType(d => d.Name("Query"))
    .AddMutationType(d => d.Name("Mutation"))
    .AddAuthorization()
    .AddType<ItemQuery>()
    .AddType<CategoriaQuery>()
    .AddType<UbicacionQuery>()
    .AddType<EntregaQuery>()
    .AddType<RecepcionQuery>()
    .AddType<AuthQuery>()
    .AddType<ItemMutation>()
    .AddType<CategoriaMutation>()
    .AddType<UbicacionMutation>()
    .AddType<EntregaMutation>()
    .AddType<RecepcionMutation>()
    .AddType<AuthMutation>()
    .AddType<KardexQuery>()
    .AddType<ReporteQuery>()
    .ModifyRequestOptions(options =>
    {
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });

var app = builder.Build();

app.UseRouting();

// Global request logging placeholder; keep /health silent to avoid Render noise.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/health"))
    {
        // var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        // logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
    }

    await next();
});

app.UseCors("DashboardCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapMethods("/health", new[] { HttpMethods.Get, HttpMethods.Head },
        (HttpContext context /*, ILogger<Program> logger */) =>
        {
            // logger.LogInformation("Health check endpoint hit");
            var activeWindowStart = new TimeSpan(10, 43, 0);
            var activeWindowEnd = new TimeSpan(2, 17, 0);
            var utcTime = DateTime.UtcNow.TimeOfDay;

            // Active window crosses midnight, so combine >= start OR < end.
            var isActive = utcTime >= activeWindowStart || utcTime < activeWindowEnd;

            if (!isActive)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (HttpMethods.IsHead(context.Request.Method))
            {
                return Results.StatusCode(StatusCodes.Status200OK);
            }

            return Results.Ok("ok");
        })
    .AllowAnonymous();

app.MapGraphQL("/graphql").RequireCors("DashboardCors");

await AdminUserSeeder.EnsureAdminUserAsync(app.Services);

app.Run();

static void LoadDotEnvFile(string filePath)
{
    if (!File.Exists(filePath))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(filePath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex < 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');

        if (string.IsNullOrEmpty(key))
        {
            continue;
        }

        if (Environment.GetEnvironmentVariable(key) is not null)
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
