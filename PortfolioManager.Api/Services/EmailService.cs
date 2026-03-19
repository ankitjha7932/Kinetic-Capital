using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PortfolioManager.Api.Services
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string email, string otp);
        Task SendResetEmailAsync(string email, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task SendOtpEmailAsync(string email, string otp)
        {
            var html = $"""
                    <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 500px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 16px;">
                        <div style="text-align: center; margin-bottom: 24px;">
                            <h1 style="color: #4f46e5; margin: 0; font-size: 24px;">Kinetic Capital</h1>
                        </div>
                        
                        <p style="color: #1e293b; font-size: 16px;">Hi there,</p>
                        <p style="color: #475569; line-height: 1.5;">Use the verification code below to securely sign in to your investment dashboard.</p>
                        
                        <div style="background: #f8fafc; padding: 32px; border-radius: 12px; text-align: center; margin: 24px 0; border: 1px dashed #cbd5e1;">
                            <span style="font-size: 36px; font-weight: 800; color: #1e1b4b; letter-spacing: 6px; font-family: 'Courier New', monospace;">{otp}</span>
                        </div>
                        
                        <p style="color: #94a3b8; font-size: 13px; text-align: center;">This code expires in 10 minutes. If you didn't request this, please ignore this email.</p>
                        <hr style="border: 0; border-top: 1px solid #f1f5f9; margin: 20px 0;" />
                        <p style="color: #cbd5e1; font-size: 11px; text-align: center;">© 2026 Kinetic Capital. All rights reserved.</p>
                    </div>
                """;

            await PostToBrevo(email, $"{otp} is your verification code", html);
        }

        public async Task SendResetEmailAsync(string email, string resetLink)
        {
            var html = $"""
                    <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 500px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 16px;">
                        <div style="text-align: center; margin-bottom: 24px;">
                            <h1 style="color: #4f46e5; margin: 0; font-size: 24px;">Kinetic Capital</h1>
                        </div>

                        <p style="color: #1e293b; font-size: 16px;">Hi there,</p>
                        <p style="color: #475569; line-height: 1.5;">We received a request to reset the password for your Kinetic Capital account. Click the button below to set a new one:</p>
                        
                        <div style="text-align: center; margin: 32px 0;">
                            <a href="{resetLink}" style="background: #4f46e5; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 10px; font-weight: bold; font-size: 16px; display: inline-block;">Reset Password</a>
                        </div>
                        
                        <p style="color: #64748b; font-size: 13px;">For security, this link will expire in 60 minutes. If you did not make this request, your password will remain unchanged.</p>
                        <hr style="border: 0; border-top: 1px solid #f1f5f9; margin: 20px 0;" />
                        <p style="color: #cbd5e1; font-size: 11px; text-align: center;">© 2026 Kinetic Capital. Your Data-Driven Investing Partner.</p>
                    </div>
                """;

            await PostToBrevo(email, "Reset your Kinetic Capital password", html);
        }

        private async Task PostToBrevo(string email, string subject, string htmlContent)
        {
            var apiKey =
                Environment.GetEnvironmentVariable("BREVO_API_KEY")
                ?? _config["EmailSettings:ApiKey"];

            // --- DEBUG CHECK ---
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError(
                    "CRITICAL: Brevo API Key is NULL or Empty. Check your .env file for BREVO_API_KEY."
                );
                throw new Exception("Email configuration error: API Key missing.");
            }

            var fromEmail =
                Environment.GetEnvironmentVariable("BREVO_FROM_EMAIL")
                ?? _config["EmailSettings:FromEmail"]
                ?? "ankitjhastudy@gmail.com"; 

            var payload = new
            {
                sender = new { email = fromEmail, name = "Kinetic Capital" },
                to = new[] { new { email = email } },
                subject = subject,
                htmlContent = htmlContent,
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.brevo.com/v3/smtp/email"
            )
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                ),
            };

            request.Headers.Clear(); 
            request.Headers.Add("api-key", apiKey);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Brevo API Error: {Error}", error);
                throw new Exception("Email service unavailable.");
            }
        }
    }
}
