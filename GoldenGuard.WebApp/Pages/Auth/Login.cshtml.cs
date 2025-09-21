using GoldenGuard.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GoldenGuard.WebApp.Pages.Auth;
public class LoginModel : PageModel
{
    private readonly ApiClient _api;
    public LoginModel(ApiClient api) => _api = api;

    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var res = await _api.LoginAsync(Username, Password);
        if (res is null)
        {
            Error = "Credenciais inválidas.";
            return Page();
        }
        Response.Cookies.Append("gg.jwt", res.token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax });
        Response.Cookies.Append("gg.userId", res.userId.ToString());
        Response.Cookies.Append("gg.role", res.role);
        return RedirectToPage("/Index");
    }
}