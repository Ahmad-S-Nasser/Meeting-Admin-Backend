using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

/// <summary>Per-meeting call settings (who may share a screen / record) plus the two read-only
/// lookups every participant's call screen needs. The settings are Dashboard-owned and never sent
/// to Coon.Meeting - it has no notion of them, and every integrator decides that for itself.</summary>
[ApiController]
[Route("api/v1/meetings/{id}")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class MeetingSettingsController : ControllerBase
{
    private readonly IOrganizationRepository _orgs;
    private readonly ICoonMeetingClient _coonMeeting;
    private readonly IMeetingSettingsRepository _settings;
    private readonly IMeetingJoinLinkRepository _links;
    private readonly FrontendSettings _frontend;

    public MeetingSettingsController(
        IOrganizationRepository orgs,
        ICoonMeetingClient coonMeeting,
        IMeetingSettingsRepository settings,
        IMeetingJoinLinkRepository links,
        FrontendSettings frontend)
    {
        _orgs = orgs;
        _coonMeeting = coonMeeting;
        _settings = settings;
        _links = links;
        _frontend = frontend;
    }

    // GET /api/v1/meetings/{id}/settings - organizer only
    [HttpGet("settings")]
    public async Task<ActionResult<MeetingSettingsDto>> GetSettings(string id)
    {
        var (org, meeting, error) = await LoadAsync(id);
        if (error != null) return error;
        if (!IsOrganizer(meeting!)) return Forbid();

        var stored = await _settings.GetAsync(id);
        return Ok(new MeetingSettingsDto
        {
            ScreenShare = ToDto(stored?.ScreenShare),
            Recording = ToDto(stored?.Recording),
            People = SelectablePeople(meeting!),
        });
    }

    // PUT /api/v1/meetings/{id}/settings - organizer only
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(string id, [FromBody] UpdateMeetingSettingsDto dto)
    {
        var (org, meeting, error) = await LoadAsync(id);
        if (error != null) return error;
        if (!IsOrganizer(meeting!)) return Forbid();

        if (!PermissionMode.IsValid(dto.ScreenShare.Mode) || !PermissionMode.IsValid(dto.Recording.Mode))
            return BadRequest(new { message = "Mode must be OrganizerOnly, Selected or Everyone." });

        // Only people actually on this meeting can be selected - anything else is dropped rather
        // than trusted, so a hand-crafted request can't grant a permission to an arbitrary id.
        var validIds = SelectablePeople(meeting!).Select(p => p.ExternalId).ToHashSet(StringComparer.Ordinal);

        await _settings.UpsertAsync(new MeetingSettings
        {
            Id = id,
            OrganizationId = org!.Id,
            ScreenShare = ToPolicy(dto.ScreenShare, validIds),
            Recording = ToPolicy(dto.Recording, validIds),
            UpdatedAt = DateTime.UtcNow,
        });

        return NoContent();
    }

    // GET /api/v1/meetings/{id}/my-permissions - polled by the call screen so a change the
    // organizer makes mid-call reaches everyone already in it.
    [HttpGet("my-permissions")]
    public async Task<ActionResult<MyPermissionsDto>> GetMyPermissions(string id)
    {
        var (org, meeting, error) = await LoadAsync(id);
        if (error != null) return error;
        if (!CanAccess(org!, meeting!)) return Forbid();

        var stored = await _settings.GetAsync(id);
        var callerId = SessionContext.UserId(User);
        return Ok(new MyPermissionsDto
        {
            CanShareScreen = CallPermissionEvaluator.Evaluate(stored?.ScreenShare, meeting!.CreatedByExternalId, callerId, isGuest: false),
            CanRecord = CallPermissionEvaluator.Evaluate(stored?.Recording, meeting.CreatedByExternalId, callerId, isGuest: false),
        });
    }

    // GET /api/v1/meetings/{id}/invite-link - never creates anything: only the organizer mints a
    // guest link (MeetingJoinLinksController), everyone else just copies what already exists.
    [HttpGet("invite-link")]
    public async Task<ActionResult<InviteLinkDto>> GetInviteLink(string id)
    {
        var (org, meeting, error) = await LoadAsync(id);
        if (error != null) return error;
        if (!CanAccess(org!, meeting!)) return Forbid();

        var baseUrl = _frontend.BaseUrl.TrimEnd('/');

        if (string.Equals(meeting!.Visibility, "Any", StringComparison.Ordinal))
        {
            var existing = await _links.GetActiveAnyLinkAsync(id);
            if (existing?.PlaintextToken != null)
                return Ok(new InviteLinkDto { Url = $"{baseUrl}/join/{existing.PlaintextToken}", Kind = "join-link" });
        }

        return Ok(new InviteLinkDto { Url = $"{baseUrl}/meetings/{id}", Kind = "meeting-page" });
    }

    private bool IsOrganizer(CoonMeetingMeeting meeting) =>
        string.Equals(meeting.CreatedByExternalId, SessionContext.UserId(User), StringComparison.Ordinal);

    /// <summary>Same "who may join" rule Coon.Meeting itself applies, so this never grants a
    /// lookup to someone the call itself would turn away: the organizer, a listed attendee, or
    /// (Any meetings) any org member.</summary>
    private bool CanAccess(Organization org, CoonMeetingMeeting meeting)
    {
        var callerId = SessionContext.UserId(User);
        if (IsOrganizer(meeting)) return true;
        if (meeting.Attendees.Any(a => string.Equals(a.ExternalParticipantId, callerId, StringComparison.Ordinal))) return true;
        return string.Equals(meeting.Visibility, "Any", StringComparison.Ordinal);
    }

    private static List<SelectablePersonDto> SelectablePeople(CoonMeetingMeeting meeting) =>
        meeting.Attendees
            .Where(a => !string.Equals(a.ExternalParticipantId, meeting.CreatedByExternalId, StringComparison.Ordinal))
            .Select(a => new SelectablePersonDto { ExternalId = a.ExternalParticipantId, Name = a.Name, Email = a.Email })
            .ToList();

    private static PermissionPolicyDto ToDto(PermissionPolicy? policy) => new()
    {
        Mode = policy?.Mode ?? PermissionMode.OrganizerOnly,
        AllowedParticipantIds = policy?.AllowedParticipantIds ?? new List<string>(),
    };

    private static PermissionPolicy ToPolicy(PermissionPolicyDto dto, HashSet<string> validIds) => new()
    {
        Mode = dto.Mode,
        AllowedParticipantIds = dto.Mode == PermissionMode.Selected
            ? dto.AllowedParticipantIds.Where(validIds.Contains).Distinct(StringComparer.Ordinal).ToList()
            : new List<string>(),
    };

    private async Task<(Organization? Org, CoonMeetingMeeting? Meeting, ActionResult? Error)> LoadAsync(string meetingId)
    {
        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return (null, null, NotFound());

        var meeting = await _coonMeeting.GetMeetingAsync(org.CoonMeetingApiKey, meetingId);
        if (meeting == null) return (null, null, NotFound());

        return (org, meeting, null);
    }
}
