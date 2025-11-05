using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
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

        // incoming from /Signup/Verify?gameId=...&email=...&token=...
        [BindProperty(SupportsGet = true)] public int GameId { get; set; }
        [BindProperty(SupportsGet = true)] public string Email { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true)] public string Token { get; set; } = string.Empty;

        // for the view
        public bool Verified { get; set; }
        public string Message { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            // Get game
            var game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == GameId);
            if (game is null)
            {
                Verified = false;
                Message = "Game not found.";
                return Page();
            }

            // Find the player row by (GameId, Email)
            var emailNorm = (Email ?? string.Empty).Trim().ToUpperInvariant();
            var player = await _db.Players.FirstOrDefaultAsync(p =>
                p.GameId == GameId && p.EmailNormalized == emailNorm);

            if (player is null)
            {
                Verified = false;
                Message = "Player not found for this game.";
                return Page();
            }

            // Validate token
            if (string.IsNullOrWhiteSpace(Token) || !string.Equals(Token, player.VerificationToken, StringComparison.Ordinal))
            {
                Verified = false;
                Message = "Invalid or missing verification token.";
                return Page();
            }

            // Mark verified and allow them to appear in game
            player.IsEmailVerified = true;
            player.IsActive = true;             // now they will show up
            player.VerificationToken = null;    // burn the token
            await _db.SaveChangesAsync();

            Verified = true;
            Message = "Your email has been verified. You're in!";

            // Send their “details” email right away (subject includes game name, includes photo)
            await SendDetailsEmailAsync(game, player);

            return Page();
        }

        private async Task SendDetailsEmailAsync(Game game, Player player)
        {
            var gameName = game?.Name ?? $"Game #{player.GameId}";
            var subject = $"{gameName} Details - Verification Successful";

            // Build absolute photo URL if we stored a relative path
            string? absolutePhotoUrl = player.PhotoUrl;
            if (!string.IsNullOrWhiteSpace(absolutePhotoUrl) &&
                Uri.TryCreate(absolutePhotoUrl, UriKind.Relative, out _))
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                absolutePhotoUrl = baseUrl.TrimEnd('/') + absolutePhotoUrl;
            }

            // Plaintext (safe fallback)
            var text = new StringBuilder()
                .AppendLine($"You're verified for {gameName}. Here are your submitted details:")
                .AppendLine()
                .AppendLine($"Email: {player.Email}")
                .AppendLine($"Real Name: {player.RealName}")
                .AppendLine($"Display Name: {player.DisplayName}")
                .AppendLine($"Alias: {player.Alias}")
                .AppendLine($"Approximate Age: {(player.ApproximateAge?.ToString() ?? "N/A")}")
                .AppendLine($"Hair Color: {player.HairColor ?? "N/A"}")
                .AppendLine($"Eye Color: {player.EyeColor ?? "N/A"}")
                .AppendLine($"Visible Markings: {player.VisibleMarkings ?? "N/A"}")
                .AppendLine($"Specialty: {player.Specialty ?? "N/A"}")
                .AppendLine()
                .AppendLine($"Photo: {(string.IsNullOrWhiteSpace(absolutePhotoUrl) ? "N/A" : absolutePhotoUrl)}")
                .AppendLine()
                .AppendLine("If something looks off, reply to this email or contact the organizer.")
                .ToString();

            static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

            // Lightweight HTML block appended after plaintext so most clients render nicely
            var html = new StringBuilder()
                .AppendLine($"<h3>You're verified for {H(gameName)}.</h3>")
                .AppendLine("<p>Here are your submitted details:</p>")
                .AppendLine("<ul>")
                .AppendLine($"  <li><strong>Email:</strong> {H(player.Email)}</li>")
                .AppendLine($"  <li><strong>Real Name:</strong> {H(player.RealName)}</li>")
                .AppendLine($"  <li><strong>Display Name:</strong> {H(player.DisplayName)}</li>")
                .AppendLine($"  <li><strong>Alias:</strong> {H(player.Alias)}</li>")
                .AppendLine($"  <li><strong>Approximate Age:</strong> {H(player.ApproximateAge?.ToString() ?? "N/A")}</li>")
                .AppendLine($"  <li><strong>Hair Color:</strong> {H(player.HairColor ?? "N/A")}</li>")
                .AppendLine($"  <li><strong>Eye Color:</strong> {H(player.EyeColor ?? "N/A")}</li>")
                .AppendLine($"  <li><strong>Visible Markings:</strong> {H(player.VisibleMarkings ?? "N/A")}</li>")
                .AppendLine($"  <li><strong>Specialty:</strong> {H(player.Specialty ?? "N/A")}</li>")
                .AppendLine("</ul>");

            if (!string.IsNullOrWhiteSpace(absolutePhotoUrl))
            {
                var safe = H(absolutePhotoUrl);
                html.AppendLine("<p><strong>Photo:</strong></p>");
                html.AppendLine($"<p><img src=\"{safe}\" alt=\"Your submitted photo\" style=\"max-width:320px;height:auto;border-radius:8px;border:1px solid #ddd\" /></p>");
                html.AppendLine($"<p><a href=\"{safe}\">Open full-size photo</a></p>");
            }
            else
            {
                html.AppendLine("<p><strong>Photo:</strong> N/A</p>");
            }

            html.AppendLine("<p>If something looks off, reply to this email or contact the organizer.</p>");

            // Combine (keeps IEmailSender simple)
            var combined = new StringBuilder()
                .AppendLine(text)
                .AppendLine()
                .AppendLine("-----")
                .AppendLine("(If your mail client supports HTML, the details and your photo appear below.)")
                .AppendLine("-----")
                .AppendLine()
                .AppendLine(html.ToString())
                .ToString();

            await _email.SendAsync(player.Email, subject, combined);
        }
    }
}
