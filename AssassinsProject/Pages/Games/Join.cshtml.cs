using System;
using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Games
{
    public class JoinModel : PageModel
    {
        private readonly AppDbContext _db;
        public JoinModel(AppDbContext db) => _db = db;

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; } // game id in route

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostAsync()
        {
            // Ensure the game exists
            var game = await _db.Games.FindAsync(Id);
            if (game == null) return NotFound();

            if (string.IsNullOrWhiteSpace(Email))
                return BadRequest("Email is required.");

            var normalized = Email.ToUpperInvariant();

            // Find existing player
            var player = await _db.Players.FindAsync(Id, Email);

            if (player == null)
            {
                player = new Player
                {
                    GameId = Id,
                    Email = Email,
                    EmailNormalized = normalized,
                    DisplayName = Email,         // or set something nicer
                    Alias = string.Empty,
                    // CRITICAL: do NOT activate now
                    IsActive = false,
                    IsEmailVerified = false
                };
                _db.Players.Add(player);
            }
            else
            {
                // Re-joining path: keep them inactive until verified
                player.IsActive = false;
                player.IsEmailVerified = false;
            }

            // Issue verification token + send email
            player.VerificationToken = Guid.NewGuid().ToString("N");
            player.VerificationSentAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            // TODO: Send your email with link to /Auth/VerifyEmail?gameId=...&email=...&token=...
            // await _mailer.SendVerificationLinkAsync(Email, MakeVerifyUrl(Id, Email, player.VerificationToken));

            TempData["Flash"] = "We emailed you a verification link. Please verify to be added to the roster.";
            return RedirectToPage("/Games/Details", new { id = Id });
        }
    }
}
