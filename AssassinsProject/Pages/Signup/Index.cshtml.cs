using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
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
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly LinkGenerator _linkGenerator;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            AppDbContext db,
            IEmailSender emailSender,
            LinkGenerator linkGenerator,
            ILogger<IndexModel> logger)
        {
            _db = db;
            _emailSender = emailSender;
            _linkGenerator = linkGenerator;
            _logger = logger;
        }

        // You can set this from the querystring or UI; default to the first game if present.
        [BindProperty(SupportsGet = true)]
        public int? GameId { get; set; }

        // Form fields (kept in sync with your .cshtml)
        [BindProperty, Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty, Required, StringLength(100)]
        public string RealName { get; set; } = string.Empty;

        [BindProperty, Required, StringLength(100)]
        public string Alias { get; set; } = string.Empty;

        [BindProperty]
        public int? ApproximateAge { get; set; }

        [BindProperty]
        public string? EyeColor { get; set; }

        [BindProperty]
        public string? HairColor { get; set; }

        [BindProperty]
        public string? VisibleMarkings { get; set; }

        [BindProperty]
        public string? Specialty { get; set; }

        [BindProperty]
        public IFormFile? Photo { get; set; }

        public bool IsSignupOpen { get; private set; } = true;

        public async Task OnGetAsync()
        {
            if (!GameId.HasValue)
            {
                var firstGame = await _db.Games.OrderBy(g => g.Id).FirstOrDefaultAsync();
                GameId = firstGame?.Id;
            }

            // Optional: reflect roster lock/open status if your Game has that flag
            if (GameId.HasValue)
            {
                var game = await _db.Games.FindAsync(GameId.Value);
                if (game != null)
                {
                    // If you track IsSignupOpen, use it; otherwise leave true.
                    var prop = game.GetType().GetProperty("IsSignupOpen");
                    if (prop != null && prop.PropertyType == typeof(bool))
                    {
                        IsSignupOpen = (bool)(prop.GetValue(game) ?? false);
                    }
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            if (!GameId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "No game selected.");
                return Page();
            }

            var game = await _db.Games.FindAsync(GameId.Value);
            if (game == null)
            {
                ModelState.AddModelError(string.Empty, "Game not found.");
                return Page();
            }

            var emailNormalized = Email.Trim().ToUpperInvariant();

            // Ensure alias trimmed
            var alias = (Alias ?? string.Empty).Trim();

            // If duplicate (GameId, Email) exists, update their details and resend verification if needed.
            var player = await _db.Players
                .FirstOrDefaultAsync(p => p.GameId == GameId.Value && p.EmailNormalized == emailNormalized);

            if (player == null)
            {
                player = new Player
                {
                    GameId = GameId.Value,
                    Email = Email.Trim(),
                    EmailNormalized = emailNormalized,
                    DisplayName = alias,     // keep DisplayName aligned with Alias initially
                    RealName = RealName.Trim(),
                    Alias = alias,
                    IsActive = false,        // locked until verified
                    Points = 0,
                    ApproximateAge = ApproximateAge,
                    EyeColor = EyeColor,
                    HairColor = HairColor,
                    Specialty = Specialty,
                    VisibleMarkings = VisibleMarkings,
                    IsEmailVerified = false
                };

                // Minimal passcode fields (required by schema)
                if (player.PasscodeHash == null || player.PasscodeHash.Length == 0)
                    player.PasscodeHash = Array.Empty<byte>();
                if (player.PasscodeSalt == null || player.PasscodeSalt.Length == 0)
                    player.PasscodeSalt = Array.Empty<byte>();
                if (string.IsNullOrWhiteSpace(player.PasscodeAlgo))
                    player.PasscodeAlgo = "argon2id";
                if (player.PasscodeCost <= 0)
                    player.PasscodeCost = 3;

                _db.Players.Add(player);
            }
            else
            {
                // Update editable fields on re-signup
                player.RealName = RealName.Trim();
                player.Alias = alias;
                player.DisplayName = alias;
                player.ApproximateAge = ApproximateAge;
                player.EyeColor = EyeColor;
                player.HairColor = HairColor;
                player.Specialty = Specialty;
                player.VisibleMarkings = VisibleMarkings;
                // keep IsActive gated until verified
            }

            // Optional: handle Photo upload via your FileStorageService elsewhere.

            // Create a cryptographically strong token
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            player.VerificationToken = token;
            player.VerificationSentAt = DateTimeOffset.UtcNow;
            player.IsEmailVerified = false;
            player.IsActive = false;

            await _db.SaveChangesAsync();

            // Build absolute verification URL
            var verifyUrl = _linkGenerator.GetUriByPage(
                HttpContext,
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

            await _emailSender.SendAsync(player.Email, subject, body);

            TempData["SignupMessage"] = "Check your email for a verification link.";
            return RedirectToPage("/Signup/Verify", new { gameId = player.GameId, email = player.Email });
        }
    }
}
