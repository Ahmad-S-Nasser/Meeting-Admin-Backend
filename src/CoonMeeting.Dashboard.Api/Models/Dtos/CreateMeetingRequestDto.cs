using System.ComponentModel.DataAnnotations;

namespace CoonMeeting.Dashboard.Api.Models.Dtos;

/// <summary>An attendee by email only - the Dashboard resolves whether that email belongs to an
/// org member (and if so, which User.Id/Name to use) server-side; the client never supplies an
/// external id directly.</summary>
public class CreateMeetingAttendeeRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Only used for a non-member attendee - an org member's own name always wins.</summary>
    public string? Name { get; set; }
}

public class CreateMeetingRequestDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime ScheduledAt { get; set; }

    public string? TimeZone { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }

    /// <summary>"Private" or "Any" - null defaults to Private on the Coon.Meeting side.</summary>
    public string? Visibility { get; set; }

    public List<CreateMeetingAttendeeRequestDto> Attendees { get; set; } = new();
}
