using System;
using System.Threading.Tasks;
using AssassinsProject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Auth
{
    public class VerifyEmailModel : PageModel
    {
        private readonly AppDbContext _db;
        public VerifyEmailModel(AppDbContext db) => _db = db;

        public async Task<IActionResult> OnGetAsync(int gameId, string email, string token)
        {
            if (gameId <= 0 || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return BadRequest("Invalid verification link.");

            var player = await _db.Players.FindAsync(gameId, email);
            if (player == null) return NotFound();

            if (!string.Equals(player.VerificationToken, token, StringComparison.Ordinal))
                return BadRequest("Invalid or expired token.");

            player.IsEmailVerified = true;

            // If your flow wants verified == immediately active, keep this:
            player.IsActive = true;

            player.VerificationToken = null;

            await _db.SaveChangesAsync();

            TempData["Flash"] = "Your email is verified. You’ve been added to the roster.";
            return RedirectToPage("/Games/Details", new { id = gameId });
        }
    }
}
