using AssassinsProject.Data;
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

        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        public string? Message { get; private set; }
        public bool Success { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (GameId <= 0 || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token))
            {
                Message = "Invalid verification link.";
                Success = false;
                return Page();
            }

            var emailNorm = Email.Trim().ToLowerInvariant();

            var player = await _db.Players
                .FirstOrDefaultAsync(p => p.GameId == GameId && p.EmailNormalized == emailNorm);

            if (player is null)
            {
                Message = "No signup found for this email.";
                Success = false;
                return Page();
            }

            if (player.EmailVerifyToken != Token)
            {
                Message = "Verification token is invalid.";
                Success = false;
                return Page();
            }

            if (player.EmailVerifyTokenExpiresAt is DateTimeOffset exp && exp < DateTimeOffset.UtcNow)
            {
                Message = "Verification link has expired. Please sign up again.";
                Success = false;
                return Page();
            }

            // Activate player
            player.IsActive = true;
            player.EmailVerifiedAt = DateTimeOffset.UtcNow;
            player.EmailVerifyToken = null;
            player.EmailVerifyTokenExpiresAt = null;

            await _db.SaveChangesAsync();

            Success = true;
            Message = "Your email has been verified. You’re in!";
            return Page();
        }
    }
}
