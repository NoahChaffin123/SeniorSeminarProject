using System;
using System.Threading;
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

        // Query params
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Email { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        // What your .cshtml expects
        public bool Verified { get; set; }
        public string? Message { get; set; }

        // Optional for the page (if you render it)
        public Game? Game { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
        {
            Game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == GameId, ct);

            if (GameId <= 0 || string.IsNullOrWhiteSpace(Email) || Game is null)
            {
                Verified = false;
                Message = "Invalid or missing verification token.";
                return Page();
            }

            var emailNorm = EmailNormalizer.Normalize(Email);
            var player = await _db.Players
                .FirstOrDefaultAsync(p => p.GameId == GameId && p.EmailNormalized == emailNorm, ct);

            if (player is null)
            {
                Verified = false;
                Message = "We couldn't find a signup for that email in this game.";
                return Page();
            }

            // Already verified? Friendly success.
            if (player.IsEmailVerified)
            {
                Verified = true;
                Message = "You're already verified and in the game. See you on the field!";
                return Page();
            }

            // Need a valid (latest) token
            if (string.IsNullOrWhiteSpace(Token) || string.IsNullOrWhiteSpace(player.VerificationToken))
            {
                Verified = false;
                Message = "Invalid or missing verification token.";
                return Page();
            }

            if (!string.Equals(Token, player.VerificationToken, StringComparison.Ordinal))
            {
                Verified = false;
                Message = "This verification link is invalid or has expired. If you requested multiple links, use the most recent one.";
                return Page();
            }

            // Token matches — verify/activate and ensure passcode exists
            player.IsEmailVerified = true;
            player.IsActive = true;
            player.VerificationToken = null;
            player.VerificationSentAt = null;

            if (string.IsNullOrWhiteSpace(player.PasscodePlaintext))
            {
                var plain = Passcode.Generate();
                var (hash, salt, algo, cost) = Passcode.Hash(plain);

                player.PasscodePlaintext = plain;
                player.PasscodeHash = hash;
                player.PasscodeSalt = salt;
                player.PasscodeAlgo = algo;
                player.PasscodeCost = cost;
                player.PasscodeSetAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            Verified = true;
            Message = "Your email is verified! You're in.";
            return Page();
        }
    }
}
