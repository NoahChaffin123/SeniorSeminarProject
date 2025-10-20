using System.ComponentModel.DataAnnotations;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Games;

public class CreateModel(GameService svc) : PageModel
{
    private readonly GameService _svc = svc;

    [BindProperty, Required] public string Name { get; set; } = "";
    [TempData] public string? Flash { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var game = await _svc.CreateGameAsync(Name);
        Flash = "Game created!";
        return RedirectToPage("Details", new { id = game.Id });
    }
}
