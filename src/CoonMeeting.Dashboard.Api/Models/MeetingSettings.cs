namespace CoonMeeting.Dashboard.Api.Models;

public static class PermissionMode
{
    public const string OrganizerOnly = "OrganizerOnly";
    public const string Selected = "Selected";
    public const string Everyone = "Everyone";

    public static bool IsValid(string? mode) =>
        mode is OrganizerOnly or Selected or Everyone;
}

/// <summary>Who may use one in-call capability (screen sharing or recording). The organizer always
/// may, whatever this says.</summary>
public class PermissionPolicy
{
    public string Mode { get; set; } = PermissionMode.OrganizerOnly;

    /// <summary>Coon.Meeting external ids - only consulted when Mode is Selected. An org member's
    /// User.Id, or the lower-cased email for a non-member (MeetingsController.ResolveAttendeeExternalId).</summary>
    public List<string> AllowedParticipantIds { get; set; } = new();
}

/// <summary>
/// Dashboard-side, per-meeting call settings. Deliberately NOT stored in Coon.Meeting: the core
/// has no notion of who may share or record, and integrators decide that themselves. Top-level
/// collection keyed by the Coon.Meeting meeting id, same reasoning as MeetingJoinLink. A meeting
/// with no row behaves as OrganizerOnly for everything.
/// </summary>
public class MeetingSettings
{
    /// <summary>The Coon.Meeting meeting id.</summary>
    public string Id { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public PermissionPolicy ScreenShare { get; set; } = new();
    public PermissionPolicy Recording { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
