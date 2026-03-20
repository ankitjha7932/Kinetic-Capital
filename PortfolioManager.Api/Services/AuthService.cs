using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioManager.Api.Models;
using static BCrypt.Net.BCrypt;

namespace PortfolioManager.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IEmailService emailService, IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
    }

    public async Task<(bool Success, string Message)> SendOtpAsync(string email, bool isRegistration = false)
    {
        // Normalize email to ensure check works regardless of case/spaces
        email = email.Trim().ToLower();

        if (isRegistration && await _db.Users.AnyAsync(u => u.Email == email))
            return (false, "This email is already registered. Please sign in.");

        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        if (await _db.Otps.CountAsync(o => o.Email == email && o.CreatedAt > oneHourAgo) >= 3)
            return (false, "Too many requests. Try again in an hour.");

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var hashedOtp = HashPassword(otpCode);

        _db.Otps.RemoveRange(_db.Otps.Where(o => o.Email == email));
        _db.Otps.Add(new Otp { Email = email, HashedOtp = hashedOtp, ExpiresAt = DateTime.UtcNow.AddMinutes(10) });

        await _db.SaveChangesAsync();
        await _emailService.SendOtpEmailAsync(email, otpCode);
        return (true, "OTP sent successfully.");
    }

    public async Task<(bool Success, string Message, string? Token, string? UserId)> VerifyOtpAsync(string email, string otp)
    {
        email = email.Trim().ToLower();
        var otpRecord = await _db.Otps
            .Where(o => o.Email == email && !o.IsVerified)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null || otpRecord.Attempts >= 5 || !Verify(otp, otpRecord.HashedOtp))
        {
            if (otpRecord != null) { otpRecord.Attempts++; await _db.SaveChangesAsync(); }
            return (false, "Invalid or expired OTP.", null, null);
        }

        otpRecord.IsVerified = true;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email) ?? new User { Email = email };

        if (string.IsNullOrEmpty(user.Id))
        {
            user.Id = Guid.NewGuid().ToString();
            _db.Users.Add(user);
        }

        await _db.SaveChangesAsync();
        return (true, "Verified", GenerateJwt(user.Id, user.Email!), user.Id);
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
    {
        email = email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return (false, "Email not registered.");

        if (user.LastResetRequest > DateTime.UtcNow.AddMinutes(-15))
            return (false, "Please wait 15 minutes before requesting again.");

        string token = Guid.NewGuid().ToString();
        string resetLink = $"https://kinetic-capital.vercel.app/reset-password?token={token}";

        try
        {
            await _emailService.SendResetEmailAsync(email, resetLink);
            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            user.LastResetRequest = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (true, "Reset link sent.");
        }
        catch { return (false, "Email service failed. Try again later."); }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ResetToken == token && u.ResetTokenExpiry > DateTime.UtcNow);
        if (user == null) return (false, "Invalid or expired token.");

        user.PasswordHash = HashPassword(newPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();
        return (true, "Password updated.");
    }

    public async Task<(bool Success, string Message, string? Token, string? UserId)> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !Verify(password, user.PasswordHash))
            return (false, "Invalid credentials", null, null);

        return (true, "Success", GenerateJwt(user.Id, user.Email!), user.Id);
    }

    public string GenerateJwt(string userId, string email)
    {
        var keyString = Environment.GetEnvironmentVariable("JWT_KEY") ?? _config["Jwt:Key"];
        if (string.IsNullOrEmpty(keyString) || keyString.Length < 16)
            throw new Exception("JWT Key missing or too short.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, userId), new Claim(JwtRegisteredClaimNames.Email, email) },
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}