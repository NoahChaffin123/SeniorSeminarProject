using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using AssassinsProject.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssassinsProject.Pages.Games;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly GameService _svc;
    private readonly IEmailSender _email;
    private readonly ILogger<DetailsModel> _log;

    public DetailsModel(AppDbContext db, GameService svc, IEmailSender email, ILogger<DetailsModel> log)
    {
        _db = db;
        _svc = svc;
        _email = email;
        _log = log;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Game? Game { get; set; }

    // Players
    public List<Player> AllPlayers { get; set; } = new();
    public List<Player> ActivePlayers { get; set; } = new();

    // Scoreboard (active first, then points desc)
    public List<Player> ScoreboardPlayers { get; set; } = new();

    // Fast lookup for rendering "A -> B"
    public Dictionary<string, Player> PlayersByEmail { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // History
    public List<Elimination> Eliminations { get; set; } = new();

    // Share links
    public string? SignupUrl { get; set; }
    public string? ReportUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Id = id;

        Game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (Game is null) return NotFound();

        AllPlayers = await _db.Players
            .Where(p => p.GameId == id)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();

        ActivePlayers = AllPlayers.Where(p => p.IsActive).ToList();

        // Build lookup by email for easy formatting on the view
        PlayersByEmail = AllPlayers.ToDictionary(p => p.Email, p => p, StringComparer.OrdinalIgnoreCase);

        Eliminations = await _db.Eliminations
            .Where(e => e.GameId == id)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync();

        // Scoreboard: active players first, then by points (desc), then name
        ScoreboardPlayers = AllPlayers
            .OrderByDescending(p => p.IsActive)   // true first
            .ThenByDescending(p => p.Points)
            .ThenBy(p => p.DisplayName)
            .ToList();

        // Build absolute URLs for sharing
        SignupUrl = Url.Page(
            pageName: "/Signup/Index",
            pageHandler: null,
            values: new { gameId = id },
            protocol: Request.Scheme,
            host: Request.Host.ToString()
        ) ?? string.Empty;

        ReportUrl = Url.Page(
            pageName: "/Eliminations/Report",
            pageHandler: null,
            values: new { gameId = id },
            protocol: Request.Scheme,
            host: Request.Host.ToString()
        ) ?? string.Empty;

        return Page();
    }

    // Start game
    public async Task<IActionResult> OnPostStartAsync()
    {
        try
        {
            // Start game (assign targets, set StartedAt/Status, etc.)
            await _svc.StartGameAsync(Id);

            var game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == Id);
            if (game != null)
            {
                // Get all verified players after targets have been assigned
                var players = await _db.Players
                    .Where(p => p.GameId == Id && p.IsEmailVerified)
                    .ToListAsync();

                // Build a quick lookup to resolve targets by email
                var byEmail = players.ToDictionary(p => p.Email, StringComparer.OrdinalIgnoreCase);

                foreach (var me in players)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(me.TargetEmail) &&
                            byEmail.TryGetValue(me.TargetEmail, out var target))
                        {
                            var (subject, body) = BuildTargetEmail(game, me, target);
                            await _email.SendAsync(me.Email, subject, body);
                        }
                        else
                        {
                            _log.LogInformation("No target found for player {Email} in Game {GameId} when sending target emails.", me.Email, Id);
                        }
                    }
                    catch (Exception exTarget)
                    {
                        _log.LogError(exTarget, "Failed to send target email to {Email} for Game {GameId}", me.Email, Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Unable to start the game: {ex.Message}");
        }

        return await OnGetAsync(Id);
    }

    // End game (set Completed, close signups, and stamp EndedAt)
    public async Task<IActionResult> OnPostEndAsync()
    {
        var g = await _db.Games.FirstOrDefaultAsync(x => x.Id == Id);
        if (g is null) return NotFound();

        g.Status = GameStatus.Completed;
        g.IsSignupOpen = false;
        g.EndedAt = DateTimeOffset.UtcNow; // stamp an end time

        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostCloseSignupAsync()
    {
        Game? g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        g.IsSignupOpen = false;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    public async Task<IActionResult> OnPostOpenSignupAsync()
    {
        Game? g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        g.IsSignupOpen = true;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    // NEW: Pause the game (only while Active)
    public async Task<IActionResult> OnPostPauseAsync()
    {
        var g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        if (g.Status != GameStatus.Active) return BadRequest("Game is not active.");

        g.IsPaused = true;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    // NEW: Unpause (resume) the game
    public async Task<IActionResult> OnPostUnpauseAsync()
    {
        var g = await _db.Games.FindAsync(Id);
        if (g is null) return NotFound();
        if (g.Status != GameStatus.Active) return BadRequest("Game is not active.");

        g.IsPaused = false;
        await _db.SaveChangesAsync();
        return await OnGetAsync(Id);
    }

    // ---------------------------
    // Helpers
    // ---------------------------
    private (string subject, string body) BuildTargetEmail(Game game, Player me, Player target)
    {
        var gameName = game?.Name ?? $"Game #{Id}";
        var subject = $"{gameName} Target Assignment";

        // Absolute photo URL for the target (if relative, prefix with host)
        string? absolutePhotoUrl = target.PhotoUrl;
        if (!string.IsNullOrWhiteSpace(absolutePhotoUrl) &&
            Uri.TryCreate(absolutePhotoUrl, UriKind.Relative, out _))
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            absolutePhotoUrl = baseUrl.TrimEnd('/') + absolutePhotoUrl;
        }

        // Player's own passcode (do NOT include target's)
        var myPasscode = string.IsNullOrWhiteSpace(me.PasscodePlaintext) ? "(not set)" : me.PasscodePlaintext;

        // Plaintext body
        var text = new StringBuilder()
            .AppendLine($"The game \"{gameName}\" has started.")
            .AppendLine()
            .AppendLine("Your target:")
            .AppendLine($"  Display Name: {target.DisplayName}")
            .AppendLine($"  Alias: {target.Alias}")
            .AppendLine($"  Approximate Age: {(target.ApproximateAge?.ToString() ?? "N/A")}")
            .AppendLine($"  Hair Color: {target.HairColor ?? "N/A"}")
            .AppendLine($"  Eye Color: {target.EyeColor ?? "N/A"}")
            .AppendLine($"  Visible Markings: {target.VisibleMarkings ?? "N/A"}")
            .AppendLine($"  Specialty: {target.Specialty ?? "N/A"}")
            .AppendLine($"  Photo: {(string.IsNullOrWhiteSpace(absolutePhotoUrl) ? "N/A" : absolutePhotoUrl)}")
            .AppendLine()
            .AppendLine($"Your passcode: {myPasscode}")
            .AppendLine()
            .AppendLine("Keep this information confidential. Good luck!")
            .ToString();

        // HTML section (similar lightweight style as earlier emails)
        string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        var html = new StringBuilder()
            .AppendLine($"<h3>Your target for \"{H(gameName)}\"</h3>")
            .AppendLine("<p>Keep this information confidential.</p>")
            .AppendLine("<ul>")
            .AppendLine($"  <li><strong>Display Name:</strong> {H(target.DisplayName)}</li>")
            .AppendLine($"  <li><strong>Alias:</strong> {H(target.Alias)}</li>")
            .AppendLine($"  <li><strong>Approximate Age:</strong> {H(target.ApproximateAge?.ToString() ?? "N/A")}</li>")
            .AppendLine($"  <li><strong>Hair Color:</strong> {H(target.HairColor ?? "N/A")}</li>")
            .AppendLine($"  <li><strong>Eye Color:</strong> {H(target.EyeColor ?? "N/A")}</li>")
            .AppendLine($"  <li><strong>Visible Markings:</strong> {H(target.VisibleMarkings ?? "N/A")}</li>")
            .AppendLine($"  <li><strong>Specialty:</strong> {H(target.Specialty ?? "N/A")}</li>")
            .AppendLine("</ul>");

        if (!string.IsNullOrWhiteSpace(absolutePhotoUrl))
        {
            var safeUrl = H(absolutePhotoUrl);
            html.AppendLine("<p><strong>Photo:</strong></p>");
            html.AppendLine($"<p><img src=\"{safeUrl}\" alt=\"Target photo\" style=\"max-width:320px;height:auto;border-radius:8px;border:1px solid #ddd\" /></p>");
            html.AppendLine($"<p><a href=\"{safeUrl}\">Open full-size photo</a></p>");
        }
        else
        {
            html.AppendLine("<p><strong>Photo:</strong> N/A</p>");
        }

        html.AppendLine($"<p><strong>Your passcode:</strong> {H(myPasscode)}</p>");
        html.AppendLine("<p>Good luck!</p>");

        var combined = new StringBuilder()
            .AppendLine(text)
            .AppendLine()
            .AppendLine("-----")
            .AppendLine("(If your mail client supports HTML, the details and the photo appear below.)")
            .AppendLine("-----")
            .AppendLine()
            .AppendLine(html.ToString())
            .ToString();

        return (subject, combined);
    }
}
