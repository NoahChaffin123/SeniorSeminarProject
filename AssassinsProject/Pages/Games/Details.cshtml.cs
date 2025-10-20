using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Games;

public class DetailsModel(AppDbContext db, GameService svc) : PageModel
{
    private readonly AppDbContext _db = db;
    private readonly GameService _svc = svc;

    public Game? Game { get; set; }
    public List<Player> ActivePlayers { get; set; } = [];
    public List<Player> AllPlayers { get; set; } = [];
    public List<Elimination> Eliminations { get; set; } = [];

    // Shareable links shown on the page
    public string? SignupUrl { get; set; }
    public string? ReportUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Game = await _db.Games.FindAsync(id);
        if (Game is null) return NotFound();

        var req = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";
        SignupUrl = $"{baseUrl}/Signup?gameId={id}";
        ReportUrl = $"{baseUrl}/Eliminations/Report?gameId={id}";

        ActivePlayers = await _db.Players
            .Where(p => p.GameId == id && p.IsActive)
            .OrderBy(p => p.DisplayName)
            .ToListAsync();

        AllPlayers = await _db.Players
            .Where(p => p.GameId == id)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.Points)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();

        Eliminations = await _db.Eliminations
            .Where(e => e.GameId == id)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostStartAsync(int id)
    {
        await _svc.StartGameAsync(id);
        return RedirectToPage(new { id });
    }

    // NEW: toggle signups during Setup
    public async Task<IActionResult> OnPostCloseSignupAsync(int id)
    {
        var g = await _db.Games.FindAsync(id);
        if (g is null) return NotFound();
        if (g.Status != GameStatus.Setup) return BadRequest("Can only change signup state during Setup.");
        g.IsSignupOpen = false;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostOpenSignupAsync(int id)
    {
        var g = await _db.Games.FindAsync(id);
        if (g is null) return NotFound();
        if (g.Status != GameStatus.Setup) return BadRequest("Can only change signup state during Setup.");
        g.IsSignupOpen = true;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }
}
