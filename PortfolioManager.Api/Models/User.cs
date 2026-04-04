using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Api.Models
{
    [BsonIgnoreExtraElements]
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("firebaseUid")]
        public string? FirebaseUid { get; set; }

        [BsonElement("email")]
        public string? Email { get; set; }

        [BsonElement("passwordHash")]
        public string? PasswordHash { get; set; }

        [BsonElement("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [BsonElement("riskProfile")]
        public string? RiskProfile { get; set; }

        [BsonElement("investmentHorizon")]
        public int InvestmentHorizon { get; set; }

        [BsonElement("preferredSectors")]
        public string? PreferredSectors { get; set; }

        [BsonElement("fullName")]
        public string? FullName { get; set; }

        [BsonElement("resetToken")]
        public string? ResetToken { get; set; }

        [BsonElement("resetTokenExpiry")]
        public DateTime? ResetTokenExpiry { get; set; }

        [BsonElement("lastResetRequest")]
        public DateTime? LastResetRequest { get; set; }

        [BsonElement("googleId")]
        public string? GoogleId { get; set; }

        [BsonElement("createdAt")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Otp
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string Email { get; set; } = string.Empty;
        public string HashedOtp { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Attempts { get; set; } = 0;
        public bool IsVerified { get; set; } = false;
    }

    public record ForgotPasswordRequest(string Email);

    public record ResetPasswordRequest(string Token, string NewPassword);
}
