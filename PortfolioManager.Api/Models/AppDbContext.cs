using System;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions; // Ensure this is present

namespace PortfolioManager.Api.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<Holding> Holdings { get; set; }
        public DbSet<Otp> Otps { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Explicitly Map Collections to MongoDB Names
            modelBuilder.Entity<User>().ToCollection("Users");
            modelBuilder.Entity<Holding>().ToCollection("Holdings");
            modelBuilder.Entity<UserProfile>().ToCollection("UserProfiles");
            modelBuilder.Entity<Otp>().ToCollection("Otps");

            // 2. Fix the "Shadow State" UserId1 Warning
            // This tells EF Core that 'UserId' is just a property, not a complex relationship
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.Property(up => up.UserId).HasElementName("UserId");
            });

            // 3. User Indexing
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.FirebaseUid);

            // 4. OTP Configuration
            modelBuilder.Entity<Otp>(entity =>
            {
                entity.HasIndex(e => e.Email);
                // Note: Query filters work, but ensure you manage expiration
                // as MongoDB doesn't enforce this via the driver automatically
                entity.HasQueryFilter(e => e.ExpiresAt > DateTime.UtcNow);
            });

            // 5. Holding Configuration
            modelBuilder.Entity<Holding>(entity =>
            {
                entity.Property(h => h.UserId).HasElementName("UserId");
            });
        }
    }
}
