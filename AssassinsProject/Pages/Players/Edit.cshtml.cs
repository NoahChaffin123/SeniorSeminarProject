using AssassinsProject.Data;
using AssassinsProject.Services;
using AssassinsProject.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Pages.Players;

public class EditModel(AppDbContext db, FileStorageService storage) : PageModel
{
    private readonly AppDbContext _db = db;
    private readonly FileStorageService _storage = storage;

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

    [BindProperty] public IFormFile? Photo { get; set; }

    // Read-only display
    public string? CurrentPhotoUrl { get; set; }
    public string? PasscodePlaintext { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var p = await _db.Players.SingleOrDefaultAsync(x => x.GameId == GameId && x.Email == Email);
        if (p is null) return NotFound();

        RealName = p.RealName;
        Alias = p.Alias;
        HairColor = p.HairColor;
        EyeColor = p.EyeColor;
        VisibleMarkings = p.VisibleMarkings;
        ApproximateAge = p.ApproximateAge;
        Specialty = p.Specialty;

        CurrentPhotoUrl = p.PhotoUrl;
        PasscodePlaintext = p.PasscodePlaintext; // may be null for older records

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var p = await _db.Players.SingleOrDefaultAsync(x => x.GameId == GameId && x.Email == Email);
        if (p is null) return NotFound();

        if (string.IsNullOrWhiteSpace(RealName))
            ModelState.AddModelError(nameof(RealName), "Real name is required.");
        if (string.IsNullOrWhiteSpace(Alias))
            ModelState.AddModelError(nameof(Alias), "Alias is required.");

        // Alias uniqueness check if changed
        if (!string.Equals(p.Alias, Alias, StringComparison.Ordinal))
        {
            var taken = await _db.Players.AnyAsync(x => x.GameId == GameId && x.Alias == Alias && x.Email != p.Email);
            if (taken)
                ModelState.AddModelError(nameof(Alias), "That alias is already taken in this game.");
        }

        if (!ModelState.IsValid)
        {
            CurrentPhotoUrl = p.PhotoUrl;
            PasscodePlaintext = p.PasscodePlaintext;
            return Page();
        }

        p.RealName = RealName;
        p.Alias = Alias;
        p.DisplayName = Alias; // keep display name in sync with alias
        p.HairColor = HairColor;
        p.EyeColor = EyeColor;
        p.VisibleMarkings = VisibleMarkings;
        p.ApproximateAge = ApproximateAge;
        p.Specialty = Specialty;

        if (Photo is not null && Photo.Length > 0)
        {
            var norm = EmailNormalizer.Normalize(Email);
            var saved = await _storage.SavePlayerPhotoAsync(GameId, norm, Photo);
            p.PhotoUrl = saved.url;
            p.PhotoContentType = saved.contentType;
            p.PhotoBytesSha256 = saved.sha256;
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("/Games/Details", new { id = GameId });
    }
}
