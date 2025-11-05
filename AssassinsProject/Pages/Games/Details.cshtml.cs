using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services;
using AssassinsProject.Services.Email;
using AssassinsProject.Utilities; // <-- for Passcode.Generate()
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

        PlayersByEmail = AllPlayers.ToDictionary(p => p.Email, p => p, StringComparer.OrdinalIgnoreCase);

        Eliminations = await _db.Eliminations
            .Where(e => e.GameId == id)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync();

        ScoreboardPlayers = AllPlayers
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.Points)
            .ThenBy(p => p.DisplayName)
            .ToList();

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
            // Start/assign via your existing GameService (unchanged)
            await _svc.StartGameAsync(Id);

            var game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == Id);
            if (game != null)
            {
                // Fetch verified participants AFTER StartGame so TargetEmail is set
                var players = await _db.Players
                    .Where(p => p.GameId == Id && p.IsEmailVerified)
                    .ToListAsync();

                // Make sure everyone has a passcode (generate + HASH if missing)
                bool anyUpdated = false;
                foreach (var p in players)
                {
                    if (string.IsNullOrWhiteSpace(p.PasscodePlaintext))
                    {
                        var plain = Passcode.Generate();
                        var (hash, salt, algo, cost) = Passcode.Hash(plain);

                        p.PasscodePlaintext = plain;
                        p.PasscodeSetAt = DateTimeOffset.UtcNow;

                        p.PasscodeAlgo = algo;
                        p.PasscodeCost = cost;
                        p.PasscodeSalt = salt;
                        p.PasscodeHash = hash;

                        anyUpdated = true;
                    }
                }
                if (anyUpdated)
                    await _db.SaveChangesAsync();

                // Send each player their target + their own passcode (no target email/real name/passcode)
                var byEmail = players.ToDictionary(p => p.Email, p => p, StringComparer.OrdinalIgnoreCase);
                foreach (var me in players)
                {
                    Player? target = null;
                    if (!string.IsNullOrWhiteSpace(me.TargetEmail))
                        byEmail.TryGetValue(me.TargetEmail, out target);

                    try
                    {
                        var (subject, body) = BuildTargetEmail(game, me, target);
                        await _email.SendAsync(me.Email, subject, body);
                    }
                    catch (Exception exSend)
                    {
                        _log.LogError(exSend, "Failed to send target email to {Email} for Game {GameId}", me.Email, Id);
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
        g.EndedAt = DateTimeOffset.UtcNow;

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
    private (string subject, string body) BuildTargetEmail(Game game, Player me, Player? target)
    {
        var gameName = game?.Name ?? $"Game #{Id}";
        var subject = $"{gameName} – Your Target";

        string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        var tAlias = target?.Alias ?? "(no target assigned yet)";
        var tDisplay = string.IsNullOrWhiteSpace(target?.DisplayName) ? target?.Alias : target?.DisplayName;

        var text = new StringBuilder()
            .AppendLine($"The game \"{gameName}\" has started.")
            .AppendLine()
            .AppendLine($"Your alias: {me.Alias}")
            .AppendLine($"Your passcode: {me.PasscodePlaintext ?? "(not set)"}")
            .AppendLine()
            .AppendLine("Your current target:")
            .AppendLine($"• Alias: {tAlias}")
            .AppendLine($"• Display Name: {tDisplay}")
            .AppendLine()
            .AppendLine("Remember: never share your passcode. Use it when reporting an elimination.")
            .ToString();

        var html = new StringBuilder()
            .AppendLine($"<h3>The game \"{H(gameName)}\" has started.</h3>")
            .AppendLine("<p>Here is your assignment:</p>")
            .AppendLine("<ul>")
            .AppendLine($"  <li><strong>Your alias:</strong> {H(me.Alias)}</li>")
            .AppendLine($"  <li><strong>Your passcode:</strong> {H(me.PasscodePlaintext ?? "(not set)")}</li>")
            .AppendLine("</ul>")
            .AppendLine("<p><strong>Your current target:</strong></p>")
            .AppendLine("<ul>")
            .AppendLine($"  <li><strong>Alias:</strong> {H(tAlias)}</li>")
            .AppendLine($"  <li><strong>Display Name:</strong> {H(tDisplay)}</li>")
            .AppendLine("</ul>")
            .AppendLine("<p><em>Do not share your passcode. You’ll need it when reporting an elimination.</em></p>");

        var combined = new StringBuilder()
            .AppendLine(text)
            .AppendLine()
            .AppendLine("-----")
            .AppendLine("(If your mail client supports HTML, the formatted details appear below.)")
            .AppendLine("-----")
            .AppendLine()
            .AppendLine(html.ToString())
            .ToString();

        return (subject, combined);
    }
}
