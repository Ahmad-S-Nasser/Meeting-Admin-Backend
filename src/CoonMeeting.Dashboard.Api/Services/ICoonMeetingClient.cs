namespace CoonMeeting.Dashboard.Api.Services;

public class CoonMeetingTenant
{
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The raw API key - shown to Coon.Meeting exactly once, at creation. Caller must persist it.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

public class CoonMeetingAttendeeRequest
{
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class CreateCoonMeetingMeetingRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? TimeZone { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }

    /// <summary>"Private" or "Any" - passed through as-is, not translated like attendee ids.</summary>
    public string? Visibility { get; set; }

    public CoonMeetingAttendeeRequest Organizer { get; set; } = new();
    public List<CoonMeetingAttendeeRequest> Attendees { get; set; } = new();
}

public class UpdateCoonMeetingMeetingRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? TimeZone { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }

    /// <summary>Null leaves the meeting's current Visibility unchanged.</summary>
    public string? Visibility { get; set; }
}

public class CoonMeetingAttendee
{
    public string Id { get; set; } = string.Empty;
    public string ExternalParticipantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class CoonMeetingMeeting
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? TimeZone { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }
    public string CreatedByExternalId { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByEmail { get; set; }

    /// <summary>"Scheduled" or "Cancelled" - passed through as a string, no local enum needed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>"Private" or "Any" - passed through as a string, no local enum needed.</summary>
    public string Visibility { get; set; } = string.Empty;

    public List<string> BlockedParticipantIds { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CoonMeetingAttendee> Attendees { get; set; } = new();
}

public class CoonMeetingParticipantToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string MeetingId { get; set; } = string.Empty;
}

/// <summary>Thrown for any non-2xx, non-404 response from Coon.Meeting so a caller can react to a
/// specific status (e.g. 400 from participant-tokens on a cancelled meeting) instead of just failing.</summary>
public class CoonMeetingApiException : Exception
{
    public int StatusCode { get; }

    public CoonMeetingApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Calls the already-built, separate Coon.Meeting service server-to-server - the Dashboard is
/// just another integrator of it, same as any third-party product would be.
/// </summary>
public interface ICoonMeetingClient
{
    Task<CoonMeetingTenant> CreateTenantAsync(string name, List<string> allowedOrigins);

    Task<List<CoonMeetingMeeting>> ListMeetingsAsync(string apiKey);

    /// <summary>Null if no meeting with this id exists for the tenant this apiKey belongs to.</summary>
    Task<CoonMeetingMeeting?> GetMeetingAsync(string apiKey, string meetingId);

    Task<CoonMeetingMeeting> CreateMeetingAsync(string apiKey, CreateCoonMeetingMeetingRequest request);

    /// <summary>False if no meeting with this id exists for the tenant this apiKey belongs to.</summary>
    Task<bool> UpdateMeetingAsync(string apiKey, string meetingId, UpdateCoonMeetingMeetingRequest request);

    /// <summary>False if no meeting with this id exists for the tenant this apiKey belongs to.</summary>
    Task<bool> CancelMeetingAsync(string apiKey, string meetingId);

    /// <summary>Null if no meeting with this id exists. Throws CoonMeetingApiException(400) if
    /// the meeting is cancelled, or CoonMeetingApiException(403) if the meeting is Private and
    /// this participantExternalId isn't the organizer or a listed attendee, or is blocked.</summary>
    Task<CoonMeetingParticipantToken?> MintParticipantTokenAsync(string apiKey, string meetingId, string participantExternalId, string name);

    /// <summary>Adds an attendee to an existing meeting (proxies Coon.Meeting's own
    /// AttendeesController.Add) - used to make a mid-call invitee a verified attendee of a
    /// Private meeting.</summary>
    Task<CoonMeetingAttendee> AddAttendeeAsync(string apiKey, string meetingId, CoonMeetingAttendeeRequest attendee);

    Task KickParticipantAsync(string apiKey, string meetingId, string participantExternalId, string requestedByExternalId);
    Task BlockParticipantAsync(string apiKey, string meetingId, string participantExternalId, string requestedByExternalId);
    Task UnblockParticipantAsync(string apiKey, string meetingId, string participantExternalId, string requestedByExternalId);
}
