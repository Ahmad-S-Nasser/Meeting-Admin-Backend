using System.Net;
using System.Net.Mail;
using CoonMeeting.Dashboard.Api.Config;

namespace CoonMeeting.Dashboard.Api.Services;

/// <summary>Adapted from Coon.Meeting's own SmtpEmailSender - same client setup, same
/// never-fail-the-write-that-already-happened philosophy, no ICS needed here.</summary>
public class SmtpInviteEmailSender : IInviteEmailSender
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromAddress;
    private readonly ILogger<SmtpInviteEmailSender> _logger;

    public SmtpInviteEmailSender(SmtpSettings settings, ILogger<SmtpInviteEmailSender> logger)
    {
        _fromAddress = settings.FromAddress;
        _logger = logger;

        _smtpClient = new SmtpClient(settings.Host, settings.Port)
        {
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            EnableSsl = true,
            // SmtpClient's default is 100 seconds - this fires synchronously right after the
            // invite write, so an unreachable mail server must fail fast, not stall the request.
            Timeout = 10_000,
        };
    }

    public async Task SendInviteAsync(string toEmail, string organizationName, string inviteUrl)
    {
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_fromAddress),
                Subject = $"You're invited to join {organizationName} on Coon.Meeting",
                IsBodyHtml = true,
                Body = BuildBody(organizationName, inviteUrl),
            };
            mail.To.Add(toEmail);

            await _smtpClient.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            // Never let invite delivery take down the write that already succeeded.
            _logger.LogWarning(ex, "Failed to email org invite to {Email}.", toEmail);
        }
    }

    private static string BuildBody(string organizationName, string inviteUrl) => $@"
        <div style=""font-family: -apple-system, Segoe UI, Roboto, sans-serif; max-width: 480px;"">
            <h2 style=""color:#0f172a; margin-top:0; font-size:20px;"">You're invited</h2>
            <div style=""background:#f1f5f9; border-left:4px solid #14b8a6; padding:16px; margin:24px 0;"">
                <p style=""margin:0; font-weight:600; color:#0f172a;"">{organizationName}</p>
                <p style=""margin:12px 0 0 0;""><a href=""{inviteUrl}"">Accept invitation</a></p>
            </div>
        </div>";
}
