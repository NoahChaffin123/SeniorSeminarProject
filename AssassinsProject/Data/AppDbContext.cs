using Microsoft.EntityFrameworkCore;
using AssassinsProject.Models;

namespace AssassinsProject.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Game> Games => Set<Game>();
        public DbSet<Player> Players => Set<Player>();
        public DbSet<Elimination> Eliminations => Set<Elimination>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- GAME ----------
            modelBuilder.Entity<Game>(e =>
            {
                e.HasKey(g => g.Id);
                e.Property(g => g.Name).HasMaxLength(128).IsRequired();

                e.HasMany(g => g.Players)
                 .WithOne(p => p.Game)
                 .HasForeignKey(p => p.GameId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasMany(g => g.Eliminations)
                 .WithOne()
                 .HasForeignKey(el => el.GameId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- PLAYER ----------
            modelBuilder.Entity<Player>(e =>
            {
                e.HasKey(p => new { p.GameId, p.Email });

                e.Property(p => p.Email).HasMaxLength(256).IsRequired();
                e.Property(p => p.EmailNormalized).HasMaxLength(256).IsRequired();
                e.Property(p => p.DisplayName).HasMaxLength(100).IsRequired();
                e.Property(p => p.RealName).HasMaxLength(100).IsRequired();
                e.Property(p => p.Alias).HasMaxLength(100).IsRequired();
                e.Property(p => p.TargetEmail).HasMaxLength(256);

                e.HasIndex(p => new { p.GameId, p.Email });
                e.HasIndex(p => new { p.GameId, p.Alias });
                e.HasIndex(p => new { p.GameId, p.EmailNormalized });
                e.HasIndex(p => new { p.GameId, p.TargetEmail });

                e.HasOne(p => p.Game)
                 .WithMany(g => g.Players)
                 .HasForeignKey(p => p.GameId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(p => p.Target)
                 .WithMany()
                 .HasForeignKey(p => new { p.GameId, p.TargetEmail })
                 .HasPrincipalKey(nameof(Player.GameId), nameof(Player.Email))
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ---------- ELIMINATION ----------
            modelBuilder.Entity<Elimination>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.EliminatorEmail).HasMaxLength(256).IsRequired();
                e.Property(x => x.VictimEmail).HasMaxLength(256).IsRequired();

                // FK to Game via GameId
                e.HasOne<Game>()
                 .WithMany(g => g.Eliminations)
                 .HasForeignKey(x => x.GameId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Eliminator -> Player via (EliminatorGameId, EliminatorEmail)
                e.HasOne(x => x.Eliminator)
                 .WithMany()
                 .HasForeignKey(x => new { x.EliminatorGameId, x.EliminatorEmail })
                 .HasPrincipalKey(nameof(Player.GameId), nameof(Player.Email))
                 .OnDelete(DeleteBehavior.NoAction);

                // Victim -> Player via (VictimGameId, VictimEmail)
                e.HasOne(x => x.Victim)
                 .WithMany()
                 .HasForeignKey(x => new { x.VictimGameId, x.VictimEmail })
                 .HasPrincipalKey(nameof(Player.GameId), nameof(Player.Email))
                 .OnDelete(DeleteBehavior.NoAction);

                e.HasIndex(x => new { x.EliminatorGameId, x.EliminatorEmail });
                e.HasIndex(x => new { x.VictimGameId, x.VictimEmail });
            });
        }
    }
}
