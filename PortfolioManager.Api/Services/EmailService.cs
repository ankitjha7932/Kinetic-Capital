using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PortfolioManager.Api.Services
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string email, string otp);
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
            // 1. Retrieve API Key and Sender Email from Environment
            var apiKey = _config["EmailSettings__ApiKey"] ?? _config["EmailSettings:ApiKey"];
            var fromEmail = _config["EmailSettings__FromEmail"] ?? _config["EmailSettings:FromEmail"];

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Brevo API Key is missing. Check Render Environment variables.");
                throw new Exception("Email configuration error.");
            }

            // 2. Prepare the Brevo API Payload
            var mailPayload = new
            {
                sender = new { email = fromEmail, name = "Kinetic Capital" },
                to = new[] { new { email = email } },
                subject = $"{otp} is your verification code",
                htmlContent = $"""
                <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 500px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px;">
                    <div style="text-align: center; margin-bottom: 20px;">
                        <h2 style="color: #4f46e5; margin: 0;">Verification Code</h2>
                        <p style="color: #64748b;">Use the code below to sign in to your account.</p>
                    </div>
                    <div style="background: #f8fafc; padding: 24px; border-radius: 8px; text-align: center; border: 1px border-dashed #cbd5e1;">
                        <span style="font-size: 32px; font-weight: bold; color: #1e1b4b; letter-spacing: 8px; font-family: monospace;">
                            {otp}
                        </span>
                    </div>
                    <p style="color: #94a3b8; font-size: 13px; text-align: center; margin-top: 20px;">
                        This code will expire in 10 minutes. <br />
                        If you did not request this code, please ignore this email.
                    </p>
                </div>
                """
            };

            // 3. Construct the HTTP Request
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(mailPayload), Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation("STEP 1: Sending API request to Brevo for {Email}...", email);
                
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SUCCESS: OTP delivered via Brevo API to {Email}", email);
                }
                else
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    _logger.LogError("FAILED: Brevo API returned {Status}. Error: {Error}", response.StatusCode, errorDetails);
                    throw new Exception($"Email delivery failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL: Exception occurred while calling Brevo API for {Email}", email);
                throw;
            }
        }
    }
}