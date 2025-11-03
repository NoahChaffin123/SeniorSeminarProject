using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Signup
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;
        private readonly HtmlEncoder _html;

        public IndexModel(AppDbContext db, IEmailSender emailSender, IConfiguration config, HtmlEncoder html)
        {
            _db = db;
            _emailSender = emailSender;
            _config = config;
            _html = html;
        }

        // The page/markup expects this as ?gameId=#
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        // Used by the Razor page to enable/disable the form
        public bool IsSignupOpen { get; private set; } = true;

        // These properties match asp-for fields in Index.cshtml
        [BindProperty, Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [BindProperty, Required] public string RealName { get; set; } = string.Empty;
        [BindProperty, Required] public string Alias { get; set; } = string.Empty;

        [BindProperty] public string? HairColor { get; set; }
        [BindProperty] public string? EyeColor { get; set; }
        [BindProperty] public string? VisibleMarkings { get; set; }
        [BindProperty] public int?    ApproximateAge { get; set; }
        [BindProperty] public string? Specialty { get; set; }

        [BindProperty] public IFormFile? Photo { get; set; } // if your page has this field

        public string? SuccessMessage { get; private set; }
        public string? ErrorMessage { get; private set; }
        public Game? Game { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (GameId <= 0)
            {
                ErrorMessage = "Missing game id.";
                return Page();
            }

            Game = await _db.Games.FirstOrDefaultAsync(g => g.Id == GameId);
            if (Game is null)
            {
                ErrorMessage = "Game not found.";
                return Page();
            }

            IsSignupOpen = Game.IsSignupOpen;
            if (!IsSignupOpen)
            {
                ErrorMessage = "Signups are currently closed for this game.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Basic model validation
            if (!ModelState.IsValid)
            {
                await LoadGameAsync();
                return Page();
            }

            await LoadGameAsync();
            if (Game is null)
            {
                ModelState.AddModelError(string.Empty, "Game not found.");
                return Page();
            }

            IsSignupOpen = Game.IsSignupOpen;
            if (!IsSignupOpen)
            {
                ModelState.AddModelError(string.Empty, "Signups are currently closed for this game.");
                return Page();
            }

            var email = Email.Trim();
            var emailNorm = email.ToLowerInvariant();
            var alias = Alias.Trim();

            // App-level alias uniqueness (DB has a unique index too)
            var aliasInUse = await _db.Players.AnyAsync(p =>
                p.GameId == GameId &&
                p.Alias.ToLower() == alias.ToLower());

            if (aliasInUse)
            {
                ModelState.AddModelError(nameof(Alias), "That alias is already taken in this game.");
                return Page();
            }

            // Upsert a pending player
            var player = await _db.Players.FirstOrDefaultAsync(p => p.GameId == GameId && p.EmailNormalized == emailNorm);
            if (player is null)
            {
                player = new Player
                {
                    GameId = GameId,
                    Email = email,
                    EmailNormalized = emailNorm,
                    DisplayName = alias,    // or a separate DisplayName field if your view uses one
                    RealName = RealName.Trim(),
                    Alias = alias,
                    HairColor = HairColor,
                    EyeColor = EyeColor,
                    VisibleMarkings = VisibleMarkings,
                    ApproximateAge = ApproximateAge,
                    Specialty = Specialty,
                    IsActive = false,
                    PasscodeAlgo = "PBKDF2-SHA256",
                    PasscodeCost = 100_000,
                    PasscodeSetAt = DateTimeOffset.UtcNow
                };
                _db.Players.Add(player);
            }
            else
            {
                player.RealName = RealName.Trim();
                player.Alias = alias;
                player.HairColor = HairColor;
                player.EyeColor = EyeColor;
                player.VisibleMarkings = VisibleMarkings;
                player.ApproximateAge = ApproximateAge;
                player.Specialty = Specialty;
                player.IsActive = false; // still pending until email verification
            }

            // Generate/assign verification token
            player.EmailVerifyToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            player.EmailVerifyTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
            player.EmailVerifiedAt = null;

            await _db.SaveChangesAsync();

            // Build absolute verification URL
            var baseUrl = _config["App:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
            }

            var verifyUrl =
                $"{baseUrl}/Signup/Verify?gameId={GameId}" +
                $"&email={Uri.EscapeDataString(email)}" +
                $"&token={Uri.EscapeDataString(player.EmailVerifyToken)}";

            var subject = $"Verify your email for {Game.Name}";
            var body = $@"
<p>Hi {_html.Encode(RealName)},</p>
<p>Please verify your email to join <strong>{_html.Encode(Game.Name)}</strong>.</p>
<p><a href=""{verifyUrl}"">Click here to verify your email</a></p>
<p>This link expires at {player.EmailVerifyTokenExpiresAt:u}.</p>";

            await _emailSender.SendAsync(email, subject, body);

            SuccessMessage = "Thanks! We sent a verification link to your email. Click it within 24 hours to be admitted into the game.";
            ModelState.Clear();

            // Keep Game data for the page after POST
            await LoadGameAsync();
            return Page();
        }

        private async Task LoadGameAsync()
        {
            if (Game is null && GameId > 0)
            {
                Game = await _db.Games.FirstOrDefaultAsync(g => g.Id == GameId);
            }
        }
    }
}
