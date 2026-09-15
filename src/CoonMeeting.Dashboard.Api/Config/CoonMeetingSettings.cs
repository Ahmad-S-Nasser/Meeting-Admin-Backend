namespace CoonMeeting.Dashboard.Api.Config;

public class CoonMeetingSettings
{
    /// <summary>Not a secret - a public base URL, e.g. "https://api.coon-meeting.example.com".</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Used once, at signup, to call Coon.Meeting's own POST /tenants. Never sent to the Dashboard frontend.</summary>
    public string AdminProvisioningKey { get; set; } = string.Empty;
}
