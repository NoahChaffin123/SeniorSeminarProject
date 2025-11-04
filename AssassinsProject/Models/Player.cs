using System;

namespace AssassinsProject.Models
{
    public class Player
    {
        // Composite key (GameId, Email)
        public int GameId { get; set; }
        public string Email { get; set; } = default!;

        public Game? Game { get; set; }    // <-- the ONLY Game navigation

        // Common fields seen in your logs/views (keep/add what your app needs)
        public string EmailNormalized { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public string RealName { get; set; } = default!;
        public string Alias { get; set; } = default!;
        public bool IsActive { get; set; } = true;

        // Target linkage (optional)
        public string? TargetEmail { get; set; }
        public Player? Target { get; set; }

        // Email verification (based on your migrations)
        public bool IsEmailVerified { get; set; } = false;
        public DateTimeOffset? VerificationSentAt { get; set; }
        public string? VerificationToken { get; set; }

        // Misc you referenced in logs; keep if you actually use them
        public string? EyeColor { get; set; }
        public string? HairColor { get; set; }
        public int? ApproximateAge { get; set; }

        public int Points { get; set; } = 0;

        // Passcode fields (shape inferred from logs; keep if used)
        public string PasscodeAlgo { get; set; } = "argon2id";
        public int PasscodeCost { get; set; } = 1;
        public byte[] PasscodeHash { get; set; } = Array.Empty<byte>();
        public byte[] PasscodeSalt { get; set; } = Array.Empty<byte>();
        public string? PasscodePlaintext { get; set; }
        public DateTimeOffset PasscodeSetAt { get; set; } = DateTimeOffset.UtcNow;

        // Photos/notes (optional)
        public byte[]? PhotoBytesSha256 { get; set; }
        public string? PhotoContentType { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Specialty { get; set; }
        public string? VisibleMarkings { get; set; }
    }
}
