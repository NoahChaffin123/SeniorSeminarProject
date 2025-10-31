namespace AssassinsProject.Models;

public class Player
{
    public int GameId { get; set; }
    public string Email { get; set; } = "";
    public string EmailNormalized { get; set; } = "";
    public Game Game { get; set; } = default!;

    // Display purposes (kept)
    public string DisplayName { get; set; } = "";

    // Signup profile fields
    public string RealName { get; set; } = "";       // required
    public string Alias { get; set; } = "";          // required, unique per game
    public string? HairColor { get; set; }
    public string? EyeColor { get; set; }
    public string? VisibleMarkings { get; set; }     // tattoos/scars etc
    public int? ApproximateAge { get; set; }
    public string? Specialty { get; set; }

    // Game state
    public bool IsActive { get; set; } = true;
    public int Points { get; set; } = 0;

    public string? TargetEmail { get; set; }
    public Player? Target { get; set; }
    public ICollection<Player> Hunters { get; set; } = new List<Player>();

    // Photo metadata
    public string? PhotoUrl { get; set; }
    public string? PhotoContentType { get; set; }
    public byte[]? PhotoBytesSha256 { get; set; }

    // Passcode (hashed) + stored plaintext for admin view only
    public byte[] PasscodeHash { get; set; } = Array.Empty<byte>();
    public byte[] PasscodeSalt { get; set; } = Array.Empty<byte>();
    public string PasscodeAlgo { get; set; } = "PBKDF2-SHA256";
    public int PasscodeCost { get; set; } = 100_000;
    public DateTimeOffset PasscodeSetAt { get; set; } = DateTimeOffset.UtcNow;

    // WARNING: This is stored to allow admins to view the code; consider encrypting or adopting a regenerate flow.
    public string? PasscodePlaintext { get; set; }
}
