namespace CoonMeeting.Dashboard.Api.Services;

public interface IGuestJoinEmailSender
{
    Task SendGuestInviteAsync(string toEmail, string meetingTitle, string joinUrl);
}
