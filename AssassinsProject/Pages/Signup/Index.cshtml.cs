using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using AssassinsProject.Services.Email;
using AssassinsProject.Utilities;

namespace AssassinsProject.Pages.Signup
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly FileStorageService _files;
        private readonly IEmailSender _email;
        private readonly ILogger<IndexModel> _log;

        public IndexModel(AppDbContext db, FileStorageService files, IEmailSender email, ILogger<IndexModel> log)
        {
            _db = db;
            _files = files;
            _email = email;
            _log = log;
        }

        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        public bool IsSignupOpen { get; set; }
        public Game? Game { get; set; }

        // ===== Form fields =====
        [BindProperty, Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty, StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [BindProperty, Required, StringLength(100)]
        public string Alias { get; set; } = string.Empty;

        [BindProperty] public IFormFile? Photo { get; set; }

        [BindProperty, Required, StringLength(100)] public string RealName { get; set; } = string.Empty;
        [BindProperty, StringLength(50)]  public string? HairColor { get; set; }
        [BindProperty, StringLength(50)]  public string? EyeColor { get; set; }
        [BindProperty, StringLength(200)] public string? VisibleMarkings { get; set; }
        [BindProperty, Range(0, 120)]     public int?    ApproximateAge { get; set; }
        [BindProperty, StringLength(200)] public string? Specialty { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Game = await _db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == GameId);
            if (Game is null) return NotFound();

            IsSignupOpen = Game.IsSignupOpen && Game.Status == GameStatus.Setup;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            var game = await _db.Games.SingleOrDefaultAsync(g => g.Id == GameId, ct);
            if (game is null) return NotFound();

            IsSignupOpen = game.IsSignupOpen && game.Status == GameStatus.Setup;
            if (!IsSignupOpen)
            {
                ModelState.AddModelError(string.Empty, "Signups are closed.");
            }

            // --- Domain restriction (left disabled per your comment) ---
            var emailTrimmed = (Email ?? string.Empty).Trim();
            // if (!emailTrimmed.EndsWith("@hendrix.edu", StringComparison.OrdinalIgnoreCase))
            //     ModelState.AddModelError(nameof(Email), "Use your @hendrix.edu email address.");

            // --- Robustly capture the uploaded file (required) ---
            IFormFile? uploaded = Photo;
            if (uploaded is null || uploaded.Length == 0)
            {
                uploaded = Request.Form.Files.FirstOrDefault(f =>
                    f.Name.EndsWith(".Photo", StringComparison.OrdinalIgnoreCase) ||
                    f.Name.Equals("Photo", StringComparison.OrdinalIgnoreCase))
                    ?? Request.Form.Files.FirstOrDefault();
            }
            if (uploaded is null || uploaded.Length == 0)
            {
                ModelState.AddModelError(nameof(Photo), "A picture is required");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = !string.IsNullOrWhiteSpace(RealName)
                    ? RealName.Trim()
                    : (Alias ?? string.Empty).Trim();

                ModelState.Remove(nameof(DisplayName));
            }

            // Bail early on validation errors
            if (!ModelState.IsValid)
            {
                foreach (var kv in ModelState)
                {
                    foreach (var err in kv.Value.Errors)
                        _log.LogWarning("Signup validation error: key={Key} error={Error}", kv.Key, err.ErrorMessage);
                }
                Game = game;
                return Page();
            }

            // Normalize email consistently
            var emailNorm = EmailNormalizer.Normalize(emailTrimmed);

            // Reject duplicates up-front (verified OR unverified)
            var exists = await _db.Players
                .AsNoTracking()
                .AnyAsync(p => p.GameId == GameId && p.EmailNormalized == emailNorm, ct);

            if (exists)
            {
                ModelState.AddModelError(nameof(Email),
                    "This email is already registered for this game. " +
                    "If you need a verification link, check your inbox/spam or contact the organizer.");
                Game = game;
                return Page();
            }

            // Create the new (unverified) player
            var player = new Player
            {
                GameId = GameId,
                Email = emailTrimmed,
                EmailNormalized = emailNorm,
                DisplayName = DisplayName.Trim(),
                Alias = Alias.Trim(),
                RealName = RealName.Trim(),
                HairColor = HairColor?.Trim(),
                EyeColor = EyeColor?.Trim(),
                VisibleMarkings = VisibleMarkings?.Trim(),
                ApproximateAge = ApproximateAge,
                Specialty = Specialty?.Trim(),
                IsActive = false,           // locked until verified
                IsEmailVerified = false,
                Points = 0,
                PasscodeAlgo = "argon2id",
                PasscodeCost = 3,
                PasscodeHash = Array.Empty<byte>(),
                PasscodeSalt = Array.Empty<byte>(),
                PasscodeSetAt = DateTimeOffset.UtcNow
            };
            _db.Players.Add(player);

            // Save REQUIRED photo (now with a proper null guard)
            if (uploaded is null)
            {
                // Should be unreachable due to earlier validation, but guard anyway.
                ModelState.AddModelError(nameof(Photo), "A picture is required");
                Game = game;
                return Page();
            }

            var (url, contentType, sha256) =
                await _files.SavePlayerPhotoAsync(GameId, emailNorm, uploaded, ct);

            player.PhotoUrl = url;
            player.PhotoContentType = contentType;
            player.PhotoBytesSha256 = sha256;

            // Issue verification
            player.VerificationToken = Guid.NewGuid().ToString("N");
            player.VerificationSentAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            // Build absolute verification URL
            var verifyUrl = Url.Page(
                pageName: "/Signup/Verify",
                pageHandler: null,
                values: new { gameId = GameId, email = player.Email, token = player.VerificationToken },
                protocol: Request.Scheme,
                host: Request.Host.ToString()
            ) ?? string.Empty;

            // Subject includes the game name
            var subject = $"{game.Name} Verification Email";

            // HTML body with clickable link + plain text fallback
            var body =
$@"<p>Thanks for signing up for <strong>{game.Name}</strong>!</p>
<p>Please verify your email to join the game:</p>
<p><a href=""{verifyUrl}"">{verifyUrl}</a></p>
<p>If you didn’t request this, you can ignore this email.</p>

----
Plain-text fallback:
{verifyUrl}";

            await _email.SendAsync(player.Email, subject, body);

            TempData["Message"] = "Check your email for a verification link.";
            return RedirectToPage("/Signup/Sent", new { gameId = GameId, email = player.Email });
        }
    }
}
