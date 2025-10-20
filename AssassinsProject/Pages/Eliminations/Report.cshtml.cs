using AssassinsProject.Data;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Eliminations;

public class ReportModel(AppDbContext db, GameService svc) : PageModel
{
    private readonly AppDbContext _db = db;
    private readonly GameService _svc = svc;

    [BindProperty(SupportsGet = true)] public int GameId { get; set; }

    // Optional preselect via query string: ?eliminatorEmail=you@hendrix.edu
    [BindProperty(SupportsGet = true)] public string? EliminatorEmail { get; set; }

    public class PlayerOption
    {
        public required string Email { get; set; }
        public required string Alias { get; set; }
    }

    public List<PlayerOption> ActivePlayers { get; set; } = [];

    [BindProperty] public string VictimEmail { get; set; } = "";
    [BindProperty] public string VictimPasscode { get; set; } = "";
    [BindProperty] public string EliminatorPasscode { get; set; } = ""; // NEW

    public async Task<IActionResult> OnGetAsync()
    {
        var game = await _db.Games.FindAsync(GameId);
        if (game is null) return NotFound();

        ActivePlayers = await _db.Players
            .Where(p => p.GameId == GameId && p.IsActive)
            .OrderBy(p => p.Alias)
            .Select(p => new PlayerOption { Email = p.Email, Alias = p.Alias })
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(EliminatorEmail) &&
            !ActivePlayers.Any(op => op.Email == EliminatorEmail))
        {
            EliminatorEmail = null;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(EliminatorEmail) ||
            string.IsNullOrWhiteSpace(VictimEmail) ||
            string.IsNullOrWhiteSpace(VictimPasscode) ||
            string.IsNullOrWhiteSpace(EliminatorPasscode))
        {
            ModelState.AddModelError(string.Empty, "All fields are required.");
            await OnGetAsync();
            return Page();
        }

        if (EliminatorEmail == VictimEmail)
        {
            ModelState.AddModelError(string.Empty, "A player cannot eliminate themselves.");
            await OnGetAsync();
            return Page();
        }

        try
        {
            await _svc.ReportEliminationAsync(
                gameId: GameId,
                eliminatorEmail: EliminatorEmail,
                victimEmail: VictimEmail,
                providedVictimPasscode: VictimPasscode,
                providedEliminatorPasscode: EliminatorPasscode
            );
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
