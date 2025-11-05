using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Services.Email;
using AssassinsProject.Utilities; // Passcode.Generate / Passcode.Hash / Passcode.Verify
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssassinsProject.Services
{
    /// <summary>
    /// Game orchestration (create, start, ring assignment, reporting, admin helpers).
    /// </summary>
    public class GameService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _email;
        private readonly ILogger<GameService> _log;

        public GameService(AppDbContext db, IEmailSender email, ILogger<GameService> log)
        {
            _db = db;
            _email = email;
            _log = log;
        }

        // ------------------------------------------------------------------
        // Ensure a player has a valid (generated + hashed) passcode.
        // IMPORTANT: Passcode.Hash returns (hash: byte[], salt: byte[], algo: string, cost: int)
        // ------------------------------------------------------------------
        private static bool EnsurePlayerPasscode(Player p)
        {
            bool changed = false;

            if (string.IsNullOrWhiteSpace(p.PasscodePlaintext))
            {
                p.PasscodePlaintext = Passcode.Generate();   // e.g., 7–8 uppercase letters/digits
                p.PasscodeSetAt = DateTimeOffset.UtcNow;
                changed = true;
            }

            var needsAlgo = string.IsNullOrWhiteSpace(p.PasscodeAlgo);
            var needsCost = p.PasscodeCost <= 0;
            var needsSalt = p.PasscodeSalt == null || p.PasscodeSalt.Length == 0;
            var needsHash = p.PasscodeHash == null || p.PasscodeHash.Length == 0;

            if (needsAlgo || needsCost || needsSalt || needsHash)
            {
                // (hash, salt, algo, cost) — keep order in sync with your utility.
                var (hash, salt, algo, cost) = Passcode.Hash(p.PasscodePlaintext!);

                p.PasscodeHash = hash;   // byte[]
                p.PasscodeSalt = salt;   // byte[]
                p.PasscodeAlgo = algo;   // string
                p.PasscodeCost = cost;   // iterations/cost

                if (p.PasscodeSetAt == default)
                    p.PasscodeSetAt = DateTimeOffset.UtcNow;

                changed = true;
            }

            return changed;
        }

        // ------------------------------------------------------------------
        // Create game (used by Pages/Games/Create.cshtml.cs)
        // ------------------------------------------------------------------
        public async Task<Game> CreateGameAsync(string name)
        {
            var g = new Game
            {
                Name = name.Trim(),
                Status = GameStatus.Setup,
                IsSignupOpen = true,
                IsPaused = false
            };

            _db.Games.Add(g);
            await _db.SaveChangesAsync();
            return g;
        }

        // ------------------------------------------------------------------
        // Start game: assign ring and activate verified players.
        // Ensures all verified players have valid passcodes.
        // Sends each player a "Your Target" email (no victim passcode/email/real name).
        // ------------------------------------------------------------------
        public async Task StartGameAsync(int gameId)
        {
            var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId);
            if (game is null) throw new InvalidOperationException("Game not found.");
            if (game.Status != GameStatus.Setup)
                throw new InvalidOperationException("Game is not in Setup state.");

            var players = await _db.Players
                .Where(p => p.GameId == gameId && p.IsEmailVerified)
                .OrderBy(p => p.DisplayName)
                .ToListAsync();

            if (players.Count < 2)
                _log.LogWarning("Starting game {GameId} with fewer than 2 verified players.", gameId);

            foreach (var p in players)
            {
                EnsurePlayerPasscode(p);
                p.IsActive = true;
            }

            // Ring assignment
            if (players.Count >= 2)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var curr = players[i];
                    var next = players[(i + 1) % players.Count];
                    curr.TargetEmail = next.Email;
                }
            }
            else if (players.Count == 1)
            {
                players[0].TargetEmail = null;
            }

            game.Status = GameStatus.Active;
            game.IsPaused = false;
            game.IsSignupOpen = false;
            game.StartedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            // Notify each verified/active player of their target + their own passcode
            foreach (var p in players)
            {
                try
                {
                    await SendTargetEmailAsync(game, p);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to send target email to {Email} (Game {GameId})", p.Email, gameId);
                }
            }
        }

        // ------------------------------------------------------------------
        // Report elimination (used by Players' report page).
        // Uses 4-arg Passcode.Verify(plaintext, hash, salt, iterations).
        // ------------------------------------------------------------------
        public async Task ReportEliminationAsync(
            int gameId,
            string eliminatorAlias,
            string victimAlias,
            string eliminatorPasscode,
            string victimPasscode)
        {
            var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == gameId);
            if (game is null) throw new InvalidOperationException("Game not found.");
            if (game.Status != GameStatus.Active) throw new InvalidOperationException("Game is not active.");
            if (game.IsPaused) throw new InvalidOperationException("Game is paused.");

            eliminatorAlias = (eliminatorAlias ?? string.Empty).Trim();
            victimAlias     = (victimAlias     ?? string.Empty).Trim();

            var players = await _db.Players
                .Where(p => p.GameId == gameId)
                .ToListAsync();

            var eliminator = FindPlayerByAnyId(players, eliminatorAlias);
            var victim     = FindPlayerByAnyId(players, victimAlias);

            if (eliminator is null || victim is null)
                throw new InvalidOperationException("Could not find eliminator or victim.");

            if (!eliminator.IsActive)
                throw new InvalidOperationException("Eliminator is not active.");

            if (!victim.IsActive)
                throw new InvalidOperationException("Victim is already eliminated.");

            // Must match the ring (eliminator -> victim)
            if (!string.Equals(eliminator.TargetEmail, victim.Email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Selected victim is not your assigned target.");

            // Ensure passcodes exist (paranoia)
            if (string.IsNullOrWhiteSpace(eliminator.PasscodePlaintext))
                EnsurePlayerPasscode(eliminator);
            if (string.IsNullOrWhiteSpace(victim.PasscodePlaintext))
                EnsurePlayerPasscode(victim);

            // --- Normalize passcodes (trim whitespace/newlines, force uppercase) ---
            string normElim = (eliminatorPasscode ?? string.Empty)
                .Trim()
                .Replace("\r", "")
                .Replace("\n", "")
                .ToUpperInvariant();

            string normVictim = (victimPasscode ?? string.Empty)
                .Trim()
                .Replace("\r", "")
                .Replace("\n", "")
                .ToUpperInvariant();

            // Verify with iterations (cost)
            if (!Passcode.Verify(normElim, eliminator.PasscodeHash!, eliminator.PasscodeSalt!, eliminator.PasscodeCost))
                throw new InvalidOperationException("Your passcode is incorrect.");

            if (!Passcode.Verify(normVictim, victim.PasscodeHash!, victim.PasscodeSalt!, victim.PasscodeCost))
                throw new InvalidOperationException("Victim’s passcode is incorrect.");

            // Record elimination
            var elim = new Elimination
            {
                GameId = gameId,
                EliminatorEmail = eliminator.Email,
                EliminatorGameId = gameId,
                VictimEmail = victim.Email,
                VictimGameId = gameId,
                OccurredAt = DateTimeOffset.UtcNow,
                PasscodeVerified = true,
                PointsAwarded = 1
            };
            _db.Eliminations.Add(elim);

            // Award + rewire
            eliminator.Points += 1;
            victim.IsActive = false;

            eliminator.TargetEmail = string.IsNullOrEmpty(victim.TargetEmail)
                ? null
                : victim.TargetEmail;

            await _db.SaveChangesAsync();
        }

        // ------------------------------------------------------------------
        // Admin add player (OVERLOAD that matches a caller without displayName).
        // Returns (player, passcodePlaintext). DisplayName defaults to alias.
        // ------------------------------------------------------------------
        public async Task<(Player player, string passcode)> AddPlayerAdminAsync(
            int gameId,
            string email,
            string alias,
            string realName,
            int? approximateAge,
            string? hairColor,
            string? eyeColor,
            string? visibleMarkings,
            string? specialty,
            string? photoUrl = null,
            string? contentType = null,
            byte[]? photoSha256 = null)
        {
            return await AddPlayerAdminAsync(
                gameId, email, alias, realName, displayName: alias,
                approximateAge, hairColor, eyeColor, visibleMarkings, specialty,
                photoUrl, contentType, photoSha256);
        }

        // Canonical/admin add with explicit displayName
        public async Task<(Player player, string passcode)> AddPlayerAdminAsync(
            int gameId,
            string email,
            string alias,
            string realName,
            string? displayName,
            int? approximateAge,
            string? hairColor,
            string? eyeColor,
            string? visibleMarkings,
            string? specialty,
            string? photoUrl = null,
            string? contentType = null,
            byte[]? photoSha256 = null)
        {
            var emailNorm = (email ?? string.Empty).Trim().ToUpperInvariant();

            var p = await _db.Players
                .FirstOrDefaultAsync(x => x.GameId == gameId && x.EmailNormalized == emailNorm);

            if (p == null)
            {
                p = new Player
                {
                    GameId = gameId,
                    Email = email.Trim(),
                    EmailNormalized = emailNorm,
                    Alias = alias?.Trim() ?? string.Empty,
                    RealName = realName?.Trim() ?? string.Empty,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? (alias?.Trim() ?? string.Empty) : displayName!.Trim(),
                    ApproximateAge = approximateAge,
                    HairColor = hairColor?.Trim(),
                    EyeColor = eyeColor?.Trim(),
                    VisibleMarkings = visibleMarkings?.Trim(),
                    Specialty = specialty?.Trim(),
                    IsActive = false,
                    IsEmailVerified = true, // admin-added => treat as verified
                    Points = 0,
                    PhotoUrl = photoUrl,
                    PhotoContentType = contentType,
                    PhotoBytesSha256 = photoSha256
                };
                _db.Players.Add(p);
            }
            else
            {
                p.Alias = alias?.Trim() ?? p.Alias;
                p.RealName = realName?.Trim() ?? p.RealName;
                p.DisplayName = string.IsNullOrWhiteSpace(displayName) ? p.DisplayName : displayName!.Trim();
                p.ApproximateAge = approximateAge;
                p.HairColor = hairColor?.Trim();
                p.EyeColor = eyeColor?.Trim();
                p.VisibleMarkings = visibleMarkings?.Trim();
                p.Specialty = specialty?.Trim();
                if (!string.IsNullOrWhiteSpace(photoUrl)) p.PhotoUrl = photoUrl;
                if (!string.IsNullOrWhiteSpace(contentType)) p.PhotoContentType = contentType;
                if (photoSha256 is not null && photoSha256.Length > 0) p.PhotoBytesSha256 = photoSha256;
            }

            EnsurePlayerPasscode(p);
            await _db.SaveChangesAsync();
            return (p, p.PasscodePlaintext!);
        }

        // ------------------------------------------------------------------
        // Admin remove (soft remove and ring rewire if necessary).
        // ------------------------------------------------------------------
        public async Task RemovePlayerAsync(int gameId, string email)
        {
            var emailNorm = (email ?? string.Empty).Trim().ToUpperInvariant();

            var p = await _db.Players
                .FirstOrDefaultAsync(x => x.GameId == gameId && x.EmailNormalized == emailNorm);

            if (p == null) return;

            if (p.IsActive)
            {
                var players = await _db.Players.Where(x => x.GameId == gameId && x.IsActive).ToListAsync();

                var prev = players.FirstOrDefault(x =>
                    string.Equals(x.TargetEmail, p.Email, StringComparison.OrdinalIgnoreCase));
                var nextEmail = p.TargetEmail;

                if (prev != null) prev.TargetEmail = nextEmail;

                p.IsActive = false;
                p.TargetEmail = null;
            }

            await _db.SaveChangesAsync();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        // Accepts Alias OR DisplayName OR Email, and also tolerates "alias : email".
        private static Player? FindPlayerByAnyId(IEnumerable<Player> players, string value)
        {
            var v = (value ?? string.Empty).Trim();

            // If a UI ever posts "alias : email", take just the alias part so lookups still work.
            var core = v;
            var colon = v.IndexOf(':');
            if (colon >= 0)
            {
                core = v.Substring(0, colon).Trim();
            }

            return players.FirstOrDefault(p =>
                string.Equals(p.Alias ?? string.Empty, v,    StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Alias ?? string.Empty, core, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.DisplayName ?? string.Empty, v,    StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.DisplayName ?? string.Empty, core, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Email ?? string.Empty, v,    StringComparison.OrdinalIgnoreCase));
        }

        // Email helper: "Your Target" (no victim passcode/email/real name).
        private async Task SendTargetEmailAsync(Game game, Player player)
        {
            string gameName = game?.Name ?? $"Game #{player.GameId}";
            var subject = $"{gameName} — Your Target";

            Player? target = null;
            if (!string.IsNullOrWhiteSpace(player.TargetEmail))
            {
                var norm = player.TargetEmail.Trim().ToUpperInvariant();
                target = await _db.Players
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.GameId == player.GameId &&
                                              p.EmailNormalized == norm);
            }

            // Plaintext
            var sbText = new StringBuilder()
                .AppendLine($"The game \"{gameName}\" has started.")
                .AppendLine()
                .AppendLine("Your passcode (keep this secret):")
                .AppendLine(player.PasscodePlaintext ?? "(not set)")
                .AppendLine();

            if (target == null)
            {
                sbText.AppendLine("You currently have no target assigned.");
            }
            else
            {
                sbText
                    .AppendLine("Your current target:")
                    .AppendLine($"  Alias: {target.Alias}")
                    .AppendLine($"  Display Name: {target.DisplayName}")
                    .AppendLine()
                    .AppendLine("Do not share this email.");
            }

            // Lightweight HTML
            string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

            var html = new StringBuilder()
                .AppendLine($"<h3>The game \"{H(gameName)}\" has started.</h3>")
                .AppendLine("<p><strong>Your passcode</strong> (keep this secret):</p>")
                .AppendLine($"<p><code>{H(player.PasscodePlaintext)}</code></p>");

            if (target == null)
            {
                html.AppendLine("<p>You currently have no target assigned.</p>");
            }
            else
            {
                html.AppendLine("<p><strong>Your current target:</strong></p>")
                    .AppendLine("<ul>")
                    .AppendLine($"  <li>Alias: {H(target.Alias)}</li>")
                    .AppendLine($"  <li>Display Name: {H(target.DisplayName)}</li>")
                    .AppendLine("</ul>");

                if (!string.IsNullOrWhiteSpace(target.PhotoUrl))
                {
                    var photoUrl = target.PhotoUrl!;
                    html.AppendLine($"<p><img src=\"{H(photoUrl)}\" alt=\"Target photo\" style=\"max-width:320px;height:auto;border:1px solid #ddd;border-radius:8px\" /></p>");
                }
            }

            var combined = new StringBuilder()
                .AppendLine(sbText.ToString())
                .AppendLine()
                .AppendLine("-----")
                .AppendLine("(If your mail client supports HTML, details appear below.)")
                .AppendLine("-----")
                .AppendLine()
                .AppendLine(html.ToString())
                .ToString();

            await _email.SendAsync(player.Email, subject, combined);
        }
    }
}
