namespace CoonMeeting.Dashboard.Api.Models;

/// <summary>
/// Top-level collection, not embedded in Organization - looked up by TokenHash across every
/// org, which an embedded-list .Any() search can't safely do under LiteDB's LINQ translator
/// (same class of issue as ClaimMeetingNeedingReminder in Coon.Meeting itself).
/// </summary>
public class PendingInvite
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>Always lowercased, matching User.Email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the raw invite token - the raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string InvitedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
