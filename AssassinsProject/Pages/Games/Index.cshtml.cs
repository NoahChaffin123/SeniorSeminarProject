using AssassinsProject.Data;
using AssassinsProject.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Games;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Game> Games { get; set; } = new();

    public async Task OnGetAsync()
    {
        Games = await _db.Games
            .OrderByDescending(g => g.Status)   // Active first
            .ThenBy(g => g.Name)
            .ToListAsync();
    }
}
