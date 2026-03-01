using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PortfolioManager.Api.Services
{
    // The Interface defines the contract for the service
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string email, string otp);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendOtpEmailAsync(string email, string otp)
        {
            // 1. Prepare the email message
            var message = new MimeMessage();
            
            // It is critical that 'FromEmail' is the same as your 'Username' for Gmail
            var fromEmail = _config["EmailSettings:FromEmail"] ?? _config["EmailSettings__FromEmail"];
            var displayName = "Kinetic Capital";

            message.From.Add(new MailboxAddress(displayName, fromEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = $"{otp} is your verification code";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $"""
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

            message.Body = bodyBuilder.ToMessageBody();

            // 2. Configure and use the SMTP Client
            using var client = new SmtpClient();
            
            // Optimization: Set a timeout for the connection
            client.Timeout = 10000; // 10 seconds

            try
            {
                var host = _config["EmailSettings:SmtpHost"] ?? _config["EmailSettings__SmtpHost"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["EmailSettings:SmtpPort"] ?? _config["EmailSettings__SmtpPort"] ?? "587");
                var username = _config["EmailSettings:Username"] ?? _config["EmailSettings__Username"];
                var password = _config["EmailSettings:Password"] ?? _config["EmailSettings__Password"];

                // Gmail Port 587 requires StartTls
                // Gmail Port 465 requires SslOnConnect
                var socketOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

                _logger.LogInformation("Attempting to connect to {Host}:{Port} via {Option}", host, port, socketOption);

                await client.ConnectAsync(host, port, socketOption);
                
                // Authenticate using the App Password
                await client.AuthenticateAsync(username, password);

                await client.SendAsync(message);
                
                _logger.LogInformation("OTP successfully delivered to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver OTP to {Email}", email);
                // Throwing allows the AuthController to catch it and notify the frontend
                throw; 
            }
            finally
            {
                // Always disconnect cleanly
                await client.DisconnectAsync(true);
            }
        }
    }
}