using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Games;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly GameService _svc;
    private readonly AdminGuard _guard;

    public DetailsModel(AppDbContext db, GameService svc, AdminGuard guard)
    {
        _db = db;
        _svc = svc;
        _guard = guard;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Game? Game { get; set; }

    // Players
    public List<Player> AllPlayers { get; set; } = new();
    public List<Player> ActivePlayers { get; set; } = new();

    // Scoreboard
    public List<Player> ScoreboardPlayers { get; set; } = new();

    // Fast lookup for rendering "A -> B"
    public Dictionary<string, Player> PlayersByEmail { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // History
    public List<Elimination> Eliminations { get; set; } = new();

    // Share links
    public string? SignupUrl { get; set; }
    public string? ReportUrl { get; set; }

    public string StatusFor(Player p)
    {
        var g = Game!;
        return g.Status switch
        {
            GameStatus.Setup     => p.IsEmailVerified ? "Verified" : "Unverified",
            GameStatus.Active    => p.IsActive        ? "Active"    : "Eliminated",
            GameStatus.Completed => p.IsActive        ? "Winner"    : "Eliminated",
            _                    => p.IsActive        ? "Active"    : "Eliminated"
        };
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        Id = id;

        Game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (Game is null) return NotFound();

        AllPlayers = await _db.Players
            .Where(p => p.GameId == id)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();

        ActivePlayers = AllPlayers.Where(p => p.IsActive).ToList();

        PlayersByEmail = AllPlayers.ToDictionary(p => p.Email, p => p, StringComparer.OrdinalIgnoreCase);

        Eliminations = await _db.Eliminations
            .Where(e => e.GameId == id)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync();

        if (Game.Status == GameStatus.Setup)
        {
            ScoreboardPlayers = AllPlayers
                .OrderByDescending(p => p.IsEmailVerified)
                .ThenBy(p => p.DisplayName)
                .ToList();
        }
        else
        {
            ScoreboardPlayers = AllPlayers
                .OrderByDescending(p => p.IsActive)
                .ThenByDescending(p => p.Points)
                .ThenBy(p => p.DisplayName)
                .ToList();
        }

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

    public async Task<IActionResult> OnPostStartAsync()
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id = Id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        try
        {
            await _svc.StartGameAsync(Id);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Unable to start the game: {ex.Message}");
        }

        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostEndAsync()
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id = Id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        var g = await _db.Games.FirstOrDefaultAsync(x => x.Id == Id);
        if (g is null) return NotFound();

        g.Status = GameStatus.Completed;
        g.IsSignupOpen = false;
        g.EndedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostCloseSignupAsync()
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id = Id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        Game? g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        g.IsSignupOpen = false;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostOpenSignupAsync()
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id = Id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        Game? g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        g.IsSignupOpen = true;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostPauseAsync()
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id = Id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        var g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        if (g.Status != GameStatus.Active) return BadRequest("Game is not active.");

        g.IsPaused = true;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostUnpauseAsync()
    {
        if (!_guard.IsAdmin(HttpContext))
        {
            var returnUrl = Url.Page("/Games/Details", new { id = Id });
            return RedirectToPage("/Auth/Login", new { returnUrl });
        }

        var g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        if (g.Status != GameStatus.Active) return BadRequest("Game is not active.");

        g.IsPaused = false;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }
}
