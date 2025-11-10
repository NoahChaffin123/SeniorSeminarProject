using AssassinsProject.Data;
using AssassinsProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssassinsProject.Pages.Games;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Game> Games { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Sort newest first using identity column
        Games = await _db.Games
            .OrderByDescending(g => g.Id)
            .ToListAsync();
    }

    // POST: delete a game (and its children), safely breaking Player->Player ring.
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        // Quick exist check
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null) return RedirectToPage();

        using var tx = await _db.Database.BeginTransactionAsync();

        // 1) Remove eliminations first
        var eliminations = _db.Eliminations.Where(e => e.GameId == id);
        _db.Eliminations.RemoveRange(eliminations);
        await _db.SaveChangesAsync();

        // 2) Load players in this game
        var players = await _db.Players
            .Where(p => p.GameId == id)
            .ToListAsync();

        // 3) Break the self-referencing ring: null out TargetEmail for everyone
        if (players.Count > 0)
        {
            foreach (var p in players)
            {
                p.TargetEmail = null;
            }
            await _db.SaveChangesAsync();
        }

        // 4) Delete players safely
        if (players.Count > 0)
        {
            _db.Players.RemoveRange(players);
            await _db.SaveChangesAsync();
        }

        // 5) Finally delete the game
        _db.Games.Remove(game);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();
        return RedirectToPage();
    }
}
