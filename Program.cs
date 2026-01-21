using System.IO;
using System.Text;
using InventarioSilo.Data;
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

builder.Services.AddSingleton(sp =>
{
    var settings = builder.Configuration
        .GetSection("MongoDbSettings")
        .Get<MongoDbSettings>();

    return new MongoDbContext(settings!);
});

builder.Services.AddSingleton<JwtService>();

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
            "http://localhost:5174",
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
    .AddType<EntregaQuery>()
    .AddType<RecepcionQuery>()
    .AddType<AuthQuery>()
    .AddType<ItemMutation>()
    .AddType<EntregaMutation>()
    .AddType<RecepcionMutation>()
    .AddType<AuthMutation>()
    .AddType<KardexQuery>()
    .AddType<ReporteQuery>();

var app = builder.Build();

app.UseRouting();

app.UseCors("DashboardCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL("/graphql").RequireCors("DashboardCors");

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
