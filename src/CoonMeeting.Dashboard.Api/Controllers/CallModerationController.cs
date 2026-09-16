using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

/// <summary>Forwards a kick/block/unblock to Coon.Meeting, passing the session's own UserId as
/// RequestedByExternalId - Coon.Meeting is the sole authority on "is this really the organizer",
/// this controller only translates its response.</summary>
[ApiController]
[Route("api/v1/meetings/{id}/participants/{participantExternalId}")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class CallModerationController : ControllerBase
{
    private readonly IOrganizationRepository _orgs;
    private readonly ICoonMeetingClient _coonMeeting;

    public CallModerationController(IOrganizationRepository orgs, ICoonMeetingClient coonMeeting)
    {
        _orgs = orgs;
        _coonMeeting = coonMeeting;
    }

    [HttpPost("kick")]
    public Task<IActionResult> Kick(string id, string participantExternalId) =>
        ModerateAsync((org, requestedBy) => _coonMeeting.KickParticipantAsync(org.CoonMeetingApiKey, id, participantExternalId, requestedBy));

    [HttpPost("block")]
    public Task<IActionResult> Block(string id, string participantExternalId) =>
        ModerateAsync((org, requestedBy) => _coonMeeting.BlockParticipantAsync(org.CoonMeetingApiKey, id, participantExternalId, requestedBy));

    [HttpPost("unblock")]
    public Task<IActionResult> Unblock(string id, string participantExternalId) =>
        ModerateAsync((org, requestedBy) => _coonMeeting.UnblockParticipantAsync(org.CoonMeetingApiKey, id, participantExternalId, requestedBy));

    private async Task<IActionResult> ModerateAsync(Func<Organization, string, Task> action)
    {
        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return NotFound();

        try
        {
            await action(org, SessionContext.UserId(User));
            return NoContent();
        }
        catch (CoonMeetingApiException ex) when (ex.StatusCode == 403)
        {
            return Forbid();
        }
        catch (CoonMeetingApiException ex) when (ex.StatusCode == 404)
        {
            return NotFound();
        }
    }
}
