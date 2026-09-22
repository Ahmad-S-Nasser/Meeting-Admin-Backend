using System.ComponentModel.DataAnnotations;

namespace CoonMeeting.Dashboard.Api.Models.Dtos;

public class CreateGuestInviteDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }
}

public class MeetingJoinLinkResponseDto
{
    public string Url { get; set; } = string.Empty;
}

public class GuestJoinPreviewDto
{
    public string MeetingId { get; set; } = string.Empty;
    public string MeetingTitle { get; set; } = string.Empty;

    /// <summary>MeetingJoinLinkScope.Any or .Attendee.</summary>
    public string Scope { get; set; } = string.Empty;

    public string? PrefilledName { get; set; }
}

public class GuestJoinTokenRequestDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

public class GuestJoinTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string MeetingId { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;

    /// <summary>A guest gets these only when the meeting's policy is Everyone - a guest's id is
    /// random per visit, so it can never be individually selected.</summary>
    public bool CanShareScreen { get; set; }
    public bool CanRecord { get; set; }
}
