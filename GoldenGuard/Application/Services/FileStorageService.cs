using System.Text.Json;

namespace GoldenGuard.Application.Services;

public sealed class FileStorageService(IConfiguration cfg)
{
    private readonly string _basePath = cfg["FileStorage:BasePath"] ?? "Files";
    private readonly string _jsonPath = cfg["FileStorage:TransactionsJson"] ?? "Files/transactions.json";
    private readonly string _auditLog = cfg["FileStorage:AuditLog"] ?? "Files/audit.log";

    public async Task EnsureFoldersAsync()
    {
        Directory.CreateDirectory(_basePath);
        if (!File.Exists(_jsonPath)) await File.WriteAllTextAsync(_jsonPath, "[]");
        if (!File.Exists(_auditLog)) await File.WriteAllTextAsync(_auditLog, "");
    }

    public async Task AppendAuditAsync(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        await File.AppendAllTextAsync(_auditLog, line);
    }

    public async Task<T[]> ReadJsonAsync<T>()
    {
        var txt = await File.ReadAllTextAsync(_jsonPath);
        return JsonSerializer.Deserialize<T[]>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? Array.Empty<T>();
    }

    public async Task WriteJsonAsync<T>(IEnumerable<T> items)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_jsonPath, json);
    }
}
