using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Players;

public class AddedModel : PageModel
{
    [BindProperty(SupportsGet = true)] public int GameId { get; set; }
    [BindProperty(SupportsGet = true)] public string Email { get; set; } = "";
    [TempData] public string? NewPasscode { get; set; }
    public void OnGet() { }
}
