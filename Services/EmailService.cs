using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string code);
}

public class EmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly ILogger<EmailService> _logger;
    private readonly EmailConfig _config;

    public EmailService(IOptions<EmailConfig> config, ILogger<EmailService> logger)
    {
        _config = config.Value;
        _client = new SendGridClient(_config.ApiKey);
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string email, string code)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
                throw new InvalidOperationException("SendGrid API key is missing");

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