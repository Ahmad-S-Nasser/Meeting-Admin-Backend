namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class PermissionPolicyDto
{
    /// <summary>"OrganizerOnly", "Selected" or "Everyone".</summary>
    public string Mode { get; set; } = PermissionMode.OrganizerOnly;
    public List<string> AllowedParticipantIds { get; set; } = new();
}

/// <summary>An attendee the organizer can pick for the "Selected" mode.</summary>
public class SelectablePersonDto
{
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class MeetingSettingsDto
{
    public PermissionPolicyDto ScreenShare { get; set; } = new();
    public PermissionPolicyDto Recording { get; set; } = new();
    public List<SelectablePersonDto> People { get; set; } = new();
}

public class UpdateMeetingSettingsDto
{
    public PermissionPolicyDto ScreenShare { get; set; } = new();
    public PermissionPolicyDto Recording { get; set; } = new();
}

public class MyPermissionsDto
{
    public bool CanShareScreen { get; set; }
    public bool CanRecord { get; set; }
}

public class InviteLinkDto
{
    public string Url { get; set; } = string.Empty;

    /// <summary>"join-link" - the reusable guest link the organizer already created (anyone with
    /// it can join). "meeting-page" - the Dashboard page for this meeting; only invited people
    /// can open it, so copying it doesn't by itself let anyone new in.</summary>
    public string Kind { get; set; } = string.Empty;
}
