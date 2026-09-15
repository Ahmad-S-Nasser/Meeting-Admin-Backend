namespace CoonMeeting.Dashboard.Api.Config;

public class SessionSettings
{
    public string Issuer { get; set; } = "coon-meeting-dashboard";
    public string Audience { get; set; } = "coon-meeting-dashboard-users";
    public string SigningKey { get; set; } = string.Empty;
    public int TtlHours { get; set; } = 12;
}
