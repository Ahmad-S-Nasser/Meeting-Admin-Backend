namespace CoonMeeting.Dashboard.Api.Config;

/// <summary>
/// Frontend:BaseUrl is always allowed (it's the Dashboard's own frontend). This carries any
/// OTHER browser-facing origin that legitimately calls this API directly - e.g. a separate
/// standalone app (like the coon-meeting-sdk example app) that calls the public /api/v1/guest
/// endpoints from its own domain.
/// </summary>
public class CorsSettings
{
    /// <summary>Comma-separated list of extra allowed origins, e.g. "https://meeting.coon.one".</summary>
    public string AdditionalAllowedOrigins { get; set; } = string.Empty;

    public IEnumerable<string> Parse() =>
        AdditionalAllowedOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
