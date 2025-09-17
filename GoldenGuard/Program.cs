using GoldenGuard.Application.Services;
using GoldenGuard.Data;
using GoldenGuard.Endpoints;
using GoldenGuard.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// DI
builder.Services.AddSingleton<IOracleConnectionFactory, OracleConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddScoped<TransactionService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Arquivos locais (JSON/TXT)
var fs = app.Services.GetRequiredService<FileStorageService>();
await fs.EnsureFoldersAsync();

// Swagger + endpoints
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapUsers();
app.MapTransactions();

app.Run();
