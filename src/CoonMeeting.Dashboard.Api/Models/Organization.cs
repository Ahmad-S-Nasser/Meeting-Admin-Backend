namespace CoonMeeting.Dashboard.Api.Models;

public enum OrganizationRole
{
    Owner,
    Member,
}

/// <summary>
/// Embedded, mirroring Coon.Meeting's own Meeting.Attendees style - only ever looked up by its
/// parent Organization's own Id, never queried across organizations.
/// </summary>
public class OrganizationMember
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One organization owns exactly one Coon.Meeting tenant (and its API key) in v1 - no multi-org-per-org, no key rotation.</summary>
public class Organization
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    public string CoonMeetingTenantId { get; set; } = string.Empty;

    /// <summary>Stored server-side only - never sent to the Dashboard's own frontend.</summary>
    public string CoonMeetingApiKey { get; set; } = string.Empty;

    public List<OrganizationMember> Members { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
