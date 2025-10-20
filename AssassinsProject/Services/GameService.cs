using AssassinsProject.Data;
using AssassinsProject.Models;
using AssassinsProject.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Services;

public class GameService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

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

    // Public signup overload (same as admin)
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

        // Only in Setup and only when signups are open
        if (game.Status != GameStatus.Setup)
            throw new InvalidOperationException("You can only add players while the game is in Setup.");
        if (!game.IsSignupOpen)
            throw new InvalidOperationException("Signups are currently closed for this game.");

        // Enforce hendrix.edu
        if (!norm.EndsWith("@hendrix.edu"))
            throw new InvalidOperationException("Only email addresses ending in @hendrix.edu can join.");

        // Uniqueness checks
        var existsEmail = await _db.Players.AnyAsync(p => p.GameId == gameId && p.EmailNormalized == norm, ct);
        if (existsEmail) throw new InvalidOperationException("A player with this email already exists in this game.");

        var existsAlias = await _db.Players.AnyAsync(p => p.GameId == gameId && p.Alias == alias, ct);
        if (existsAlias) throw new InvalidOperationException("That alias is already taken in this game. Choose another.");

        // Generate passcode
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
            PasscodePlaintext = passcode // admin can view, not edit
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

        // Auto-close signups on start
        game.IsSignupOpen = false;

        game.Status = GameStatus.Active;
        game.StartedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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
        if (game.Status != GameStatus.Active) throw new InvalidOperationException("Game is not active.");

        var eliminator = await _db.Players.SingleAsync(p => p.GameId == gameId && p.Email == eliminatorEmail, ct);
        var victim = await _db.Players.SingleAsync(p => p.GameId == gameId && p.Email == victimEmail, ct);

        if (!eliminator.IsActive || !victim.IsActive) throw new InvalidOperationException("Inactive eliminator or victim.");
        if (eliminator.TargetEmail != victim.Email) throw new InvalidOperationException("Victim is not the eliminator's current target.");

        // Verify eliminator’s passcode (prevents impersonation)
        var okElim = Passcode.Verify(providedEliminatorPasscode, eliminator.PasscodeSalt, eliminator.PasscodeHash, eliminator.PasscodeCost);
        if (!okElim) throw new InvalidOperationException("Your passcode is incorrect.");

        // Verify victim’s passcode (proof of elimination)
        var okVictim = Passcode.Verify(providedVictimPasscode, victim.PasscodeSalt, victim.PasscodeHash, victim.PasscodeCost);
        if (!okVictim) throw new InvalidOperationException("Victim passcode is incorrect.");

        var awarded = victim.Points + 1;
        eliminator.Points += awarded;

        var victimsTargetEmail = victim.TargetEmail;
        victim.IsActive = false;
        victim.TargetEmail = null;

        var stillActive = game.Players.Count(p => p.IsActive);
        if (stillActive > 1)
            eliminator.TargetEmail = (victimsTargetEmail == eliminator.Email) ? null : victimsTargetEmail;

        _db.Eliminations.Add(new Elimination
        {
            GameId = gameId,
            EliminatorEmail = eliminatorEmail,
            VictimEmail = victimEmail,
            OccurredAt = DateTimeOffset.UtcNow,
            PointsAwarded = awarded,
            PasscodeVerified = true,
            VerifiedAt = DateTimeOffset.UtcNow
        });

        if (game.Players.Count(p => p.IsActive) == 1)
        {
            game.Status = GameStatus.Completed;
            game.EndedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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

        player.IsActive = false;
        player.TargetEmail = null;

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
}
