using System.Net;
using System.Net.Mail;
using CoonMeeting.Dashboard.Api.Config;

namespace CoonMeeting.Dashboard.Api.Services;

/// <summary>Same SmtpClient setup and best-effort philosophy as SmtpInviteEmailSender - a
/// separate sender because this one invites someone to a specific call, not to the org.</summary>
public class SmtpGuestJoinEmailSender : IGuestJoinEmailSender
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromAddress;
    private readonly ILogger<SmtpGuestJoinEmailSender> _logger;

    public SmtpGuestJoinEmailSender(SmtpSettings settings, ILogger<SmtpGuestJoinEmailSender> logger)
    {
        _fromAddress = settings.FromAddress;
        _logger = logger;

        _smtpClient = new SmtpClient(settings.Host, settings.Port)
        {
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            EnableSsl = true,
            Timeout = 10_000,
        };
    }

    public async Task SendGuestInviteAsync(string toEmail, string meetingTitle, string joinUrl)
    {
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_fromAddress),
                Subject = $"You're invited to join \"{meetingTitle}\"",
                IsBodyHtml = true,
                Body = BuildBody(meetingTitle, joinUrl),
            };
            mail.To.Add(toEmail);

            await _smtpClient.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            // Never let invite delivery take down the write that already succeeded.
            _logger.LogWarning(ex, "Failed to email guest join link to {Email}.", toEmail);
        }
    }

    private static string BuildBody(string meetingTitle, string joinUrl) => $@"
        <div style=""font-family: -apple-system, Segoe UI, Roboto, sans-serif; max-width: 480px;"">
            <h2 style=""color:#0f172a; margin-top:0; font-size:20px;"">You're invited to a call</h2>
            <div style=""background:#f1f5f9; border-left:4px solid #14b8a6; padding:16px; margin:24px 0;"">
                <p style=""margin:0; font-weight:600; color:#0f172a;"">{meetingTitle}</p>
                <p style=""margin:12px 0 0 0;""><a href=""{joinUrl}"">Join the call</a></p>
            </div>
        </div>";
}
