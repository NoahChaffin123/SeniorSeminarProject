using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssassinsProject.Pages.Games;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly GameService _svc;

    public DetailsModel(AppDbContext db, GameService svc)
    {
        _db = db;
        _svc = svc;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Game? Game { get; set; }

    // Players
    public List<Player> AllPlayers { get; set; } = new();
    public List<Player> ActivePlayers { get; set; } = new();

    // Scoreboard (active first, then points desc)
    public List<Player> ScoreboardPlayers { get; set; } = new();

    // Fast lookup for rendering "A -> B"
    public Dictionary<string, Player> PlayersByEmail { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // History
    public List<Elimination> Eliminations { get; set; } = new();

    // Share links
    public string? SignupUrl { get; set; }
    public string? ReportUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Id = id;

        Game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (Game is null) return NotFound();

        AllPlayers = await _db.Players
            .Where(p => p.GameId == id)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();

        ActivePlayers = AllPlayers.Where(p => p.IsActive).ToList();

        // Build lookup by email for easy formatting on the view
        PlayersByEmail = AllPlayers.ToDictionary(p => p.Email, p => p, StringComparer.OrdinalIgnoreCase);

        Eliminations = await _db.Eliminations
            .Where(e => e.GameId == id)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync();

        // Scoreboard: active players first, then by points (desc), then name
        ScoreboardPlayers = AllPlayers
            .OrderByDescending(p => p.IsActive)   // true first
            .ThenByDescending(p => p.Points)
            .ThenBy(p => p.DisplayName)
            .ToList();

        // Build absolute URLs for sharing
        SignupUrl = Url.Page(
            pageName: "/Signup/Index",
            pageHandler: null,
            values: new { gameId = id },
            protocol: Request.Scheme,
            host: Request.Host.ToString()
        ) ?? string.Empty;

        ReportUrl = Url.Page(
            pageName: "/Eliminations/Report",
            pageHandler: null,
            values: new { gameId = id },
            protocol: Request.Scheme,
            host: Request.Host.ToString()
        ) ?? string.Empty;

        return Page();
    }

    // Start game
    public async Task<IActionResult> OnPostStartAsync()
    {
        try
        {
            await _svc.StartGameAsync(Id); // GameService handles validation/assignment and sets StartedAt
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Unable to start the game: {ex.Message}");
        }
        return await OnGetAsync(Id);
    }

    // End game (set Completed, close signups, and stamp EndedAt)
    public async Task<IActionResult> OnPostEndAsync()
    {
        var g = await _db.Games.FirstOrDefaultAsync(x => x.Id == Id);
        if (g is null) return NotFound();

        g.Status = GameStatus.Completed;
        g.IsSignupOpen = false;
        g.EndedAt = DateTimeOffset.UtcNow; // stamp an end time

        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostCloseSignupAsync()
    {
        Game? g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        g.IsSignupOpen = false;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostOpenSignupAsync()
    {
        Game? g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        g.IsSignupOpen = true;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }
}
