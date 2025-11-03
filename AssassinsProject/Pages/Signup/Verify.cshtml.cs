using AssassinsProject.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Signup;

public class VerifyModel : PageModel
{
    private readonly AppDbContext _db;
    public VerifyModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int GameId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = "";

    public bool Success { get; private set; }
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (GameId <= 0 || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token))
        {
            Success = false;
            Message = "Invalid verification link.";
            return Page();
        }

        var emailNorm = Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.GameId == GameId && p.EmailNormalized == emailNorm);

        if (player is null)
        {
            Success = false;
            Message = "No signup found for this email.";
            return Page();
        }

        if (player.EmailVerifiedAt is not null)
        {
            Success = true;
            Message = "This email is already verified.";
            return Page();
        }

        if (player.EmailVerifyToken is null ||
            !TimeConstantEquals(player.EmailVerifyToken, Token) ||
            player.EmailVerifyTokenExpiresAt is null ||
            now > player.EmailVerifyTokenExpiresAt.Value)
        {
            Success = false;
            Message = "Verification link is invalid or expired.";
            return Page();
        }

        // Mark verified + activate the player.
        player.EmailVerifiedAt = now;
        player.EmailVerifyToken = null;
        player.EmailVerifyTokenExpiresAt = null;
        player.IsActive = true;

        await _db.SaveChangesAsync();

        Success = true;
        Message = "Email verified! You are now in the game.";
        return Page();
    }

    // Constant-time compare to avoid timing leaks on tokens
    private static bool TimeConstantEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
