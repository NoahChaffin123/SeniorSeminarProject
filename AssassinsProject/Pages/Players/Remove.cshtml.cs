using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Players
{
    public class RemoveModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly GameService _svc;

        public RemoveModel(AppDbContext db, GameService svc)
        {
            _db = db;
            _svc = svc;
        }

        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        // Exposed to the Razor view
        public Player? Player { get; set; }

        public async Task<IActionResult> OnGetAsync(int gameId, string email)
        {
            GameId = gameId;
            Email = email;

            Player = await _db.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.GameId == gameId && p.Email == email);

            if (Player is null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                return BadRequest("Email is required.");
            }

            await _svc.RemovePlayerAsync(GameId, Email);

            return RedirectToPage("/Games/Details", new { id = GameId });
        }
    }
}
