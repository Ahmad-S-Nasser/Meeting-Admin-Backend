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

    public Task SendGuestInviteAsync(string toEmail, string organizationName, string meetingTitle, string joinUrl) =>
        SendAsync(
            toEmail,
            $"{organizationName}: you're invited to \"{meetingTitle}\"",
            "You're invited to a call",
            organizationName,
            meetingTitle,
            joinUrl,
            "Join the call");

    public Task SendMemberInviteAsync(string toEmail, string organizationName, string meetingTitle, string meetingUrl) =>
        SendAsync(
            toEmail,
            $"{organizationName}: new meeting \"{meetingTitle}\"",
            "You've been added to a meeting",
            organizationName,
            meetingTitle,
            meetingUrl,
            "View meeting");

    private async Task SendAsync(string toEmail, string subject, string heading, string organizationName, string meetingTitle, string url, string ctaLabel)
    {
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_fromAddress),
                Subject = subject,
                IsBodyHtml = true,
                Body = BuildBody(heading, organizationName, meetingTitle, url, ctaLabel),
            };
            mail.To.Add(toEmail);

            await _smtpClient.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            // Never let invite delivery take down the write that already succeeded.
            _logger.LogWarning(ex, "Failed to email meeting invite to {Email}.", toEmail);
        }
    }

    private static string BuildBody(string heading, string organizationName, string meetingTitle, string url, string ctaLabel) => $@"
        <div style=""font-family: -apple-system, Segoe UI, Roboto, sans-serif; max-width: 480px;"">
            <p style=""margin:0 0 4px 0; font-size:12px; font-weight:600; letter-spacing:.04em; text-transform:uppercase; color:#2ab7ca;"">{organizationName}</p>
            <h2 style=""color:#0f172a; margin-top:0; font-size:20px;"">{heading}</h2>
            <div style=""background:#f1f5f9; border-left:4px solid #14b8a6; padding:16px; margin:24px 0;"">
                <p style=""margin:0; font-weight:600; color:#0f172a;"">{meetingTitle}</p>
                <p style=""margin:12px 0 0 0;""><a href=""{url}"">{ctaLabel}</a></p>
            </div>
        </div>";
}
