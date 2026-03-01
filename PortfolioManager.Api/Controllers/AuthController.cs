using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using System.Security.Claims;
using System.Text;
using PortfolioManager.Api.Services;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using static BCrypt.Net.BCrypt;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IEmailService emailService, IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
    }

    // NEW: Send OTP (Step 1)
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        // 1. Basic Validation
        if (string.IsNullOrEmpty(request.Email) || !request.Email.Contains("@"))
            return BadRequest("A valid email address is required.");

        try
        {
            // 2. Rate Limiting: Max 3 OTPs per hour per email
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var recentRequestCount = await _db.Otps
                .CountAsync(o => o.Email == request.Email && o.CreatedAt > oneHourAgo);

            if (recentRequestCount >= 3)
                return StatusCode(429, "Too many requests. Please try again in an hour.");

            // 3. Generate 6-digit OTP
            var otpCode = Random.Shared.Next(100000, 999999).ToString();
            var hashedOtp = HashPassword(otpCode); // Using BCrypt

            // 4. Cleanup: Remove any existing OTPs for this specific email
            var oldOtps = _db.Otps.Where(o => o.Email == request.Email);
            _db.Otps.RemoveRange(oldOtps);

            // 5. Save new OTP to Database
            var otpRecord = new Otp
            {
                Email = request.Email,
                HashedOtp = hashedOtp,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // 10 minute window
                Attempts = 0,
                IsVerified = false
            };

            _db.Otps.Add(otpRecord);
            await _db.SaveChangesAsync();

            // 6. Send the Email 
            // We await this directly now so you can see errors in your terminal/logs
            try
            {
                await _emailService.SendOtpEmailAsync(request.Email, otpCode);
            }
            catch (Exception ex)
            {
                // Log the actual error here (ex.Message)
                return StatusCode(500, "Email delivery failed. Check SMTP configuration.");
            }

            return Ok(new { message = "If an account exists, an OTP has been sent." });
        }
        catch (Exception)
        {
            return StatusCode(500, "An internal error occurred.");
        }
    }

    // NEW: Verify OTP (Step 2 - login/register)
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var otpRecord = await _db.Otps
            .Where(o => o.Email == request.Email && !o.IsVerified)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null)
            return BadRequest("No OTP found. Please request a new one.");

        if (otpRecord.Attempts >= 5)
        {
            _db.Otps.Remove(otpRecord);
            await _db.SaveChangesAsync();
            return BadRequest("Too many attempts. Request new OTP.");
        }

        if (!Verify(request.Otp, otpRecord.HashedOtp))
        {
            otpRecord.Attempts++;
            await _db.SaveChangesAsync();
            return BadRequest("Invalid OTP");
        }

        // OTP valid! Mark verified
        otpRecord.IsVerified = true;
        await _db.SaveChangesAsync();

        // Check if user exists (login vs register)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            // Auto-register new user
            user = new User
            {
                Email = request.Email,
                RiskProfile = "Moderate",
                InvestmentHorizon = 5,
                PreferredSectors = ""
                // PasswordHash intentionally empty for OTP users
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var token = GenerateJwtToken(user.Id.ToString(), user.Email);
        return Ok(new { token, userId = user.Id });
    }

    // === YOUR EXISTING CODE (UNCHANGED) ===
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest("Email exists");

        var passwordHash = HashPassword(request.Password);
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            RiskProfile = request.RiskProfile,
            InvestmentHorizon = request.InvestmentHorizon,
            PreferredSectors = string.Join(",", request.PreferredSectors)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { userId = user.Id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Check if user exists and password matches
        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid email or password.");
        }

        var token = GenerateJwtToken(user.Id.ToString(), user.Email);
        return Ok(new { token, userId = user.Id });
    }

    private string GenerateJwtToken(string userId, string email)
    {
        var claims = new[] {
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Email, email)
    };

        // This pulls the long key you pasted into appsettings.json
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Authorize]
    [HttpPost("sync")]
    public async Task<IActionResult> SyncUser()
    {
        var firebaseUid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var phone = User.FindFirst("phone_number")?.Value;

        if (string.IsNullOrEmpty(firebaseUid))
            return Unauthorized("Firebase UID not found in token.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

        if (user == null)
        {
            user = new User
            {
                FirebaseUid = firebaseUid,
                Email = email,
                PhoneNumber = phone,
                RiskProfile = "Medium",
                InvestmentHorizon = 1,
                PreferredSectors = ""
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else
        {
            if (user.Email != email || user.PhoneNumber != phone)
            {
                user.Email = email ?? user.Email;
                user.PhoneNumber = phone ?? user.PhoneNumber;
                await _db.SaveChangesAsync();
            }
        }

        return Ok(new { userId = user.Id, email = user.Email });
    }

    [HttpPost("verify-otp-register")]
    public async Task<IActionResult> VerifyOtpRegister([FromBody] RegisterRequest request)
    {
        // 1. Validate OTP first
        var otpRecord = await _db.Otps
            .Where(o => o.Email == request.Email && !o.IsVerified)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null || !Verify(request.Otp, otpRecord.HashedOtp))
            return BadRequest("Invalid or expired OTP.");

        // 2. Check if user already exists
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest("User already registered. Please login.");

        // 3. Create the user with ALL details
        var user = new User
        {
            Email = request.Email,
            PasswordHash = HashPassword(request.Password), // Securely hash the password
            RiskProfile = request.RiskProfile ?? "Moderate",
            InvestmentHorizon = request.InvestmentHorizon,
            PreferredSectors = string.Join(",", request.PreferredSectors ?? new string[] { })
        };

        _db.Users.Add(user);
        otpRecord.IsVerified = true; // Mark OTP as used
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user.Id.ToString(), user.Email);
        return Ok(new { token, userId = user.Id, message = "Registration successful!" });
    }
}
