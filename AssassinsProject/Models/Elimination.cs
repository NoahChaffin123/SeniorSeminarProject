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

        // Navigations 
        public Player? Eliminator { get; set; }
        public Player? Victim { get; set; }
    }
}
