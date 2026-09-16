namespace CoonMeeting.Dashboard.Api.Services;

public interface IInviteEmailSender
{
    Task SendInviteAsync(string toEmail, string organizationName, string inviteUrl);
}
