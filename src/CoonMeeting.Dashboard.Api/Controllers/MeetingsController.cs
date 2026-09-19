using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

/// <summary>Thin proxy for Coon.Meeting's own meetings CRUD - the org's Coon.Meeting API key is
/// resolved from the session, never sent to or seen by the Dashboard's own frontend.</summary>
[ApiController]
[Route("api/v1/meetings")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class MeetingsController : ControllerBase
{
    private readonly IOrganizationRepository _orgs;
    private readonly ICoonMeetingClient _coonMeeting;
    private readonly IMeetingJoinLinkRepository _links;
    private readonly IInviteTokenService _tokens;
    private readonly IGuestJoinEmailSender _email;
    private readonly FrontendSettings _frontend;

    public MeetingsController(
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

    // GET /api/v1/meetings
    [HttpGet]
    public async Task<ActionResult<List<MeetingDto>>> List()
    {
        var org = await RequireOrgAsync();
        if (org == null) return NotFound();

        var meetings = await _coonMeeting.ListMeetingsAsync(org.CoonMeetingApiKey);
        return Ok(meetings.Select(m => ToDto(m, org, SessionContext.UserId(User))).ToList());
    }

    // GET /api/v1/meetings/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<MeetingDto>> GetById(string id)
    {
        var org = await RequireOrgAsync();
        if (org == null) return NotFound();

        var meeting = await _coonMeeting.GetMeetingAsync(org.CoonMeetingApiKey, id);
        if (meeting == null) return NotFound();
        return Ok(ToDto(meeting, org, SessionContext.UserId(User)));
    }

    // POST /api/v1/meetings
    [HttpPost]
    public async Task<ActionResult<MeetingDto>> Create([FromBody] CreateMeetingRequestDto dto)
    {
        var org = await RequireOrgAsync();
        if (org == null) return NotFound();

        var request = new CreateCoonMeetingMeetingRequest
        {
            Title = dto.Title,
            Description = dto.Description,
            ScheduledAt = dto.ScheduledAt,
            TimeZone = dto.TimeZone,
            DurationMinutes = dto.DurationMinutes,
            Location = dto.Location,
            MeetingLink = dto.MeetingLink,
            Visibility = dto.Visibility,
            Organizer = new CoonMeetingAttendeeRequest
            {
                ExternalId = SessionContext.UserId(User),
                Name = SessionContext.Name(User),
                Email = SessionContext.Email(User),
            },
            Attendees = dto.Attendees.Select(a => TranslateAttendee(org, a)).ToList(),
        };

        var meeting = await _coonMeeting.CreateMeetingAsync(org.CoonMeetingApiKey, request);
        await InviteAttendeesAsync(org, meeting, dto.Attendees);

        return CreatedAtAction(nameof(GetById), new { id = meeting.Id }, ToDto(meeting, org, SessionContext.UserId(User)));
    }

    /// <summary>Emails every attendee a working way into the meeting right away, instead of
    /// leaving that as a manual "Invite someone" step the organizer has to remember to do
    /// afterward, one address at a time. An org member gets a normal Dashboard link (their own
    /// login gates it); anyone else gets a guest join link - the same links "Invite someone"
    /// on the meeting detail page hands out, just triggered automatically at creation time.</summary>
    private async Task InviteAttendeesAsync(Organization org, CoonMeetingMeeting meeting, List<CreateMeetingAttendeeRequestDto> attendees)
    {
        var organizerEmail = SessionContext.Email(User);

        foreach (var attendee in attendees)
        {
            var email = attendee.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.Equals(email, organizerEmail, StringComparison.OrdinalIgnoreCase))
                continue;

            var member = org.Members.FirstOrDefault(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase));
            if (member != null)
            {
                var meetingUrl = $"{_frontend.BaseUrl.TrimEnd('/')}/meetings/{meeting.Id}";
                await _email.SendMemberInviteAsync(member.Email, org.Name, meeting.Title, meetingUrl);
            }
            else
            {
                var joinUrl = await GetOrCreateGuestJoinUrlAsync(org, meeting, email, attendee.Name);
                await _email.SendGuestInviteAsync(email, org.Name, meeting.Title, joinUrl);
            }
        }
    }

    /// <summary>The attendee is already a verified Coon.Meeting attendee at this point (added by
    /// the CreateMeetingAsync call above), so unlike MeetingJoinLinksController.CreateAttendeeLink
    /// this only needs to mint the join-link token - no second AddAttendeeAsync call.</summary>
    private async Task<string> GetOrCreateGuestJoinUrlAsync(Organization org, CoonMeetingMeeting meeting, string email, string? name)
    {
        if (string.Equals(meeting.Visibility, "Any", StringComparison.Ordinal))
        {
            var existing = await _links.GetActiveAnyLinkAsync(meeting.Id);
            if (existing?.PlaintextToken != null) return BuildJoinUrl(existing.PlaintextToken);

            var anyToken = _tokens.GenerateToken();
            await _links.InsertAsync(new MeetingJoinLink
            {
                OrganizationId = org.Id,
                MeetingId = meeting.Id,
                TokenHash = _tokens.Hash(anyToken),
                PlaintextToken = anyToken,
                Scope = MeetingJoinLinkScope.Any,
                CreatedByUserId = SessionContext.UserId(User),
                ExpiresAt = null,
            });
            return BuildJoinUrl(anyToken);
        }

        var rawToken = _tokens.GenerateToken();
        await _links.InsertAsync(new MeetingJoinLink
        {
            OrganizationId = org.Id,
            MeetingId = meeting.Id,
            TokenHash = _tokens.Hash(rawToken),
            Scope = MeetingJoinLinkScope.Attendee,
            ScopedEmail = email,
            ScopedName = string.IsNullOrWhiteSpace(name) ? null : name,
            CreatedByUserId = SessionContext.UserId(User),
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        });
        return BuildJoinUrl(rawToken);
    }

    private string BuildJoinUrl(string rawToken) => $"{_frontend.BaseUrl.TrimEnd('/')}/join/{rawToken}";

    // PUT /api/v1/meetings/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateMeetingRequestDto dto)
    {
        var org = await RequireOrgAsync();
        if (org == null) return NotFound();

        var found = await _coonMeeting.UpdateMeetingAsync(org.CoonMeetingApiKey, id, new UpdateCoonMeetingMeetingRequest
        {
            Title = dto.Title,
            Description = dto.Description,
            ScheduledAt = dto.ScheduledAt,
            TimeZone = dto.TimeZone,
            DurationMinutes = dto.DurationMinutes,
            Location = dto.Location,
            MeetingLink = dto.MeetingLink,
            Visibility = dto.Visibility,
        });

        return found ? NoContent() : NotFound();
    }

    // DELETE /api/v1/meetings/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(string id)
    {
        var org = await RequireOrgAsync();
        if (org == null) return NotFound();

        var found = await _coonMeeting.CancelMeetingAsync(org.CoonMeetingApiKey, id);
        return found ? NoContent() : NotFound();
    }

    private Task<Organization?> RequireOrgAsync() =>
        _orgs.GetByIdAsync(SessionContext.OrganizationId(User));

    /// <summary>Translation rule: an attendee whose email matches an org member always uses that
    /// member's Dashboard User.Id (and own name) as Coon.Meeting's ExternalId - only a plain,
    /// non-member attendee falls back to using their email as the external id.</summary>
    private static CoonMeetingAttendeeRequest TranslateAttendee(Organization org, CreateMeetingAttendeeRequestDto attendee)
    {
        var email = attendee.Email.Trim().ToLowerInvariant();
        var member = org.Members.FirstOrDefault(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase));

        return member != null
            ? new CoonMeetingAttendeeRequest { ExternalId = ResolveAttendeeExternalId(org, email), Name = member.Name, Email = member.Email }
            : new CoonMeetingAttendeeRequest { ExternalId = ResolveAttendeeExternalId(org, email), Name = string.IsNullOrWhiteSpace(attendee.Name) ? email : attendee.Name, Email = email };
    }

    /// <summary>The id-resolution half of attendee translation, on its own so a second caller
    /// (the guest-join flow) always derives the exact same external id for a given email as
    /// whatever was written into Meeting.Attendees when they were added - Coon.Meeting's own
    /// Private-visibility check is a byte-for-byte string match, so any drift here would make a
    /// legitimately-invited guest's own token mint fail.</summary>
    internal static string ResolveAttendeeExternalId(Organization org, string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var member = org.Members.FirstOrDefault(m => string.Equals(m.Email, normalized, StringComparison.OrdinalIgnoreCase));
        return member?.UserId ?? normalized;
    }

    private static MeetingDto ToDto(CoonMeetingMeeting meeting, Organization org, string callerUserId) => new()
    {
        Id = meeting.Id,
        Title = meeting.Title,
        Description = meeting.Description,
        ScheduledAt = meeting.ScheduledAt,
        TimeZone = meeting.TimeZone,
        DurationMinutes = meeting.DurationMinutes,
        Location = meeting.Location,
        MeetingLink = meeting.MeetingLink,
        CreatedByName = meeting.CreatedByName,
        Status = meeting.Status,
        Visibility = meeting.Visibility,
        IsOrganizer = string.Equals(meeting.CreatedByExternalId, callerUserId, StringComparison.Ordinal),
        CreatedAt = meeting.CreatedAt,
        UpdatedAt = meeting.UpdatedAt,
        Attendees = meeting.Attendees.Select(a => new MeetingAttendeeDto { Name = a.Name, Email = a.Email }).ToList(),
        BlockedParticipants = meeting.BlockedParticipantIds.Select(id => ToBlockedParticipantDto(org, id)).ToList(),
    };

    /// <summary>An org member's raw Coon.Meeting external id is their Dashboard UserId, meaningless
    /// on its own to the frontend - resolve it back to a name/email where possible. A non-member's
    /// external id is already their email (see TranslateAttendee's fallback), so it displays fine
    /// as-is either way.</summary>
    private static BlockedParticipantDto ToBlockedParticipantDto(Organization org, string externalId)
    {
        var member = org.Members.FirstOrDefault(m => m.UserId == externalId);
        return member != null
            ? new BlockedParticipantDto { ParticipantExternalId = externalId, Name = member.Name, Email = member.Email }
            : new BlockedParticipantDto { ParticipantExternalId = externalId, Name = externalId, Email = externalId };
    }
}
