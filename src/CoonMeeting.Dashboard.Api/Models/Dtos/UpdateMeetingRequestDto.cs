using System.ComponentModel.DataAnnotations;

namespace CoonMeeting.Dashboard.Api.Models.Dtos;

/// <summary>Only the fields an edit form sends - attendees aren't editable here in v1
/// (matching Coon.Meeting's own UpdateMeetingDto, which never touches Attendees either).</summary>
public class UpdateMeetingRequestDto
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

    /// <summary>Null leaves the meeting's current Visibility unchanged.</summary>
    public string? Visibility { get; set; }
}
