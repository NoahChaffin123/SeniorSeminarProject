using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Players;

public class AddedModel : PageModel
{
    [BindProperty(SupportsGet = true)] public int GameId { get; set; }
    [BindProperty(SupportsGet = true)] public string Email { get; set; } = "";
    [TempData] public string? NewPasscode { get; set; }

    public string? ReportUrl { get; set; }

    public void OnGet()
    {
        var req = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";
        ReportUrl = $"{baseUrl}/Eliminations/Report?gameId={GameId}&eliminatorEmail={Uri.EscapeDataString(Email)}";
    }
}
