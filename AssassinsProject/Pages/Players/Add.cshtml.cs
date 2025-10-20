using AssassinsProject.Data;
using AssassinsProject.Services;
using AssassinsProject.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Players;

public class AddModel : PageModel
{
    private readonly GameService _svc;
    private readonly FileStorageService _storage;
    private readonly AppDbContext _db;

    public AddModel(GameService svc, FileStorageService storage, AppDbContext db)
    {
        _svc = svc;
        _storage = storage;
        _db = db;
    }

    [BindProperty(SupportsGet = true)] public int GameId { get; set; }

    public bool IsSignupOpen { get; set; } = true;

    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string RealName { get; set; } = "";
    [BindProperty] public string Alias { get; set; } = "";
    [BindProperty] public string? HairColor { get; set; }
    [BindProperty] public string? EyeColor { get; set; }
    [BindProperty] public string? VisibleMarkings { get; set; }
    [BindProperty] public int? ApproximateAge { get; set; }
    [BindProperty] public string? Specialty { get; set; }

    [BindProperty] public IFormFile? Photo { get; set; }

    [TempData] public string? NewPasscode { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var g = await _db.Games.FindAsync(GameId);
        if (g is null) return NotFound();
        if (g.Status != Models.GameStatus.Setup)
            return BadRequest("This game is not accepting additions (already started).");

        IsSignupOpen = g.IsSignupOpen;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var g = await _db.Games.FindAsync(GameId);
        if (g is null) return NotFound();

        if (g.Status != Models.GameStatus.Setup || !g.IsSignupOpen)
        {
            ModelState.AddModelError(string.Empty, "Signups are currently closed for this game.");
            IsSignupOpen = g.IsSignupOpen;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Email))
            ModelState.AddModelError(nameof(Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(RealName))
            ModelState.AddModelError(nameof(RealName), "Real Name is required.");
        if (string.IsNullOrWhiteSpace(Alias))
            ModelState.AddModelError(nameof(Alias), "Alias is required.");

        var norm = EmailNormalizer.Normalize(Email);
        if (!norm.EndsWith("@hendrix.edu"))
            ModelState.AddModelError(nameof(Email), "Must be a @hendrix.edu email.");

        if (!ModelState.IsValid)
        {
            IsSignupOpen = g.IsSignupOpen;
            return Page();
        }

        string? url = null, contentType = null;
        byte[]? sha = null;

        if (Photo is not null && Photo.Length > 0)
        {
            var saved = await _storage.SavePlayerPhotoAsync(GameId, norm, Photo);
            url = saved.url; contentType = saved.contentType; sha = saved.sha256;
        }

        try
        {
            var (player, passcode) = await _svc.AddPlayerAdminAsync(
                gameId: GameId,
                email: Email,
                realName: RealName,
                alias: Alias,
                hairColor: HairColor,
                eyeColor: EyeColor,
                visibleMarkings: VisibleMarkings,
                approximateAge: ApproximateAge,
                specialty: Specialty,
                photoUrl: url,
                contentType: contentType,
                photoSha256: sha
            );

            NewPasscode = passcode;
            return RedirectToPage("/Players/Added", new { gameId = GameId, email = player.Email });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            IsSignupOpen = g.IsSignupOpen;
            return Page();
        }
    }
}
