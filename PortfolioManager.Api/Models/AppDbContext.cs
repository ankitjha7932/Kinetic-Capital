using System;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

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
        public DbSet<StockFundamental> Stocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToCollection("Users");
            modelBuilder.Entity<Holding>().ToCollection("Holdings");
            modelBuilder.Entity<UserProfile>().ToCollection("UserProfiles");
            modelBuilder.Entity<Otp>().ToCollection("Otps");
            modelBuilder.Entity<StockFundamental>().ToCollection("StocksDeepData");

            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.Property(up => up.UserId).HasElementName("UserId");
            });

            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.FirebaseUid);

            modelBuilder.Entity<Otp>(entity =>
            {
                entity.HasIndex(e => e.Email);
                entity.HasQueryFilter(e => e.ExpiresAt > DateTime.UtcNow);
            });

            modelBuilder.Entity<Holding>(entity =>
            {
                entity.Property(h => h.UserId).HasElementName("UserId");
            });
        }
    }
}
