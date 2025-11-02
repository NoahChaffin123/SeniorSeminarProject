using AssassinsProject.Data;
using AssassinsProject.Services;
using AssassinsProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AssassinsProject.Pages.Eliminations;

public class ReportModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly GameService _svc;

    public ReportModel(AppDbContext db, GameService svc)
    {
        _db = db;
        _svc = svc;
    }

    [BindProperty(SupportsGet = true)] public int GameId { get; set; }

    // Optional preselect via query string: ?eliminatorEmail=you@hendrix.edu
    [BindProperty(SupportsGet = true)] public string? EliminatorEmail { get; set; }

    [BindProperty] public string? VictimEmail { get; set; }
    [BindProperty] public string? VictimPasscode { get; set; }
    [BindProperty] public string? EliminatorPasscode { get; set; }

    public Game? Game { get; set; }

    // USED BY THE VIEW: some markup references Model.ActivePlayers
    public List<Player> ActivePlayers { get; set; } = new();

    public SelectListItem[] ActivePlayerOptions { get; set; } = Array.Empty<SelectListItem>();

    public class SelectListItem
    {
        public string Value { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var g = await _db.Games
            .AsNoTracking()
            .Include(x => x.Players)
            .SingleOrDefaultAsync(x => x.Id == GameId);

        if (g is null) return NotFound();
        Game = g;

        ActivePlayers = g.Players
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayName)
            .ToList();

        ActivePlayerOptions = ActivePlayers
            .Select(p => new SelectListItem
            {
                Value = p.Email,
                Text = $"{p.DisplayName} ({p.Alias} : {p.Email})"
            })
            .ToArray();

        // If eliminatorEmail was provided but no longer active, clear it
        if (!string.IsNullOrWhiteSpace(EliminatorEmail) &&
            !ActivePlayers.Any(p => p.Email.Equals(EliminatorEmail, StringComparison.OrdinalIgnoreCase)))
        {
            EliminatorEmail = null;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var g = await _db.Games.FindAsync(GameId);
        if (g is null) return NotFound();

        // Reject if the game is paused or not active
        if (g.Status != GameStatus.Active)
        {
            ModelState.AddModelError(string.Empty, "This game is not active.");
            return await OnGetAsync();
        }
        if (g.IsPaused)
        {
            ModelState.AddModelError(string.Empty, "This game is currently paused. Eliminations are disabled.");
            return await OnGetAsync();
        }

        if (string.IsNullOrWhiteSpace(EliminatorEmail) ||
            string.IsNullOrWhiteSpace(VictimEmail) ||
            string.IsNullOrWhiteSpace(VictimPasscode) ||
            string.IsNullOrWhiteSpace(EliminatorPasscode))
        {
            ModelState.AddModelError(string.Empty, "Please fill out all fields.");
            return await OnGetAsync();
        }

        if (string.Equals(EliminatorEmail, VictimEmail, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "You cannot eliminate yourself.");
            return await OnGetAsync();
        }

        try
        {
            await _svc.ReportEliminationAsync(
                GameId,
                EliminatorEmail!,
                VictimEmail!,
                VictimPasscode!,
                EliminatorPasscode!);

            TempData["Message"] = "Elimination reported.";
            return RedirectToPage("/Games/Details", new { id = GameId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await OnGetAsync();
        }
    }
}
