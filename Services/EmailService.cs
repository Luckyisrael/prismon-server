using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Prismon.Api.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string code);
    }

    public class EmailService : IEmailService
    {
        private readonly SendGridClient _client;
        private readonly ILogger<EmailService> _logger;
        private readonly EmailConfig _config;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _logger = logger;

            _config = new EmailConfig
            {
                ApiKey = configuration["SENDGRID_API_KEY"] ?? string.Empty,
                FromEmail = configuration["SENDGRID_FROM_EMAIL"] ?? string.Empty
            };

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("SendGrid API key is missing. Email sending will be disabled.");
                _client = null!; // Set to null; handle in SendVerificationEmailAsync
            }
            else
            {
                _client = new SendGridClient(_config.ApiKey);
            }
        }

        public async Task SendVerificationEmailAsync(string email, string code)
        {
            if (_client == null)
            {
                _logger.LogWarning("Cannot send verification email to {Email}: SendGrid API key is missing", email);
                throw new InvalidOperationException("Email service is disabled due to missing SendGrid API key");
            }

            try
            {
                var from = new EmailAddress(_config.FromEmail, "Prismon");
                var to = new EmailAddress(email);
                var subject = "Prismon Verification Code";
                var htmlContent = $"<p>Your verification code is: <strong>{code}</strong></p><p>It expires in 10 minutes.</p>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
                var response = await _client.SendEmailAsync(msg);

                if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
                    throw new Exception($"SendGrid failed: {response.StatusCode}");

                _logger.LogInformation("Verification email sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", email);
                throw;
            }
        }
    }

    public class EmailConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
    }
}