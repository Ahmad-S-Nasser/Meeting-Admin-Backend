using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

/// <summary>Organizer-only: creates the two kinds of guest join link. Any-visibility gets one
/// reusable "anyone with this link" link; Private-visibility gets a link scoped to one invited
/// email, who first gets added as a verified Coon.Meeting attendee.</summary>
[ApiController]
[Route("api/v1/meetings/{id}/join-links")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class MeetingJoinLinksController : ControllerBase
{
    private readonly IOrganizationRepository _orgs;
    private readonly ICoonMeetingClient _coonMeeting;
    private readonly IMeetingJoinLinkRepository _links;
    private readonly IInviteTokenService _tokens;
    private readonly IGuestJoinEmailSender _email;
    private readonly FrontendSettings _frontend;

    public MeetingJoinLinksController(
        IOrganizationRepository orgs,
        ICoonMeetingClient coonMeeting,
        IMeetingJoinLinkRepository links,
        IInviteTokenService tokens,
        IGuestJoinEmailSender email,
        FrontendSettings frontend)
    {
        _orgs = orgs;
        _coonMeeting = coonMeeting;
        _links = links;
        _tokens = tokens;
        _email = email;
        _frontend = frontend;
    }

    // POST /api/v1/meetings/{id}/join-links/any
    [HttpPost("any")]
    public async Task<ActionResult<MeetingJoinLinkResponseDto>> CreateAnyLink(string id)
    {
        var (org, meeting, error) = await LoadAndAuthorizeAsync(id);
        if (error != null) return error;

        if (!string.Equals(meeting!.Visibility, "Any", StringComparison.Ordinal))
            return BadRequest(new { message = "This meeting isn't set to \"Any\" visibility." });

        var existing = await _links.GetActiveAnyLinkAsync(id);
        if (existing?.PlaintextToken != null)
            return Ok(new MeetingJoinLinkResponseDto { Url = BuildUrl(existing.PlaintextToken) });

        var rawToken = _tokens.GenerateToken();
        var link = new MeetingJoinLink
        {
            OrganizationId = org!.Id,
            MeetingId = id,
            TokenHash = _tokens.Hash(rawToken),
            PlaintextToken = rawToken,
            Scope = MeetingJoinLinkScope.Any,
            CreatedByUserId = SessionContext.UserId(User),
            ExpiresAt = null,
        };
        await _links.InsertAsync(link);

        return Ok(new MeetingJoinLinkResponseDto { Url = BuildUrl(rawToken) });
    }

    // DELETE /api/v1/meetings/{id}/join-links/any - revokes the current link; a later
    // CreateAnyLink call mints a genuinely new URL.
    [HttpDelete("any")]
    public async Task<IActionResult> RevokeAnyLink(string id)
    {
        var (_, _, error) = await LoadAndAuthorizeAsync(id);
        if (error != null) return error;

        var existing = await _links.GetActiveAnyLinkAsync(id);
        if (existing != null)
        {
            existing.Revoked = true;
            await _links.UpdateAsync(existing);
        }

        return NoContent();
    }

    // POST /api/v1/meetings/{id}/join-links/attendee
    [HttpPost("attendee")]
    public async Task<ActionResult<MeetingJoinLinkResponseDto>> CreateAttendeeLink(string id, [FromBody] CreateGuestInviteDto dto)
    {
        var (org, meeting, error) = await LoadAndAuthorizeAsync(id);
        if (error != null) return error;

        if (!string.Equals(meeting!.Visibility, "Private", StringComparison.Ordinal))
            return BadRequest(new { message = "This meeting isn't Private - share the meeting's Any link instead." });

        var email = dto.Email.Trim().ToLowerInvariant();
        var externalId = MeetingsController.ResolveAttendeeExternalId(org!, email);

        await _coonMeeting.AddAttendeeAsync(org!.CoonMeetingApiKey, id, new CoonMeetingAttendeeRequest
        {
            ExternalId = externalId,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? email : dto.Name,
            Email = email,
        });

        var rawToken = _tokens.GenerateToken();
        var link = new MeetingJoinLink
        {
            OrganizationId = org.Id,
            MeetingId = id,
            TokenHash = _tokens.Hash(rawToken),
            Scope = MeetingJoinLinkScope.Attendee,
            ScopedEmail = email,
            ScopedName = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name,
            CreatedByUserId = SessionContext.UserId(User),
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };
        await _links.InsertAsync(link);

        var url = BuildUrl(rawToken);
        await _email.SendGuestInviteAsync(email, meeting.Title, url);

        return Ok(new MeetingJoinLinkResponseDto { Url = url });
    }

    private string BuildUrl(string rawToken) => $"{_frontend.BaseUrl.TrimEnd('/')}/join/{rawToken}";

    private async Task<(Organization? Org, CoonMeetingMeeting? Meeting, ActionResult? Error)> LoadAndAuthorizeAsync(string meetingId)
    {
        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return (null, null, NotFound());

        var meeting = await _coonMeeting.GetMeetingAsync(org.CoonMeetingApiKey, meetingId);
        if (meeting == null) return (null, null, NotFound());

        if (!string.Equals(meeting.CreatedByExternalId, SessionContext.UserId(User), StringComparison.Ordinal))
            return (null, null, Forbid());

        return (org, meeting, null);
    }
}
