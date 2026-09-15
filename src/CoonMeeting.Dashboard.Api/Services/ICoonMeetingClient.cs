namespace CoonMeeting.Dashboard.Api.Services;

public class CoonMeetingTenant
{
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The raw API key - shown to Coon.Meeting exactly once, at creation. Caller must persist it.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Calls the already-built, separate Coon.Meeting service server-to-server - the Dashboard is
/// just another integrator of it, same as any third-party product would be.
/// </summary>
public interface ICoonMeetingClient
{
    Task<CoonMeetingTenant> CreateTenantAsync(string name, List<string> allowedOrigins);
}
