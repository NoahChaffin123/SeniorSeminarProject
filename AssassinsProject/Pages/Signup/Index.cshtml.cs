using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using AssassinsProject.Services.Email;
using AssassinsProject.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssassinsProject.Pages.Signup
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly FileStorageService _files;
        private readonly IEmailSender _email;
        private readonly ILogger<IndexModel> _log;
        private readonly LinkGenerator _linkGenerator;

        public IndexModel(
            AppDbContext db,
            FileStorageService files,
            IEmailSender email,
            ILogger<IndexModel> log,
            LinkGenerator linkGenerator)
        {
            _db = db;
            _files = files;
            _email = email;
            _log = log;
            _linkGenerator = linkGenerator;
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

        [BindProperty] public IFormFile? Photo { get; set; }   // REQUIRED (validated below)

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

            // Photo is REQUIRED — capture robustly
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
                ModelState.AddModelError(nameof(Photo), "Please choose a player photo.");
            }

            // Backfill DisplayName when empty
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = !string.IsNullOrWhiteSpace(RealName)
                    ? RealName.Trim()
                    : (Alias ?? string.Empty).Trim();
                ModelState.Remove(nameof(DisplayName)); // clear binder error if any
            }

            if (!ModelState.IsValid)
            {
                foreach (var kv in ModelState)
                    foreach (var err in kv.Value.Errors)
                        _log.LogWarning("Signup validation error: key={Key} error={Error}", kv.Key, err.ErrorMessage);

                Game = game;
                return Page();
            }

            // Normalize email
            var emailNorm = EmailNormalizer.Normalize(Email);

            // Upsert player — keep hidden until verified
            var player = await _db.Players.SingleOrDefaultAsync(
                p => p.GameId == GameId && p.EmailNormalized == emailNorm, ct);

            if (player == null)
            {
                player = new Player
                {
                    GameId = GameId,
                    Email = Email.Trim(),
                    EmailNormalized = emailNorm,
                    DisplayName = DisplayName.Trim(),
                    Alias = Alias.Trim(),
                    RealName = RealName.Trim(),
                    HairColor = HairColor?.Trim(),
                    EyeColor = EyeColor?.Trim(),
                    VisibleMarkings = VisibleMarkings?.Trim(),
                    ApproximateAge = ApproximateAge,
                    Specialty = Specialty?.Trim(),
                    IsActive = false,
                    IsEmailVerified = false,
                    Points = 0
                };
                _db.Players.Add(player);
            }
            else
            {
                player.DisplayName = DisplayName.Trim();
                player.Alias = Alias.Trim();
                player.RealName = RealName.Trim();
                player.HairColor = HairColor?.Trim();
                player.EyeColor = EyeColor?.Trim();
                player.VisibleMarkings = VisibleMarkings?.Trim();
                player.ApproximateAge = ApproximateAge;
                player.Specialty = Specialty?.Trim();

                // ensure hidden until they verify
                player.IsEmailVerified = false;
                player.IsActive = false;
            }

            // Save REQUIRED photo
            var (url, contentType, sha256) =
                await _files.SavePlayerPhotoAsync(GameId, emailNorm, uploaded!, ct);

            player.PhotoUrl = url;
            player.PhotoContentType = contentType;
            player.PhotoBytesSha256 = sha256;

            // Issue fresh verification token
            player.VerificationToken = Guid.NewGuid().ToString("N");
            player.VerificationSentAt = DateTimeOffset.UtcNow;
            player.IsEmailVerified = false;
            player.IsActive = false;

            await _db.SaveChangesAsync(ct);

            // Build absolute verification URL and send email
            var verifyUrl = _linkGenerator.GetUriByPage(
                httpContext: HttpContext,
                page: "/Signup/Verify",
                values: new { gameId = player.GameId, email = player.Email, token = player.VerificationToken });

            var subject = "Verify your email for Assassins";
            var body = new StringBuilder()
                .AppendLine("Thanks for signing up for Assassins!")
                .AppendLine()
                .AppendLine("Please verify your email to join the game:")
                .AppendLine(verifyUrl)
                .AppendLine()
                .AppendLine("If you didn’t request this, you can ignore this email.")
                .ToString();

            await _email.SendAsync(player.Email, subject, body);

            // Redirect to confirmation page (NOT the game hub)
            return RedirectToPage("/Signup/Sent", new { gameId = GameId, email = player.Email });
        }
    }
}
