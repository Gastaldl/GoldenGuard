using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GoldenGuard.WebApp.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _ctx;

    public ApiClient(HttpClient http, IHttpContextAccessor ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    // Helpers
    private void EnsureAuthHeader()
    {
        var token = _ctx.HttpContext?.Request.Cookies["gg.jwt"];
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    private static async Task<T?> ReadOrThrow401<T>(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("JWT ausente ou inválido.");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    private static void ThrowIf401(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("JWT ausente ou inválido.");
    }

    private static long? TryExtractIdFromLocation(HttpResponseMessage resp)
    {
        var loc = resp.Headers.Location;
        if (loc is null) return null;

        string lastSegment;
        if (loc.IsAbsoluteUri)
        {
            lastSegment = loc.Segments.LastOrDefault()?.TrimEnd('/') ?? "";
        }
        else
        {
            // Location relativa (ex.: "/api/transactions/123")
            var s = loc.OriginalString.TrimEnd('/');
            lastSegment = s.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        }

        return long.TryParse(lastSegment, out var id) ? id : (long?)null;
    }

    // DTOs usados no WebApp
    public sealed record UserProfile(long Id, string Name, string Email, decimal MonthlyIncome, DateTime CreatedAt);
    public sealed record CreateUserDto(string Name, string Email, decimal MonthlyIncome);

    public sealed record TransactionDto(
        long Id, long UserId, string Operator, string Kind,
        decimal Amount, DateTime OccurredAt, string? RawLabel, DateTime CreatedAt);

    public sealed record CreateTransactionDto(
        long UserId, string Operator, string Kind, decimal Amount, DateTime OccurredAt, string? RawLabel);

    public sealed record RiskResponse(decimal ratioPercent, bool above30);
    public sealed record MonthlyRow(string YEAR_MONTH, decimal DEPOSITS, decimal WITHDRAWALS);

    public sealed record LoginDto(string Username, string Password);
    public sealed record LoginResult(string token, string role, long userId);

    // ===== INTEGRATIONS =====
    public sealed record ConvertResult(string? from, string? to, decimal? amount, decimal? rate, decimal? result, string? date);
    public sealed record NewsItem(string? title, string? source, DateTime? publishedAt, string? url);

    public async Task<ConvertResult?> ConvertAsync(string from, string to, decimal amount)
    {
        // Não precisa de auth, mas não atrapalha
        EnsureAuthHeader();
        var url = $"/api/integrations/rates/convert?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&amount={amount}";
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ConvertResult>();
    }

    public async Task<List<NewsItem>> GetNewsAsync(string? query = "apostas", int pageSize = 5)
    {
        EnsureAuthHeader();
        var q = string.IsNullOrWhiteSpace(query) ? "apostas" : query.Trim();
        var url = $"/api/integrations/news?query={Uri.EscapeDataString(q)}&pageSize={pageSize}";
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return new();
        // O endpoint já devolve [{title, source, publishedAt, url}]
        var arr = await resp.Content.ReadFromJsonAsync<List<NewsItem>>();
        return arr ?? new();
    }

    // AUTH
    public async Task<LoginResult?> LoginAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login", new LoginDto(username, password));
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<LoginResult>();
    }

    // USERS
    public async Task<List<UserProfile>> ListUsersAsync()
    {
        EnsureAuthHeader();
        var resp = await _http.GetAsync("/api/users");
        return await ReadOrThrow401<List<UserProfile>>(resp) ?? new();
    }

    public async Task<long?> CreateUserAsync(CreateUserDto dto)
    {
        EnsureAuthHeader();
        var resp = await _http.PostAsJsonAsync("/api/users", dto);

        // trata 401 explicitamente
        ThrowIf401(resp);
        if (!resp.IsSuccessStatusCode) return null;

        // 1) tenta corpo { id }
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, long>>();
            if (body is not null && body.TryGetValue("id", out var idFromBody))
                return idFromBody;
        }
        catch {}

        // 2) tenta extrair do header Location
        return TryExtractIdFromLocation(resp);
    }

    public async Task<bool> DeleteUserAsync(long id)
    {
        EnsureAuthHeader();
        var resp = await _http.DeleteAsync($"/api/users/{id}");
        ThrowIf401(resp);
        return resp.IsSuccessStatusCode;
    }

    // TRANSACTIONS
    public async Task<List<TransactionDto>> ListTransactionsByUserAsync(long userId, DateTime? from = null, DateTime? to = null)
    {
        EnsureAuthHeader();
        var url = $"/api/transactions/by-user/{userId}";
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);

        var resp = await _http.GetAsync(url);
        return await ReadOrThrow401<List<TransactionDto>>(resp) ?? new();
    }

    public async Task<long?> CreateTransactionAsync(CreateTransactionDto dto)
    {
        EnsureAuthHeader();
        var resp = await _http.PostAsJsonAsync("/api/transactions", dto);

        // trata 401 explicitamente
        ThrowIf401(resp);
        if (!resp.IsSuccessStatusCode) return null;

        // 1) tenta corpo { id }
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, long>>();
            if (body is not null && body.TryGetValue("id", out var idFromBody))
                return idFromBody;
        }
        catch { }

        // 2) tenta extrair do header Location
        return TryExtractIdFromLocation(resp);
    }

    // KPI/GRÁFICOS
    public async Task<RiskResponse?> GetRiskAsync(long userId, int year, int month)
    {
        EnsureAuthHeader();
        var resp = await _http.GetAsync($"/api/transactions/risk/{userId}/{year}/{month}");
        return await ReadOrThrow401<RiskResponse>(resp);
    }

    public async Task<List<MonthlyRow>> GetMonthlyAsync(long userId, int year)
    {
        EnsureAuthHeader();
        var resp = await _http.GetAsync($"/api/transactions/stats/monthly/{userId}?year={year}");
        return await ReadOrThrow401<List<MonthlyRow>>(resp) ?? new();
    }
}