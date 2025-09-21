using GoldenGuard.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace GoldenGuard.WebApp.Pages.Users;

public class CreateModel : PageModel
{
    private readonly ApiClient _api;
    public CreateModel(ApiClient api) => _api = api;

    public class Form
    {
        [Required(ErrorMessage = "Informe o nome.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inv�lido.")]
        public string Email { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Renda n�o pode ser negativa.")]
        public decimal MonthlyIncome { get; set; }
    }

    [BindProperty] public Form Data { get; set; } = new();

    public IActionResult OnGet()
    {
        // exige login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        // (opcional) checar papel no cookie para exibir msg amig�vel
        // var role = Request.Cookies["gg.role"];
        // if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)) ...

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // exige login
        if (string.IsNullOrWhiteSpace(Request.Cookies["gg.jwt"]))
            return RedirectToPage("/Auth/Login");

        if (!ModelState.IsValid) return Page();

        try
        {
            var id = await _api.CreateUserAsync(
                new ApiClient.CreateUserDto(Data.Name, Data.Email, Data.MonthlyIncome)
            );

            if (id is null)
            {
                TempData["msg"] = "Falha ao criar usu�rio. Verifique se voc� tem permiss�o (admin) e tente novamente.";
                return Page();
            }

            TempData["msg"] = "Usu�rio criado com sucesso.";
            return RedirectToPage("/Users/Index");
        }
        catch (UnauthorizedAccessException)
        {
            // Se a API respondeu 401/403, manda logar (ou mostrar msg)
            TempData["msg"] = "Sua sess�o expirou ou voc� n�o tem permiss�o. Fa�a login como admin.";
            return RedirectToPage("/Auth/Login");
        }
    }
}