using AssassinsProject.Models;
using Microsoft.EntityFrameworkCore;

namespace AssassinsProject.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Elimination> Eliminations => Set<Elimination>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Player PK
        b.Entity<Player>().HasKey(p => new { p.GameId, p.Email });

        // Player columns
        b.Entity<Player>().Property(p => p.Email).HasMaxLength(256);
        b.Entity<Player>().Property(p => p.EmailNormalized).HasMaxLength(256).IsRequired();
        b.Entity<Player>().Property(p => p.DisplayName).HasMaxLength(100).IsRequired();

        b.Entity<Player>().Property(p => p.RealName).HasMaxLength(100).IsRequired();
        b.Entity<Player>().Property(p => p.Alias).HasMaxLength(100).IsRequired();
        b.Entity<Player>().Property(p => p.HairColor).HasMaxLength(50);
        b.Entity<Player>().Property(p => p.EyeColor).HasMaxLength(50);
        b.Entity<Player>().Property(p => p.VisibleMarkings).HasMaxLength(500);
        b.Entity<Player>().Property(p => p.Specialty).HasMaxLength(100);
        b.Entity<Player>().Property(p => p.PasscodePlaintext).HasMaxLength(64);

        // Unique Alias per game
        b.Entity<Player>()
            .HasIndex(p => new { p.GameId, p.Alias })
            .IsUnique();

        // Target relation (ring)
        b.Entity<Player>()
            .HasOne(p => p.Target)
            .WithMany(p => p.Hunters)
            .HasForeignKey(p => new { p.GameId, p.TargetEmail })
            .HasPrincipalKey(p => new { p.GameId, p.Email })
            .OnDelete(DeleteBehavior.NoAction);

        // One hunter per active target
        b.Entity<Player>()
            .HasIndex(p => new { p.GameId, p.TargetEmail })
            .HasFilter("[IsActive] = 1 AND [TargetEmail] IS NOT NULL")
            .IsUnique();

        // Fast lookup by normalized email
        b.Entity<Player>()
            .HasIndex(p => new { p.GameId, p.EmailNormalized });

        // Game config
        b.Entity<Game>().Property(g => g.Name).HasMaxLength(128).IsRequired();
        b.Entity<Game>().Property(g => g.IsSignupOpen).HasDefaultValue(true);

        // Eliminations FK
        b.Entity<Elimination>()
            .HasOne(e => e.Game)
            .WithMany(g => g.Eliminations)
            .HasForeignKey(e => e.GameId);
    }
}
