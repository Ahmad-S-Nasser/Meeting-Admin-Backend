namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class CallTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string MeetingId { get; set; } = string.Empty;

    /// <summary>Resolved from the meeting's Dashboard-side settings for this caller - the call UI
    /// hides the share/record buttons when false. The organizer is always true.</summary>
    public bool CanShareScreen { get; set; }
    public bool CanRecord { get; set; }
}
