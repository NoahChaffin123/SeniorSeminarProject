using System;
using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Signup
{
    public class VerifyModel : PageModel
    {
        private readonly AppDbContext _db;

        public VerifyModel(AppDbContext db)
        {
            _db = db;
        }

        // bound for the view
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        public bool Verified { get; private set; }
        public string Message { get; private set; } = string.Empty;

        // GET /Signup/Verify?gameId=...&email=...&token=...
        public async Task<IActionResult> OnGetAsync(int gameId, string email, string token)
        {
            GameId = gameId;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                Verified = false;
                Message = "Invalid verification link.";
                return Page();
            }

            var emailNorm = EmailNormalizer.Normalize(email);

            var player = await _db.Players
                .SingleOrDefaultAsync(p => p.GameId == gameId && p.EmailNormalized == emailNorm);

            if (player is null)
            {
                Verified = false;
                Message = "Player not found for this game.";
                return Page();
            }

            if (!string.Equals(player.VerificationToken ?? "", token, StringComparison.Ordinal))
            {
                Verified = false;
                Message = "Verification token is invalid or has already been used.";
                return Page();
            }

            // Mark verified and allow the player to appear in the game.
            player.IsEmailVerified = true;

            // If you want them to immediately count as “active” in the roster,
            // set IsActive=true here. If you prefer admins to activate later,
            // comment the next line out. Requirement says appear only after verify:
            player.IsActive = true;

            // Invalidate token so the link can’t be reused
            player.VerificationToken = null;

            await _db.SaveChangesAsync();

            Verified = true;
            Message = "Your email has been verified. You’re in!";
            return Page();
        }
    }
}
