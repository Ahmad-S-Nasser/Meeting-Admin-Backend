using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

[ApiController]
[Route("api/v1/org")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class OrgController : ControllerBase
{
    private readonly IOrganizationRepository _orgs;
    private readonly IUserRepository _users;
    private readonly IPendingInviteRepository _invites;
    private readonly IInviteTokenService _tokens;
    private readonly IInviteEmailSender _email;
    private readonly FrontendSettings _frontend;

    public OrgController(
        IOrganizationRepository orgs,
        IUserRepository users,
        IPendingInviteRepository invites,
        IInviteTokenService tokens,
        IInviteEmailSender email,
        FrontendSettings frontend)
    {
        _orgs = orgs;
        _users = users;
        _invites = invites;
        _tokens = tokens;
        _email = email;
        _frontend = frontend;
    }

    // GET /api/v1/org/members
    [HttpGet("members")]
    public async Task<ActionResult<List<OrgMemberDto>>> GetMembers()
    {
        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return NotFound();

        return Ok(org.Members.Select(m => new OrgMemberDto
        {
            UserId = m.UserId,
            Email = m.Email,
            Name = m.Name,
            Role = m.Role,
            JoinedAt = m.JoinedAt,
        }).ToList());
    }

    // POST /api/v1/org/invites
    [HttpPost("invites")]
    public async Task<IActionResult> CreateInvite([FromBody] CreateInviteDto dto)
    {
        if (SessionContext.Role(User) != OrganizationRole.Owner)
            return Forbid();

        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return NotFound();

        var email = dto.Email.Trim().ToLowerInvariant();

        // Every persisted user is created together with an org (v1: exactly one) - a User
        // already existing for this email means it already belongs to one.
        if (await _users.GetByEmailAsync(email) != null)
            return Conflict(new { message = "This email already belongs to an account." });

        var rawToken = _tokens.GenerateToken();
        var invite = new PendingInvite
        {
            OrganizationId = org.Id,
            Email = email,
            TokenHash = _tokens.Hash(rawToken),
            InvitedByUserId = SessionContext.UserId(User),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        await _invites.InsertAsync(invite);

        var inviteUrl = $"{_frontend.BaseUrl.TrimEnd('/')}/invites/{rawToken}";
        await _email.SendInviteAsync(email, org.Name, inviteUrl);

        return Accepted();
    }
}
