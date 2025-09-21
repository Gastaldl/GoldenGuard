using GoldenGuard.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GoldenGuard.WebApp.Pages.Users;

public class IndexModel : PageModel
{
    private readonly ApiClient _api;
    public IndexModel(ApiClient api) => _api = api;

    public List<ApiClient.UserProfile> Users { get; private set; } = new();

    [BindProperty] public long DeleteId { get; set; }

    public async Task<IActionResult> OnGet()
    {
        // Exige login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        try
        {
            Users = await _api.ListUsersAsync();
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            // Token ausente/expirado
            TempData["msg"] = "Sua sess�o expirou. Fa�a login novamente.";
            return RedirectToPage("/Auth/Login");
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        // Exige login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        // Checa papel admin (UX; a API tamb�m valida via policy)
        var role = Request.Cookies["gg.role"];
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin)
        {
            TempData["msg"] = "Voc� n�o tem permiss�o para excluir usu�rios.";
            return RedirectToPage();
        }

        try
        {
            if (await _api.DeleteUserAsync(DeleteId))
                TempData["msg"] = "Usu�rio exclu�do com sucesso.";
            else
                TempData["msg"] = "Falha ao excluir usu�rio.";

            return RedirectToPage();
        }
        catch (UnauthorizedAccessException)
        {
            TempData["msg"] = "Sua sess�o expirou. Fa�a login novamente.";
            return RedirectToPage("/Auth/Login");
        }
    }
}