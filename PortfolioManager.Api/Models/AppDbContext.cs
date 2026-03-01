using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace PortfolioManager.Api.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<Holding> Holdings { get; set; }
        public DbSet<Otp> Otps { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Existing User index
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            // NEW: OTP Configuration (CRITICAL)
            modelBuilder.Entity<Otp>(entity =>
            {
                entity.HasIndex(e => e.Email);  // Fast email lookups
                entity.HasQueryFilter(e => e.ExpiresAt > DateTime.UtcNow); // Auto-hide expired OTPs
            });

            // Optional: Add FirebaseUid index if you use it
            modelBuilder.Entity<User>().HasIndex(u => u.FirebaseUid);

            base.OnModelCreating(modelBuilder);
        }
    }
}