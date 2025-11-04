using System.Threading.Tasks;
using AssassinsProject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Signup
{
    public class VerifyModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<VerifyModel> _logger;

        public VerifyModel(AppDbContext db, ILogger<VerifyModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Email { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public bool Verified { get; private set; }
        public string Message { get; private set; } = string.Empty;

        public async Task OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                Message = "Missing email.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Token))
            {
                // We may land here after redirect from signup, before clicking the email link.
                Message = "A verification link was sent to your email.";
                return;
            }

            var emailNormalized = Email.Trim().ToUpperInvariant();

            var player = await _db.Players
                .FirstOrDefaultAsync(p => p.GameId == GameId &&
                                          p.EmailNormalized == emailNormalized);

            if (player == null)
            {
                Message = "Player not found.";
                return;
            }

            if (player.IsEmailVerified)
            {
                Verified = true;
                Message = "Your email is already verified. You can join/play now.";
                return;
            }

            if (player.VerificationToken == Token)
            {
                player.IsEmailVerified = true;
                player.IsActive = true; // Admit into game upon verification
                player.VerificationToken = null;
                player.VerificationSentAt = null;

                await _db.SaveChangesAsync();

                Verified = true;
                Message = "Email verified! You’re in 🎉";
            }
            else
            {
                Message = "Invalid or expired verification link.";
            }
        }
    }
}
