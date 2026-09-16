namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class CallTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string MeetingId { get; set; } = string.Empty;
}
