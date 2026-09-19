namespace CoonMeeting.Dashboard.Api.Services;

public interface IGuestJoinEmailSender
{
    /// <summary>A non-member's invite to join a specific call as a guest - the link mints them a
    /// participant token directly, no account needed.</summary>
    Task SendGuestInviteAsync(string toEmail, string organizationName, string meetingTitle, string joinUrl);

    /// <summary>An org member's notice that they were added to a meeting - the link is a normal
    /// Dashboard URL, gated by their own login rather than a one-shot token.</summary>
    Task SendMemberInviteAsync(string toEmail, string organizationName, string meetingTitle, string meetingUrl);
}
