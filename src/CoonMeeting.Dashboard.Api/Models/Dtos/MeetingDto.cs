namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class MeetingAttendeeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>A blocked participant, resolved back to a name/email where they're an org member
/// (their raw Coon.Meeting external id is just their Dashboard UserId, meaningless to show
/// as-is) - see MeetingsController.ToBlockedParticipantDto.</summary>
public class BlockedParticipantDto
{
    public string ParticipantExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class MeetingDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? TimeZone { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>"Scheduled" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>"Private" or "Any".</summary>
    public string Visibility { get; set; } = string.Empty;

    /// <summary>Whether the current session's user organizes this meeting - gates the
    /// kick/block/invite UI, since only the organizer may use those.</summary>
    public bool IsOrganizer { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<MeetingAttendeeDto> Attendees { get; set; } = new();
    public List<BlockedParticipantDto> BlockedParticipants { get; set; } = new();
}
