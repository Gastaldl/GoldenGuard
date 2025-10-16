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

    // Suporte a "Authorize" com Bearer JWT no Swagger
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
    throw new InvalidOperationException("Jwt:Key não configurado. Defina em appsettings ou Application Settings do App Service.");

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

// Recomendado no Azure (proxy envia X-Forwarded-Proto/For)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:Enable");
var app = builder.Build();

// ========== Pipeline ==========

// Sempre exponha health-check simples
app.MapGet("/healthz", () => Results.Ok(new { ok = true, env = app.Environment.EnvironmentName }))
   .ExcludeFromDescription();

// Raiz básica (substituída por redirect se Swagger estiver ativo)
app.MapGet("/", () => Results.Ok(new { service = "GoldenGuard API", status = "running" }))
   .ExcludeFromDescription();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redireciona raiz para o Swagger quando habilitado
    app.MapGet("/", () => Results.Redirect("/swagger"))
       .ExcludeFromDescription();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("WebApp");
app.UseAuthentication();
app.UseAuthorization();

// Aplicar migrations automaticamente (se houver)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GgDbContext>();
    db.Database.Migrate();
}
catch
{
    // Em produção registre o log; aqui seguimos sem travar a aplicação.
}

// Pastas para arquivos locais (JSON/TXT)
var fs = app.Services.GetRequiredService<FileStorageService>();
await fs.EnsureFoldersAsync();

// ========== Endpoints ==========
app.MapAuth();          // /api/auth/login
app.MapUsers();         // /api/users
app.MapTransactions();  // /api/transactions (+ /risk e /stats/monthly)

app.Run();