using Microsoft.EntityFrameworkCore;
using LksBrothers.Explorer.Models;

namespace LksBrothers.Explorer.Data;

public class ExplorerDbContext : DbContext
{
    public ExplorerDbContext(DbContextOptions<ExplorerDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<UserActivity> UserActivities { get; set; }
    public DbSet<SavedSearch> SavedSearches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.ApiKey).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Username).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        // UserSession configuration
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sessions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // UserActivity configuration
        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Activities)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.CreatedAt);
        });

        // SavedSearch configuration
        modelBuilder.Entity<SavedSearch>(entity =>
        {
            entity.HasOne(e => e.User)
                  .WithMany(u => u.SavedSearches)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed default admin user
        var adminUserId = Guid.NewGuid();
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminUserId,
                Email = "admin@lksnetwork.com",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("LKSNetwork2025!"),
                FirstName = "LKS",
                LastName = "Administrator",
                Role = "Admin",
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ApiKey = Guid.NewGuid().ToString("N"),
                ApiCallLimit = 10000
            }
        );
    }
}
