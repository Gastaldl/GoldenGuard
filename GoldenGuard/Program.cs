using System.Text;
using GoldenGuard.Application.Services;
using GoldenGuard.Data;
using GoldenGuard.Endpoints;
using GoldenGuard.Infrastructure.Repositories;
using GoldenGuard.WebApi.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    throw new InvalidOperationException("Jwt:Key n�o configurado. Defina em appsettings ou Application Settings do App Service.");

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

var app = builder.Build();

// ========== Pipeline ==========
if (app.Environment.IsDevelopment())
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

// CORS antes dos endpoints
app.UseCors("WebApp");

app.UseAuthentication();
app.UseAuthorization();

// aplicar migrations automaticamente na subida da API
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GgDbContext>();
    db.Database.Migrate();
}
catch
{
}

// Pastas para arquivos locais (JSON/TXT)
var fs = app.Services.GetRequiredService<FileStorageService>();
await fs.EnsureFoldersAsync();

// ========== Endpoints ==========
app.MapAuth();          // /api/auth/login  (agora usando EF no AuthEndpoints)
app.MapUsers();         // /api/users       (reposit�rios EF)
app.MapTransactions();  // /api/transactions (+ /risk e /stats/monthly via EF/LINQ)

app.Run();