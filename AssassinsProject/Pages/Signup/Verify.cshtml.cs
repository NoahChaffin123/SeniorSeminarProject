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

        // What the .cshtml expects (used only when we actually render this page)
        public bool Verified { get; set; }
        public string? Message { get; set; }

        public Game? Game { get; set; }

        private IActionResult RedirectToSuccess(string email) =>
            RedirectToPage("/Signup/Sent", new { gameId = GameId, email });

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

            // ✅ Already verified -> send them to the same success page used for fresh verifications
            if (player.IsEmailVerified)
                return RedirectToSuccess(player.Email);

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

            // Fire-and-forget a simple confirmation email (non-blocking)
            try
            {
                var subject = $"{Game.Name} – Email Verified";
                var html = $@"
<p>If you received this email, this means you are verified for <strong>{Game.Name}</strong>.</p>
<p>Please note that the game has not yet started.</p>
<p><em>You will receive your passcode in an email once the game starts. Good luck!</em></p>";
                await _emailSender.SendAsync(player.Email, subject, html, ct);
            }
            catch
            {
                // Don’t block the flow if email fails; verification already succeeded.
            }

            // ✅ Always land on the same success page
            return RedirectToSuccess(player.Email);
        }
    }
}
