using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

        public DbSet<User> Users { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<Bike> Bikes { get; set; }
        public DbSet<ChainCycle> ChainCycles { get; set; }
        public DbSet<BikePart> BikeParts { get; set; }
        public DbSet<PartUsageHistory> PartUsageHistories { get; set; }
        public DbSet<ExternalServiceIntegration> ExternalServiceIntegrations { get; set; }
        public DbSet<StravaAthlete> StravaAthletes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.Bikes)
                .WithOne(b => b.User)
                .HasForeignKey(b => b.UserId);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Parts)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Bike>()
                .HasMany(b => b.Parts)
                .WithOne(p => p.Bike)
                .HasForeignKey(p => p.BikeId)
                .IsRequired(false);

            modelBuilder.Entity<ChainCycle>()
                .HasOne(c => c.Bike)
                .WithMany()
                .HasForeignKey(c => c.BikeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BikePart>()
                .HasMany(p => p.UsageHistory)
                .WithOne(h => h.BikePart)
                .HasForeignKey(h => h.BikePartId);

            // External service integration relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.ExternalServiceIntegrations)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExternalServiceIntegration>()
                .HasOne(e => e.StravaAthlete)
                .WithOne(s => s.Integration)
                .HasForeignKey<StravaAthlete>(s => s.IntegrationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure one Strava integration per user
            modelBuilder.Entity<ExternalServiceIntegration>()
                .HasIndex(e => new { e.UserId, e.ServiceType })
                .IsUnique();

            // User settings one-to-one relationship
            modelBuilder.Entity<UserSettings>()
                .HasOne(us => us.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}