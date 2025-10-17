using System.Text.Json;
using GoldenGuard.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GoldenGuard.WebApp.Pages;

public class IndexModel : PageModel
{
    private readonly ApiClient _api;
    public IndexModel(ApiClient api) => _api = api;

    // Dados para a tela
    public List<ApiClient.UserProfile> Users { get; private set; } = new();

    [BindProperty(SupportsGet = true)] public long? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }

    public decimal Ratio { get; private set; }
    public bool Above30 { get; private set; }

    public List<ApiClient.TransactionDto> RecentTx { get; private set; } = new();

    // ===== Conversor (GET form) =====
    [BindProperty(SupportsGet = true)] public string FxFrom { get; set; } = "USD";
    [BindProperty(SupportsGet = true)] public string FxTo { get; set; } = "BRL";
    [BindProperty(SupportsGet = true)] public decimal? FxAmount { get; set; }
    public decimal? FxRate { get; private set; }
    public decimal? FxResult { get; private set; }
    public string? FxDate { get; private set; }

    // ===== Notícias =====
    [BindProperty(SupportsGet = true)] public string? NewsQuery { get; set; } = "apostas";
    public List<ApiClient.NewsItem> News { get; private set; } = new();

    // JSON que alimenta o Chart.js no front (mensal depósitos x saques)
    public string MonthlyJson { get; private set; } = "[]";

    public async Task<IActionResult> OnGet()
    {
        // 1) Se não há JWT, redireciona para login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        try
        {
            // 2) Lista de usuários (precisa estar autenticado)
            Users = await _api.ListUsersAsync();
            if (Users.Count == 0) return Page();

            var now = DateTime.Now;
            var year = Year ?? now.Year;
            var month = Month ?? now.Month;

            // 3) Define usuário selecionado (cookie, querystring, primeiro da lista)
            long uid;
            if (UserId.HasValue) uid = UserId.Value;
            else if (long.TryParse(Request.Cookies["gg.userId"], out var fromCookie)) uid = fromCookie;
            else uid = Users[0].Id;

            UserId = uid;
            Year = year;
            Month = month;

            // 4) KPI de risco
            var risk = await _api.GetRiskAsync(uid, year, month);
            if (risk is { } r)
            {
                Ratio = r.ratioPercent;
                Above30 = r.above30;
            }

            // 5) Transações recentes (mês atual)
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);
            var list = await _api.ListTransactionsByUserAsync(uid, from, to);
            RecentTx = list.OrderByDescending(x => x.OccurredAt).Take(10).ToList();

            // 6) Estatística mensal (depósitos x saques) para o gráfico
            var monthly = await _api.GetMonthlyAsync(uid, year);
            MonthlyJson = JsonSerializer.Serialize(monthly);

            // 7) Conversor de moedas (chama apenas se houver amount >= 0)
            FxRate = null; FxResult = null; FxDate = null;
            if (FxAmount.HasValue && FxAmount.Value >= 0)
            {
                var conv = await _api.ConvertAsync(FxFrom, FxTo, FxAmount.Value);
                if (conv is not null)
                {
                    FxRate = conv.rate;
                    FxResult = conv.result;
                    FxDate = conv.date;
                }
            }

            // 8) Notícias (não exige auth na API, mas mantém o fluxo)
            News = await _api.GetNewsAsync(NewsQuery, pageSize: 5);

            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            // Se o ApiClient detectar 401, redireciona para login
            return RedirectToPage("/Auth/Login");
        }
    }
}