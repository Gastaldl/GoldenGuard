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

// ===== EF Core (Oracle) =====
builder.Services.AddDbContext<GgDbContext>(opt =>
    opt.UseOracle(cfg.GetConnectionString("Oracle")));

// ===== DI =====
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddScoped<TransactionService>();

// ===== CORS =====
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("WebApp", p =>
        p.WithOrigins("https://localhost:7275") // porta do GoldenGuard.WebApp
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// ===== Swagger =====
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

// ===== JWT =====
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

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
            IssuerSigningKey = new SymmetricSecurityKey(key),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("admin"));
});

var app = builder.Build();

// ===== Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseHttpsRedirection();

// Habilita CORS para chamadas do WebApp (antes dos endpoints)
app.UseCors("WebApp");

app.UseAuthentication();
app.UseAuthorization();

// Prepara pastas de arquivos locais (JSON/TXT)
var fs = app.Services.GetRequiredService<FileStorageService>();
await fs.EnsureFoldersAsync();

// ===== Endpoints =====
app.MapAuth();          // /api/auth/login  -> Atualize para usar GgDbContext (EF)
app.MapUsers();         // /api/users       -> Repositórios EF
app.MapTransactions();  // /api/transactions (+ /risk e /stats/monthly) -> EF/LINQ

app.Run();