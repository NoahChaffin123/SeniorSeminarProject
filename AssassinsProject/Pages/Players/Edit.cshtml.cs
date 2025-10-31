using AssassinsProject.Data;
using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Players;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly FileStorageService _storage;

    public EditModel(AppDbContext db, FileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    // Route/key
    [BindProperty(SupportsGet = true)] public int GameId { get; set; }
    [BindProperty(SupportsGet = true)] public string Email { get; set; } = "";

    // Editable fields
    [BindProperty] public string RealName { get; set; } = "";
    [BindProperty] public string Alias { get; set; } = "";
    [BindProperty] public string? HairColor { get; set; }
    [BindProperty] public string? EyeColor { get; set; }
    [BindProperty] public string? VisibleMarkings { get; set; }
    [BindProperty] public int? ApproximateAge { get; set; }
    [BindProperty] public string? Specialty { get; set; }

    // Photo upload
    [BindProperty] public IFormFile? Photo { get; set; }
    public string? CurrentPhotoUrl { get; set; }

    // Display-only (admin can see, not edit)
    public string PasscodePlaintextDisplay { get; set; } = "";

    // Non-editable helpers
    public string DisplayName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
            return NotFound();

        var player = await _db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.GameId == GameId && p.Email == Email);

        if (player is null) return NotFound();

        // Populate form fields
        RealName = player.RealName ?? player.DisplayName ?? "";
        Alias = player.Alias ?? "";
        HairColor = player.HairColor;
        EyeColor = player.EyeColor;
        VisibleMarkings = player.VisibleMarkings;
        ApproximateAge = player.ApproximateAge;
        Specialty = player.Specialty;
        CurrentPhotoUrl = player.PhotoUrl;
        DisplayName = player.DisplayName ?? player.Email;

        // Admin can view plain passcode if stored
        PasscodePlaintextDisplay = string.IsNullOrWhiteSpace(player.PasscodePlaintext)
            ? "(not set)"
            : player.PasscodePlaintext;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
            return NotFound();

        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.GameId == GameId && p.Email == Email);

        if (player is null) return NotFound();

        // Basic validation
        if (string.IsNullOrWhiteSpace(RealName))
            ModelState.AddModelError(nameof(RealName), "Real Name is required.");
        if (string.IsNullOrWhiteSpace(Alias))
            ModelState.AddModelError(nameof(Alias), "Alias is required.");

        if (!ModelState.IsValid)
        {
            // Rehydrate display-only props for redisplay
            CurrentPhotoUrl = player.PhotoUrl;
            PasscodePlaintextDisplay = string.IsNullOrWhiteSpace(player.PasscodePlaintext)
                ? "(not set)"
                : player.PasscodePlaintext;
            DisplayName = player.DisplayName ?? player.Email;
            return Page();
        }

        // Update editable fields
        player.RealName = RealName;
        player.Alias = Alias;
        player.HairColor = HairColor;
        player.EyeColor = EyeColor;
        player.VisibleMarkings = VisibleMarkings;
        player.ApproximateAge = ApproximateAge;
        player.Specialty = Specialty;

        // If a new photo was uploaded, save it and update URL/content-type
        if (Photo is not null && Photo.Length > 0)
        {
            var saved = await _storage.SavePlayerPhotoAsync(GameId, player.EmailNormalized, Photo);
            player.PhotoUrl = saved.url;
            player.PhotoContentType = saved.contentType;
            // Removed: player.PhotoSha256 (your model doesn't have this property)
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Likely unique index violation on (GameId, Alias)
            if (ex.InnerException?.Message.Contains("IX_Players_GameId_Alias", StringComparison.OrdinalIgnoreCase) == true)
            {
                ModelState.AddModelError(nameof(Alias), "That alias is already in use in this game.");
                // Rehydrate display-only props for redisplay
                CurrentPhotoUrl = player.PhotoUrl;
                PasscodePlaintextDisplay = string.IsNullOrWhiteSpace(player.PasscodePlaintext)
                    ? "(not set)"
                    : player.PasscodePlaintext;
                DisplayName = player.DisplayName ?? player.Email;
                return Page();
            }
            throw;
        }

        return RedirectToPage("/Games/Details", new { id = GameId });
    }
}
