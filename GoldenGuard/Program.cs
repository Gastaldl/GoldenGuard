using System.Text;
using GoldenGuard.Application.Services;
using GoldenGuard.Data;
using GoldenGuard.Endpoints;
using GoldenGuard.Infrastructure.Repositories;
using GoldenGuard.WebApi.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ========== EF Core (Oracle) ==========
builder.Services.AddDbContext<GgDbContext>(opt =>
    opt.UseOracle(cfg.GetConnectionString("Oracle")));

// ========== DI ==========
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddSingleton<FileStorageService>();

// ========== CORS (WebApp -> API) ==========
var allowedOrigins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                     new[] { "https://localhost:7275", "https://goldenguard-web.azurewebsites.net" };

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("WebApp", p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// ========== Swagger ==========
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "GoldenGuard API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Digite: Bearer {seu token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ========== JWT ==========
var jwtSection = cfg.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key não configurado.");

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("admin"));
});

builder.Services.AddProblemDetails();

builder.Services.AddHttpClient("rates", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Integrations:ExchangeRateBaseUrl"]
                            ?? "https://api.exchangerate.host");
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("GoldenGuard/1.0");
});

builder.Services.AddHttpClient("news", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["NewsApi:BaseUrl"]
                            ?? "https://newsapi.org/v2");
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("GoldenGuard/1.0");
});

builder.Services.AddHttpClient("openai", c =>
{
    // use api.openai.com por padrão; se for Azure OpenAI, mude BaseUrl e headers lá
    c.BaseAddress = new Uri(builder.Configuration["OpenAI:BaseUrl"]
                            ?? "https://api.openai.com");
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("GoldenGuard/1.0");
});

// Proxy do Azure
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var enableSwagger = cfg.GetValue<bool>("Swagger:Enable", false);
var migrateOnStartup = cfg.GetValue<bool>("Ef:MigrateOnStartup", false);

var app = builder.Build();

// ========== Rotas de saúde sempre ativas ==========
app.MapGet("/healthz", () => Results.Ok(new { ok = true, env = app.Environment.EnvironmentName }))
   .ExcludeFromDescription();

// Teste de banco sob demanda (não roda no startup)
app.MapGet("/health/db", async (GgDbContext db) =>
{
    try
    {
        var can = await db.Database.CanConnectAsync();
        return can
            ? Results.Ok(new { db = "ok" })
            : Results.Problem("Não conectou ao Oracle", statusCode: 500);
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "Falha ao conectar ao Oracle", detail: ex.Message, statusCode: 500);
    }
}).ExcludeFromDescription();

// Raiz básica (substituída por redirect se Swagger estiver ativo)
app.MapGet("/", () => Results.Ok(new { service = "GoldenGuard API", status = "running" }))
   .ExcludeFromDescription();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("WebApp");
app.UseAuthentication();
app.UseAuthorization();

// NÃO migre automaticamente sem rede liberada (controlado por setting)
if (migrateOnStartup)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GgDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // log suave e segue a vida
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogWarning(ex, "Falha ao aplicar migrations na inicialização (Ef:MigrateOnStartup=true).");
    }
}

// Pastas p/ arquivos locais
var fs = app.Services.GetRequiredService<FileStorageService>();
await fs.EnsureFoldersAsync();

// ========== Endpoints ==========
app.MapAuth();
app.MapUsers();
app.MapTransactions();
app.MapIntegrations();  // /api/integrations/*
app.MapInsights();      // /api/insights/*

app.Run();