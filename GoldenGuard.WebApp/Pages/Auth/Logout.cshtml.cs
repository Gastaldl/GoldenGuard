using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GoldenGuard.WebApp.Pages.Auth;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        // Remove cookies de autentica��o/identidade
        Response.Cookies.Delete("gg.jwt");
        Response.Cookies.Delete("gg.userId");
        Response.Cookies.Delete("gg.role");

        // Redireciona para a p�gina de login (ou Home se preferir)
        return RedirectToPage("/Auth/Login");
    }
}