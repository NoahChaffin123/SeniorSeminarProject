using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Utilities;
using AssassinsProject.Services.Email;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AssassinsProject.Services
{
    public class GameService
    {
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;

        public GameService(AppDbContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        // -------------------------
        // Game creation / players
        // -------------------------
        public async Task<Game> CreateGameAsync(string name, CancellationToken ct = default)
        {
            var game = new Game { Name = name, Status = GameStatus.Setup, IsSignupOpen = true };
            _db.Games.Add(game);
            await _db.SaveChangesAsync(ct);
            return game;
        }

        // Admin add (full details just like public signup)
        public async Task<(Player player, string passcode)> AddPlayerAdminAsync(
            int gameId,
            string email,
            string realName,
            string alias,
            string? hairColor,
            string? eyeColor,
            string? visibleMarkings,
            int? approximateAge,
            string? specialty,
            string? photoUrl,
            string? contentType,
            byte[]? photoSha256,
            CancellationToken ct = default)
            => await AddPlayerWithDetailsAsync(
                gameId, email, realName, alias, hairColor, eyeColor, visibleMarkings, approximateAge, specialty,
                photoUrl, contentType, photoSha256, ct);

        // Public/admin add implementation
        public async Task<(Player player, string passcode)> AddPlayerWithDetailsAsync(
            int gameId,
            string email,
            string realName,
            string alias,
            string? hairColor,
            string? eyeColor,
            string? visibleMarkings,
            int? approximateAge,
            string? specialty,
            string? photoUrl,
            string? contentType,
            byte[]? photoSha256,
            CancellationToken ct = default)
        {
            var norm = EmailNormalizer.Normalize(email);

            var game = await _db.Games.SingleAsync(g => g.Id == gameId, ct);

            if (game.Status != GameStatus.Setup)
                throw new InvalidOperationException("You can only add players while the game is in Setup.");
            if (!game.IsSignupOpen)
                throw new InvalidOperationException("Signups are currently closed for this game.");

            // Enforce hendrix.edu
            if (!norm.EndsWith("@hendrix.edu"))
                throw new InvalidOperationException("Only email addresses ending in @hendrix.edu can join.");

            var existsEmail = await _db.Players.AnyAsync(p => p.GameId == gameId && p.EmailNormalized == norm, ct);
            if (existsEmail) throw new InvalidOperationException("A player with this email already exists in this game.");

            var existsAlias = await _db.Players.AnyAsync(p => p.GameId == gameId && p.Alias == alias, ct);
            if (existsAlias) throw new InvalidOperationException("That alias is already taken in this game. Choose another.");

            var passcode = Passcode.Generate();
            var (hash, salt, algo, cost) = Passcode.Hash(passcode);

            var p = new Player
            {
                GameId = gameId,
                Email = email,
                EmailNormalized = norm,
                DisplayName = alias,
                RealName = realName,
                Alias = alias,
                HairColor = hairColor,
                EyeColor = eyeColor,
                VisibleMarkings = visibleMarkings,
                ApproximateAge = approximateAge,
                Specialty = specialty,
                IsActive = true,
                Points = 0,
                TargetEmail = null,
                PhotoUrl = photoUrl,
                PhotoContentType = contentType,
                PhotoBytesSha256 = photoSha256,
                PasscodeHash = hash,
                PasscodeSalt = salt,
                PasscodeAlgo = algo,
                PasscodeCost = cost,
                PasscodeSetAt = DateTimeOffset.UtcNow,
                PasscodePlaintext = passcode
            };

            _db.Players.Add(p);
            await _db.SaveChangesAsync(ct);
            return (p, passcode);
        }

        // -------------------------
        // Start: single random ring (no 2-cycles)
        // -------------------------
        public async Task StartGameAsync(int gameId, CancellationToken ct = default)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var game = await _db.Games
                .Include(g => g.Players.Where(p => p.IsActive))
                .SingleAsync(g => g.Id == gameId, ct);

            if (game.Status != GameStatus.Setup)
                throw new InvalidOperationException("Game is not in Setup.");

            var players = game.Players.OrderBy(_ => Guid.NewGuid()).ToList();
            if (players.Count < 2)
                throw new InvalidOperationException("Need at least 2 active players to start.");

            AssignSingleCycle(players);
            ValidateSingleCycle(players);

            game.IsSignupOpen = false;
            game.Status = GameStatus.Active;
            game.StartedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // ✅ Send "assignment" email to each player (the full “game started” template)
            await SendAssignmentEmailsAsync(game, players, ct);
        }

        private async Task SendAssignmentEmailsAsync(Game game, List<Player> players, CancellationToken ct)
        {
            var byEmail = players.ToDictionary(p => p.Email, p => p);

            foreach (var me in players)
            {
                Player? target = null;
                if (!string.IsNullOrWhiteSpace(me.TargetEmail))
                    byEmail.TryGetValue(me.TargetEmail, out target);

                var baseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
                              ?? "https://assassins-game-cjddb5dydyfsb4bv.centralus-01.azurewebsites.net";

                var email = AssignmentEmailBuilder.Build(game, me, target, baseUrl);

                try
                {
                    await _emailSender.SendAsync(me.Email, email.Subject, email.HtmlBody, ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Email Error] Could not send to {me.Email}: {ex.Message}");
                }
            }
        }

        private static void AssignSingleCycle(List<Player> players)
        {
            int n = players.Count;
            for (int i = 0; i < n; i++)
                players[i].TargetEmail = players[(i + 1) % n].Email;
        }

        private static void ValidateSingleCycle(ICollection<Player> players)
        {
            var active = players.Where(p => p.IsActive).ToList();
            int n = active.Count;
            if (n < 2) return;

            var map = active.ToDictionary(p => p.Email, p => p.TargetEmail);

            foreach (var p in active)
            {
                if (map[p.Email] is null) throw new InvalidOperationException("Every active player must have a target.");
                if (map[p.Email] == p.Email) throw new InvalidOperationException("No player may target themselves.");
            }

            var start = active[0].Email;
            var visited = new HashSet<string>(capacity: n);
            var cur = start;
            for (int steps = 0; steps < n; steps++)
            {
                if (!visited.Add(cur)) break;
                cur = map[cur]!;
            }
            if (cur != start || visited.Count != n)
                throw new InvalidOperationException("Target assignment must form a single ring. Try starting again.");
        }

        // -------------------------
        // Report elimination (requires both passcodes)
        // -------------------------
        public async Task ReportEliminationAsync(
            int gameId,
            string eliminatorEmail,
            string victimEmail,
            string providedVictimPasscode,
            string providedEliminatorPasscode,
            CancellationToken ct = default)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var game = await _db.Games.Include(g => g.Players).SingleAsync(g => g.Id == gameId, ct);
            if (game.Status != GameStatus.Active)
                throw new InvalidOperationException("Game is not active.");

            if (game.IsPaused)
                throw new InvalidOperationException("Game is paused. Eliminations are temporarily disabled.");

            var eliminator = await _db.Players.SingleAsync(p => p.GameId == gameId && p.Email == eliminatorEmail, ct);
            var victim = await _db.Players.SingleAsync(p => p.GameId == gameId && p.Email == victimEmail, ct);

            if (!eliminator.IsActive || !victim.IsActive)
                throw new InvalidOperationException("Inactive eliminator or victim.");
            if (eliminator.TargetEmail != victim.Email)
                throw new InvalidOperationException("Victim is not the eliminator's current target.");

            var okElim = await VerifyOrRepairPasscodeAsync(eliminator, providedEliminatorPasscode, ct);
            if (!okElim) throw new InvalidOperationException("Your passcode is incorrect.");
            var okVictim = await VerifyOrRepairPasscodeAsync(victim, providedVictimPasscode, ct);
            if (!okVictim) throw new InvalidOperationException("Victim passcode is incorrect.");

            // Points before we deactivate victim
            var awarded = victim.Points + 1;
            eliminator.Points += awarded;

            // Capture victim's target
            var victimsTargetEmail = victim.TargetEmail;

            // --- IMPORTANT ORDER TO SATISFY UNIQUE INDEX (GameId, TargetEmail) ---
            // 1) Free the unique slot by clearing the victim's target first.
            victim.IsActive = false;
            victim.TargetEmail = null;
            await _db.SaveChangesAsync(ct); // ensures UPDATE executes before we set eliminator.TargetEmail

            // 2) Move eliminator onto victim's former target (when more than 1 remains)
            var stillActive = game.Players.Count(p => p.IsActive); // victim already inactive in memory
            if (stillActive > 1)
            {
                eliminator.TargetEmail = (victimsTargetEmail == eliminator.Email) ? null : victimsTargetEmail;
            }

            // 3) Record the elimination
            _db.Eliminations.Add(new Elimination
            {
                GameId = gameId,
                EliminatorGameId = gameId,
                EliminatorEmail = eliminatorEmail,
                VictimGameId = gameId,
                VictimEmail = victimEmail,
                OccurredAt = DateTimeOffset.UtcNow,
                PointsAwarded = awarded,
                PasscodeVerified = true,
                VerifiedAt = DateTimeOffset.UtcNow
            });

            // If that deactivated the last remaining opponent, end the game
            if (game.Players.Count(p => p.IsActive) == 1)
            {
                game.Status = GameStatus.Completed;
                game.EndedAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Send "next target" email (only if game continues and a real next target exists)
            if (game.Status == GameStatus.Active && !string.IsNullOrWhiteSpace(eliminator.TargetEmail))
            {
                var nextTarget = game.Players.FirstOrDefault(p => p.Email == eliminator.TargetEmail);
                if (nextTarget is not null)
                {
                    var previousTargetName = string.IsNullOrWhiteSpace(victim.DisplayName) ? victim.Alias : victim.DisplayName;
                    var baseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
                                  ?? "https://assassins-game-cjddb5dydyfsb4bv.centralus-01.azurewebsites.net";

                    var (subject, html) = BuildNextTargetEmail(game, eliminator, nextTarget, previousTargetName, baseUrl);

                    try
                    {
                        await _emailSender.SendAsync(eliminator.Email, subject, html, ct);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Email Error] Next-target email failed for {eliminator.Email}: {ex.Message}");
                    }
                }
            }
        }

        private async Task<bool> VerifyOrRepairPasscodeAsync(Player p, string provided, CancellationToken ct)
        {
            static bool NeedsRepair(Player x) =>
                x.PasscodeHash == null || x.PasscodeHash.Length == 0 ||
                x.PasscodeSalt == null || x.PasscodeSalt.Length == 0 ||
                x.PasscodeCost <= 0;

            if (!NeedsRepair(p))
                return Passcode.Verify(provided, p.PasscodeSalt, p.PasscodeHash, p.PasscodeCost);

            if (!string.IsNullOrWhiteSpace(p.PasscodePlaintext) &&
                string.Equals(provided, p.PasscodePlaintext, StringComparison.Ordinal))
            {
                var (hash, salt, algo, cost) = Passcode.Hash(provided);
                p.PasscodeHash = hash;
                p.PasscodeSalt = salt;
                p.PasscodeAlgo = algo;
                p.PasscodeCost = cost;
                p.PasscodeSetAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                return true;
            }

            return false;
        }

        // -------------------------
        // Admin Remove
        // -------------------------
        public async Task RemovePlayerAsync(int gameId, string email, CancellationToken ct = default)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var game = await _db.Games.Include(g => g.Players).SingleAsync(g => g.Id == gameId, ct);
            var player = await _db.Players.SingleOrDefaultAsync(p => p.GameId == gameId && p.Email == email, ct);
            if (player is null) throw new InvalidOperationException("Player not found.");

            if (game.Status == GameStatus.Completed)
                throw new InvalidOperationException("Cannot remove players from a completed game.");

            if (game.Status == GameStatus.Setup)
            {
                _db.Players.Remove(player);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }

            if (!player.IsActive)
            {
                await tx.CommitAsync(ct);
                return;
            }

            var hunter = await _db.Players.SingleOrDefaultAsync(
                h => h.GameId == gameId && h.IsActive && h.TargetEmail == player.Email, ct);

            var victimsTargetEmail = player.TargetEmail;

            // Order: free unique slot first
            player.IsActive = false;
            player.TargetEmail = null;
            await _db.SaveChangesAsync(ct);

            if (hunter is not null)
                hunter.TargetEmail = (victimsTargetEmail == hunter.Email) ? null : victimsTargetEmail;

            if (game.Players.Count(p => p.IsActive) == 1)
            {
                game.Status = GameStatus.Completed;
                game.EndedAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // -------------------------
        // Email builder for "next target" after elimination
        // -------------------------
        private static (string subject, string html) BuildNextTargetEmail(
            Game game,
            Player me,
            Player? nextTarget,
            string previousTargetName,
            string baseUrl)
        {
            string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

            var gameName = game?.Name ?? "Assassins";
            var subject = $"Elimination Confirmation of {previousTargetName}";

            var tAlias = nextTarget?.Alias ?? "(no target assigned yet)";
            var tDisplay = string.IsNullOrWhiteSpace(nextTarget?.DisplayName) ? nextTarget?.Alias : nextTarget?.DisplayName;

            var details = new StringBuilder()
                .AppendLine("<ul>")
                .AppendLine($"  <li><strong>Alias:</strong> {H(tAlias)}</li>")
                .AppendLine($"  <li><strong>Display Name:</strong> {H(tDisplay)}</li>");

            if (nextTarget is not null)
            {
                if (nextTarget.ApproximateAge.HasValue) details.AppendLine($"  <li><strong>Approximate Age:</strong> {nextTarget.ApproximateAge.Value}</li>");
                if (!string.IsNullOrWhiteSpace(nextTarget.HairColor)) details.AppendLine($"  <li><strong>Hair Color:</strong> {H(nextTarget.HairColor)}</li>");
                if (!string.IsNullOrWhiteSpace(nextTarget.EyeColor)) details.AppendLine($"  <li><strong>Eye Color:</strong> {H(nextTarget.EyeColor)}</li>");
                if (!string.IsNullOrWhiteSpace(nextTarget.VisibleMarkings)) details.AppendLine($"  <li><strong>Visible Markings:</strong> {H(nextTarget.VisibleMarkings)}</li>");
                if (!string.IsNullOrWhiteSpace(nextTarget.Specialty)) details.AppendLine($"  <li><strong>Specialty:</strong> {H(nextTarget.Specialty)}</li>");

                // Photo link (if available)
                if (!string.IsNullOrWhiteSpace(nextTarget.PhotoUrl))
                {
                    var photoUrl = nextTarget.PhotoUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? nextTarget.PhotoUrl!
                        : $"{baseUrl.TrimEnd('/')}/{nextTarget.PhotoUrl!.TrimStart('/')}";

                    details.AppendLine($"  <li><strong>Photo:</strong> <a href=\"{H(photoUrl)}\">{H(photoUrl)}</a></li>");
                }
            }

            details.AppendLine("</ul>");

            var html = new StringBuilder()
                .AppendLine($"<h2>{H(gameName)} – Next Target</h2>")
                .AppendLine($"<p>Your elimination of <strong>{H(previousTargetName)}</strong> is confirmed. Here is the information of your next target:</p>")
                .AppendLine("<p><strong>Your passcode:</strong> " + H(me.PasscodePlaintext ?? "(not set)") + "</p>")
                .AppendLine("<p><strong>Your current target:</strong></p>")
                .AppendLine(details.ToString())
                .AppendLine("<p><em>Do not share your passcode. You’ll need it when reporting an elimination.</em></p>")
                .ToString();

            return (subject, html);
        }
    }
}
