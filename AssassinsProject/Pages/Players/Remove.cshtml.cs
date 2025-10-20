using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Players;

public class RemoveModel(AppDbContext db, GameService svc) : PageModel
{
    private readonly AppDbContext _db = db;
    private readonly GameService _svc = svc;

    [BindProperty(SupportsGet = true)] public int GameId { get; set; }
    [BindProperty(SupportsGet = true)] public string Email { get; set; } = "";

    public string DisplayName { get; set; } = "";
    public string? PhotoUrl { get; set; }
    public GameStatus Status { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var game = await _db.Games.FindAsync(GameId);
        if (game is null) return NotFound();
        Status = game.Status;

        var p = await _db.Players.SingleOrDefaultAsync(x => x.GameId == GameId && x.Email == Email);
        if (p is null) return NotFound();

        DisplayName = p.DisplayName;
        PhotoUrl = p.PhotoUrl;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            await _svc.RemovePlayerAsync(GameId, Email);
            return RedirectToPage("/Games/Details", new { id = GameId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await OnGetAsync();
            return Page();
        }
    }
}
