namespace AssassinsProject.Models;

public class Elimination
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = default!;
    public string EliminatorEmail { get; set; } = "";
    public string VictimEmail { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public int PointsAwarded { get; set; }
    public bool PasscodeVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
