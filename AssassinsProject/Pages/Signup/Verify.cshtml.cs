using System;
using System.Threading;
using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Utilities;
using AssassinsProject.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Signup
{
    public class VerifyModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _email;

        public VerifyModel(AppDbContext db, IEmailSender email)
        {
            _db = db;
            _email = email;
        }

        // Query params
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Email { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        // Data for the view
        public Game? Game { get; set; }
        public bool Verified { get; set; }
        public string? Message { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
        {
            // Basic guard rails
            if (GameId <= 0 || string.IsNullOrWhiteSpace(Email))
            {
                Verified = false;
                Message = "Invalid or missing verification token.";
                return Page();
            }

            Game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == GameId, ct);
            if (Game is null)
            {
                Verified = false;
                Message = "We couldn't find that game.";
                return Page();
            }

            var norm = EmailNormalizer.Normalize(Email);
            var player = await _db.Players.FirstOrDefaultAsync(
                p => p.GameId == GameId && p.EmailNormalized == norm, ct);

            if (player is null)
            {
                Verified = false;
                Message = "We couldn't find a signup for that email in this game.";
                return Page();
            }

            // If already verified, be friendly and idempotent.
            if (player.IsEmailVerified)
            {
                Verified = true;
                Message = "You're already verified and in the game. See you on the field!";
                return Page();
            }

            // Must have a current token and it must match this link
            if (string.IsNullOrWhiteSpace(Token) ||
                string.IsNullOrWhiteSpace(player.VerificationToken) ||
                !string.Equals(Token, player.VerificationToken, StringComparison.Ordinal))
            {
                Verified = false;
                Message = "This verification link is invalid or has expired. If you requested multiple links, use the most recent one.";
                return Page();
            }

            // ✅ Token ok — verify the player
            player.IsEmailVerified = true;
            player.IsActive = true;               // becomes active once verified (game still needs to start)
            player.VerificationToken = null;      // invalidate the link
            player.VerificationSentAt = null;

            // Ensure the player has a passcode (and a proper hash)
            if (string.IsNullOrWhiteSpace(player.PasscodePlaintext) ||
                player.PasscodeHash == null || player.PasscodeHash.Length == 0)
            {
                var plain = player.PasscodePlaintext ?? Passcode.Generate();
                var (hash, salt, algo, cost) = Passcode.Hash(plain);

                player.PasscodePlaintext = plain;
                player.PasscodeHash = hash;
                player.PasscodeSalt = salt;
                player.PasscodeAlgo = algo;
                player.PasscodeCost = cost;
                player.PasscodeSetAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            // Fire-and-forget a confirmation email (don’t block the page if it fails)
            try
            {
                var subject = $"{Game.Name} – Email Verified";
                var html = $@"
<p>You're verified for <strong>{Game.Name}</strong>!</p>
<p>The game may not be started yet. When it does, you'll receive your target by email.</p>
<p><em>Keep your passcode safe — you'll need it when reporting eliminations.</em></p>";
                await _email.SendAsync(player.Email, subject, html, ct);
            }
            catch
            {
                // swallow — verification has already succeeded
            }

            Verified = true;
            Message = "Your email is verified! You're in.";
            return Page();
        }
    }
}
