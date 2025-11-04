using System;

namespace AssassinsProject.Models
{
    public class Elimination
    {
        public int Id { get; set; }

        // Scope to a game
        public int GameId { get; set; }

        // Composite FKs to Players(GameId, Email)
        public int EliminatorGameId { get; set; }
        public string EliminatorEmail { get; set; } = null!;
        public int VictimGameId { get; set; }
        public string VictimEmail { get; set; } = null!;

        // Details
        public DateTimeOffset OccurredAt { get; set; }
        public bool PasscodeVerified { get; set; }
        public int PointsAwarded { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }

        // These two columns are missing in your database right now:
        public string? EvidenceUrl { get; set; }      // nvarchar(400) (nullable)
        public string? Notes { get; set; }            // nvarchar(1000) (nullable)

        // Navigations (optional)
        public Player? Eliminator { get; set; }
        public Player? Victim { get; set; }
    }
}
