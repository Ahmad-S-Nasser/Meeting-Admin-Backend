namespace CoonMeeting.Dashboard.Api.Models;

public static class MeetingJoinLinkScope
{
    public const string Any = "Any";
    public const string Attendee = "Attendee";
}

/// <summary>
/// Top-level collection, not embedded in Organization/Meeting - looked up by TokenHash across
/// every org, same reasoning as PendingInvite (an embedded-list search across all orgs isn't a
/// safe LiteDB LINQ translation).
///
/// Deliberate deviation from PendingInvite's hash-only pattern: PendingInvite never persists
/// its raw token because invite links are one-shot. A reusable "Any" shareable link needs to
/// be redisplayed on a later "get my link" click, which is impossible if only the hash is
/// stored - so PlaintextToken is kept for Scope=Any rows (UI convenience only; every
/// *consumption* of the link still validates via the hash, exactly like PendingInvite).
/// </summary>
public class MeetingJoinLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrganizationId { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Scope=Any only - null for Scope=Attendee.</summary>
    public string? PlaintextToken { get; set; }

    /// <summary>MeetingJoinLinkScope.Any or .Attendee.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Scope=Attendee only - the invited guest's email, normalized.</summary>
    public string? ScopedEmail { get; set; }

    /// <summary>Scope=Attendee only - prefills the name field on the guest join page.</summary>
    public string? ScopedName { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null for Scope=Any (reusable indefinitely); +24h for Scope=Attendee.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool Revoked { get; set; }
}
