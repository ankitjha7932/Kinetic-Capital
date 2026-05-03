using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PortfolioManager.Api.Models;
using static BCrypt.Net.BCrypt;

namespace PortfolioManager.Api.Services;

public class AuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Otp> _otps;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthService(IMongoDatabase db, IEmailService emailService, IConfiguration config)
    {
        _users = db.GetCollection<User>("Users");
        _otps = db.GetCollection<Otp>("Otps");
        _emailService = emailService;
        _config = config;
    }

    public async Task<(bool Success, string Message)> SendOtpAsync(
        string email,
        bool isRegistration = false
    )
    {
        email = email.Trim().ToLower();

        if (isRegistration && await _users.Find(u => u.Email == email).AnyAsync())
            return (false, "This email is already registered. Please sign in.");

        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentCount = await _otps.CountDocumentsAsync(o =>
            o.Email == email && o.CreatedAt > oneHourAgo
        );
        if (recentCount >= 3)
            return (false, "Too many requests. Try again in an hour.");

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var hashedOtp = HashPassword(otpCode);

        await _otps.DeleteManyAsync(o => o.Email == email);
        await _otps.InsertOneAsync(
            new Otp
            {
                Email = email,
                HashedOtp = hashedOtp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow,
            }
        );

        await _emailService.SendOtpEmailAsync(email, otpCode);
        return (true, "OTP sent successfully.");
    }

    public async Task<(bool Success, string Message, string? Token, string? UserId)> VerifyOtpAsync(
        string email,
        string otp
    )
    {
        email = email.Trim().ToLower();

        var otpRecord = await _otps
            .Find(o => o.Email == email && !o.IsVerified)
            .SortByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (
            otpRecord == null
            || otpRecord.Attempts >= 5
            || otpRecord.ExpiresAt < DateTime.UtcNow
            || !Verify(otp, otpRecord.HashedOtp)
        )
        {
            if (otpRecord != null)
            {
                await _otps.UpdateOneAsync(
                    o => o.Id == otpRecord.Id,
                    Builders<Otp>.Update.Inc(o => o.Attempts, 1)
                );
            }
            return (false, "Invalid or expired OTP.", null, null);
        }

        await _otps.UpdateOneAsync(
            o => o.Id == otpRecord.Id,
            Builders<Otp>.Update.Set(o => o.IsVerified, true)
        );

        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user == null)
        {
            user = new User { Email = email, CreatedAt = DateTime.UtcNow };
            await _users.InsertOneAsync(user);
        }

        return (true, "Verified", GenerateJwt(user.Id, user.Email!), user.Id);
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
    {
        email = email.Trim().ToLower();
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user == null)
            return (false, "Email not registered.");

        if (user.LastResetRequest > DateTime.UtcNow.AddMinutes(-15))
            return (false, "Please wait 15 minutes before requesting again.");

        string token = Guid.NewGuid().ToString();

        // Previously the token was saved after sending, meaning if the DB write
        // failed the user would get a link with a token that doesn't exist.
        try
        {
            await _users.UpdateOneAsync(
                u => u.Id == user.Id,
                Builders<User>
                    .Update.Set(u => u.ResetToken, token)
                    .Set(u => u.ResetTokenExpiry, DateTime.UtcNow.AddHours(1))
                    .Set(u => u.LastResetRequest, DateTime.UtcNow)
            );
        }
        catch
        {
            return (false, "Failed to generate reset token. Try again.");
        }

        // both work. Set FRONTEND_URL=http://localhost:5173 in your .env for
        // local dev, and FRONTEND_URL=https://kinetic-capital.vercel.app on Render.
        var frontendUrl =
            Environment.GetEnvironmentVariable("FRONTEND_URL")
            ?? _config["App:FrontendUrl"]
            ?? "https://kinetic-capital.vercel.app";

        string resetLink = $"{frontendUrl}/reset-password?token={token}";

        try
        {
            await _emailService.SendResetEmailAsync(email, resetLink);
            return (true, "Reset link sent.");
        }
        catch
        {
            // Email failed — clear the token so it can't be used with a link
            // that was never delivered.
            await _users.UpdateOneAsync(
                u => u.Id == user.Id,
                Builders<User>
                    .Update.Set(u => u.ResetToken, null)
                    .Set(u => u.ResetTokenExpiry, null)
                    .Set(u => u.LastResetRequest, null)
            );
            return (false, "Email service failed. Try again later.");
        }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        string token,
        string newPassword
    )
    {
        var user = await _users
            .Find(u => u.ResetToken == token && u.ResetTokenExpiry > DateTime.UtcNow)
            .FirstOrDefaultAsync();
        if (user == null)
            return (false, "Invalid or expired token.");

        await _users.UpdateOneAsync(
            u => u.Id == user.Id,
            Builders<User>
                .Update.Set(u => u.PasswordHash, HashPassword(newPassword))
                .Set(u => u.ResetToken, null)
                .Set(u => u.ResetTokenExpiry, null)
        );
        return (true, "Password updated.");
    }

    public async Task<(bool Success, string Message, string? Token, string? UserId)> LoginAsync(
        string email,
        string password
    )
    {
        email = email.Trim().ToLower();
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();

        if (
            user == null
            || string.IsNullOrEmpty(user.PasswordHash)
            || !Verify(password, user.PasswordHash)
        )
            return (false, "Invalid credentials", null, null);

        return (true, "Success", GenerateJwt(user.Id, user.Email!), user.Id);
    }

    public async Task<(
        bool Success,
        string Message,
        string? Token,
        string? UserId
    )> LoginWithGoogleAsync(string googleToken)
    {
        try
        {
            var clientId =
                Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                ?? Environment.GetEnvironmentVariable("VITE_GOOGLE_CLIENT_ID")
                ?? _config["Google:ClientId"];

            if (string.IsNullOrEmpty(clientId))
                return (false, "Google Client ID is not configured on server.", null, null);

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { clientId.Trim() },
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(googleToken, settings);
            string email = payload.Email.ToLower().Trim();

            var user = await _users
                .Find(u => u.GoogleId == payload.Subject || u.Email == email)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    GoogleId = payload.Subject,
                    FullName = payload.Name,
                    CreatedAt = DateTime.UtcNow,
                };
                await _users.InsertOneAsync(user);
            }
            else if (string.IsNullOrEmpty(user.GoogleId))
            {
                await _users.UpdateOneAsync(
                    u => u.Id == user.Id,
                    Builders<User>
                        .Update.Set(u => u.GoogleId, payload.Subject)
                        .Set(u => u.FullName, user.FullName ?? payload.Name)
                );
            }

            return (true, "Success", GenerateJwt(user.Id, user.Email!), user.Id);
        }
        catch (Exception ex)
        {
            return (false, $"Google authentication failed: {ex.Message}", null, null);
        }
    }

    public string GenerateJwt(string userId, string email)
    {
        var keyString =
            Environment.GetEnvironmentVariable("JWT_KEY")
            ?? Environment.GetEnvironmentVariable("Jwt__Key")
            ?? _config["Jwt:Key"];

        if (string.IsNullOrEmpty(keyString))
            throw new Exception("JWT Key is missing. Check Render environment variables.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.NameIdentifier, userId),
            },
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
