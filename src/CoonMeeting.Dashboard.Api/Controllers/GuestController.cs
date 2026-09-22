using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

/// <summary>Fully anonymous - no Dashboard session, no API key. Resolves a join-link token to a
/// live Coon.Meeting participant token. Every call re-validates from scratch (link not
/// revoked/expired, and for Scope=Any re-checks the meeting is STILL "Any" right now) - nothing
/// here trusts a cached validity from an earlier preview call, or a meeting briefly set to Any
/// and later flipped back to Private would leave a durable bypass.</summary>
[ApiController]
[Route("api/v1/guest")]
[AllowAnonymous]
public class GuestController : ControllerBase
{
    private readonly IMeetingJoinLinkRepository _links;
    private readonly IOrganizationRepository _orgs;
    private readonly ICoonMeetingClient _coonMeeting;
    private readonly IInviteTokenService _tokens;
    private readonly IMeetingSettingsRepository _settings;

    public GuestController(
        IMeetingJoinLinkRepository links,
        IOrganizationRepository orgs,
        ICoonMeetingClient coonMeeting,
        IInviteTokenService tokens,
        IMeetingSettingsRepository settings)
    {
        _links = links;
        _orgs = orgs;
        _coonMeeting = coonMeeting;
        _tokens = tokens;
        _settings = settings;
    }

    // GET /api/v1/guest/join-links/{rawToken}
    [HttpGet("join-links/{rawToken}")]
    public async Task<ActionResult<GuestJoinPreviewDto>> Preview(string rawToken)
    {
        var (link, org, meeting, error) = await ResolveAsync(rawToken);
        if (error != null) return error;

        return Ok(new GuestJoinPreviewDto
        {
            MeetingId = link!.MeetingId,
            MeetingTitle = meeting!.Title,
            Scope = link.Scope,
            PrefilledName = link.ScopedName,
        });
    }

    // POST /api/v1/guest/join-links/{rawToken}/token
    [HttpPost("join-links/{rawToken}/token")]
    public async Task<ActionResult<GuestJoinTokenResponseDto>> MintToken(string rawToken, [FromBody] GuestJoinTokenRequestDto dto)
    {
        var (link, org, meeting, error) = await ResolveAsync(rawToken);
        if (error != null) return error;

        var participantExternalId = link!.Scope == MeetingJoinLinkScope.Any
            ? $"guest_{Guid.NewGuid():N}"
            : MeetingsController.ResolveAttendeeExternalId(org!, link.ScopedEmail!);

        var name = dto.Name.Trim();

        CoonMeetingParticipantToken? token;
        try
        {
            token = await _coonMeeting.MintParticipantTokenAsync(org!.CoonMeetingApiKey, link.MeetingId, participantExternalId, name);
        }
        catch (CoonMeetingApiException ex) when (ex.StatusCode is 400 or 403)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }

        if (token == null) return NotFound();

        var settings = await _settings.GetAsync(link.MeetingId);
        // Only an Any-link visitor has a random, unselectable id. A per-guest (Attendee) link
        // resolves to a stable id - their email or org-member id - so they can be picked like anyone.
        var anonymous = link.Scope == MeetingJoinLinkScope.Any;

        return Ok(new GuestJoinTokenResponseDto
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            MeetingId = token.MeetingId,
            ParticipantName = name,
            CanShareScreen = CallPermissionEvaluator.Evaluate(settings?.ScreenShare, meeting!.CreatedByExternalId, participantExternalId, isGuest: anonymous),
            CanRecord = CallPermissionEvaluator.Evaluate(settings?.Recording, meeting.CreatedByExternalId, participantExternalId, isGuest: anonymous),
        });
    }

    private async Task<(MeetingJoinLink? Link, Organization? Org, CoonMeetingMeeting? Meeting, ActionResult? Error)> ResolveAsync(string rawToken)
    {
        var link = await _links.GetByTokenHashAsync(_tokens.Hash(rawToken));
        if (link == null || link.Revoked) return (null, null, null, NotFound());
        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow) return (null, null, null, StatusCode(410, new { message = "This invite has expired." }));

        var org = await _orgs.GetByIdAsync(link.OrganizationId);
        if (org == null) return (null, null, null, NotFound());

        var meeting = await _coonMeeting.GetMeetingAsync(org.CoonMeetingApiKey, link.MeetingId);
        if (meeting == null) return (null, null, null, NotFound());

        // Re-checked live, every call - see the class-level remark on why this can't be cached.
        if (link.Scope == MeetingJoinLinkScope.Any && !string.Equals(meeting.Visibility, "Any", StringComparison.Ordinal))
            return (null, null, null, StatusCode(403, new { message = "This meeting is no longer open to anyone with the link." }));

        return (link, org, meeting, null);
    }
}
