using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ServiceDesk.Api.Services;

public class EmailSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "servicedesk@example.com";
    public bool EnableSsl { get; set; } = true;
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}

/// <summary>
/// Sends notification e-mails over SMTP. Failures are logged and swallowed so that a mail-server
/// outage can never make a ticket operation fail. Addresses are never written to the logs (PII).
/// </summary>
public class SmtpEmailService(IOptions<EmailSettings> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var s = options.Value;
        if (!s.Enabled)
        {
            logger.LogInformation("E-mail disabled - notification '{Subject}' not sent", subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(s.Host, s.Port)
            {
                EnableSsl = s.EnableSsl,
                Credentials = new NetworkCredential(s.Username, s.Password)
            };
            using var message = new MailMessage(s.From, to, subject, body);
            await client.SendMailAsync(message);
            logger.LogInformation("Notification '{Subject}' sent", subject);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send notification '{Subject}'", subject);
        }
    }
}
