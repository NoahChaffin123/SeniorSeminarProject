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
        private readonly IEmailSender _emailSender;

        public VerifyModel(AppDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        // Query params
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Email { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        // What the .cshtml expects
        public bool Verified { get; set; }
        public string? Message { get; set; }

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

            // Already verified -> show success (but don't email again)
            if (player.IsEmailVerified)
            {
                Verified = true;
                Message = "You're already verified and in the game. See you on the field!";
                return Page();
            }

            // Require a matching (latest) token
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

            // Token matches — verify and activate
            player.IsEmailVerified = true;
            player.IsActive = true;
            player.VerificationToken = null;
            player.VerificationSentAt = null;

            // Ensure passcode exists
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

            // Fire-and-forget a simple confirmation email
            try
            {
                var subject = $"{Game.Name} – Email Verified";
                var html = $@"
<p>If you received this email, this means you are verified. <strong>{Game.Name}</strong>.</p>
<p>Please note that the game has not yet started</p>
<p><em>You will receive your passocde in an email once the game starts. Good luck!</em></p>";

                await _emailSender.SendAsync(player.Email, subject, html, ct);
            }
            catch (Exception)
            {
                // Don't block the page if email fails; verification already succeeded.
            }

            Verified = true;
            Message = "Your email is verified! You're in.";
            return Page();
        }
    }
}
