namespace CoonMeeting.Dashboard.Api.Config;

public class FrontendSettings
{
    /// <summary>Used as the Coon.Meeting tenant's AllowedOrigins entry at signup, and as the base for invite links.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
