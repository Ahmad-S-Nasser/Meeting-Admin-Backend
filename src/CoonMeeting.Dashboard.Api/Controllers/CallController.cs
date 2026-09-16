using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

/// <summary>Mints a Coon.Meeting participant token for the current session's user. Coon.Meeting
/// itself is the sole authority on whether this id may join this meeting (organizer, a listed
/// attendee, or Any-visibility) - this controller doesn't duplicate that check, it just forwards
/// Coon.Meeting's 403 as its own. For a Private meeting this means "any org member can join any
/// org meeting" no longer holds unconditionally - only the organizer/listed attendees can.</summary>
[ApiController]
[Route("api/v1/meetings/{id}/call-token")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class CallController : ControllerBase
{
    private readonly IOrganizationRepository _orgs;
    private readonly ICoonMeetingClient _coonMeeting;

    public CallController(IOrganizationRepository orgs, ICoonMeetingClient coonMeeting)
    {
        _orgs = orgs;
        _coonMeeting = coonMeeting;
    }

    [HttpPost]
    public async Task<ActionResult<CallTokenResponseDto>> MintToken(string id)
    {
        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return NotFound();

        // GetMeetingAsync first - naturally 404s for a meeting outside this org's tenant, since
        // the org's own Coon.Meeting API key only ever resolves that tenant's meetings.
        var meeting = await _coonMeeting.GetMeetingAsync(org.CoonMeetingApiKey, id);
        if (meeting == null) return NotFound();

        CoonMeetingParticipantToken? token;
        try
        {
            token = await _coonMeeting.MintParticipantTokenAsync(
                org.CoonMeetingApiKey, id, SessionContext.UserId(User), SessionContext.Name(User));
        }
        catch (CoonMeetingApiException ex) when (ex.StatusCode == 400)
        {
            return BadRequest(new { message = "Cannot start a call for a cancelled meeting." });
        }
        catch (CoonMeetingApiException ex) when (ex.StatusCode == 403)
        {
            return Forbid();
        }

        if (token == null) return NotFound();

        return Ok(new CallTokenResponseDto
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            MeetingId = token.MeetingId,
        });
    }
}
