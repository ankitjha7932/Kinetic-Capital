using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PortfolioManager.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? FirebaseUid { get; set; } // Add this!
        public string? Email { get; set; }
        public string? PasswordHash { get; set; } // Make nullable for OTP users
        public string? PhoneNumber { get; set; }  // Add this!
        public string? RiskProfile { get; set; }
        public int InvestmentHorizon { get; set; }
        public string? PreferredSectors { get; set; }
    }

    public class Otp
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string HashedOtp { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Attempts { get; set; } = 0;
        public bool IsVerified { get; set; } = false;
    }

}