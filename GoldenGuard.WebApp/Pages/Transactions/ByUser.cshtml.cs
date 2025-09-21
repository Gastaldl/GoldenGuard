using GoldenGuard.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GoldenGuard.WebApp.Pages.Transactions;

public class ByUserModel : PageModel
{
    private readonly ApiClient _api;
    public ByUserModel(ApiClient api) => _api = api;

    public List<ApiClient.UserProfile> Users { get; private set; } = new();
    public List<ApiClient.TransactionDto> Items { get; private set; } = new();

    [BindProperty(SupportsGet = true)] public long? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    // Form de criação rápida
    [BindProperty] public long NewUserId { get; set; }
    [BindProperty] public string NewOperator { get; set; } = string.Empty;
    [BindProperty] public string NewKind { get; set; } = "DEPOSIT";
    [BindProperty] public decimal NewAmount { get; set; }
    [BindProperty] public DateTime NewOccurredAt { get; set; } = DateTime.Now;
    [BindProperty] public string? NewRawLabel { get; set; }

    public decimal TotalDeposits { get; private set; }
    public decimal TotalWithdrawals { get; private set; }

    public async Task<IActionResult> OnGet()
    {
        // exige login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        try
        {
            Users = await _api.ListUsersAsync();
            if (Users.Count == 0) return Page();

            var uid = UserId ?? Users[0].Id;
            UserId = uid;
            NewUserId = uid;

            Items = await _api.ListTransactionsByUserAsync(uid, From, To);

            TotalDeposits = Items
                .Where(i => i.Kind.Equals("DEPOSIT", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.Amount);

            TotalWithdrawals = Items
                .Where(i => i.Kind.Equals("WITHDRAWAL", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.Amount);

            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            TempData["msg"] = "Sua sessão expirou. Faça login novamente.";
            return RedirectToPage("/Auth/Login");
        }
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        // exige login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        // checa papel admin (UX; API também valida)
        var role = Request.Cookies["gg.role"];
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin)
        {
            TempData["msg"] = "Você não tem permissão para criar transações.";
            return RedirectToPage(new { userId = UserId, from = From, to = To });
        }

        if (NewUserId <= 0 || string.IsNullOrWhiteSpace(NewOperator) || string.IsNullOrWhiteSpace(NewKind))
        {
            TempData["msg"] = "Preencha os campos obrigatórios.";
            return RedirectToPage(new { userId = UserId, from = From, to = To });
        }

        try
        {
            var id = await _api.CreateTransactionAsync(new ApiClient.CreateTransactionDto(
                NewUserId, NewOperator, NewKind, NewAmount, NewOccurredAt, NewRawLabel));

            TempData["msg"] = id is null ? "Falha ao criar transação." : $"Transação #{id} criada.";
            return RedirectToPage(new { userId = NewUserId, from = From, to = To });
        }
        catch (UnauthorizedAccessException)
        {
            TempData["msg"] = "Sua sessão expirou. Faça login novamente.";
            return RedirectToPage("/Auth/Login");
        }
    }
}