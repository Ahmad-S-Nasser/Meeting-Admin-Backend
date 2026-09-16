using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

[ApiController]
[Route("api/v1/invites")]
public class InvitesController : ControllerBase
{
    private readonly IPendingInviteRepository _invites;
    private readonly IOrganizationRepository _orgs;
    private readonly IUserRepository _users;
    private readonly LiteDbContext _db;
    private readonly IInviteTokenService _tokens;
    private readonly ISessionTokenService _sessions;
    private readonly IPasswordHasher<User> _hasher;

    public InvitesController(
        IPendingInviteRepository invites,
        IOrganizationRepository orgs,
        IUserRepository users,
        LiteDbContext db,
        IInviteTokenService tokens,
        ISessionTokenService sessions,
        IPasswordHasher<User> hasher)
    {
        _invites = invites;
        _orgs = orgs;
        _users = users;
        _db = db;
        _tokens = tokens;
        _sessions = sessions;
        _hasher = hasher;
    }

    // GET /api/v1/invites/{token}
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<InvitePreviewDto>> Preview(string token)
    {
        var (invite, org) = await ResolveAsync(token);
        if (invite == null || org == null) return NotFound();
        if (invite.ExpiresAt < DateTime.UtcNow) return StatusCode(410, new { message = "This invite has expired." });

        return Ok(new InvitePreviewDto { OrganizationName = org.Name, Email = invite.Email });
    }

    // POST /api/v1/invites/{token}/accept-new
    [HttpPost("{token}/accept-new")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> AcceptNew(string token, [FromBody] AcceptNewInviteDto dto)
    {
        var (invite, org) = await ResolveAsync(token);
        if (invite == null || org == null) return NotFound();
        if (invite.ExpiresAt < DateTime.UtcNow) return StatusCode(410, new { message = "This invite has expired." });

        if (await _users.GetByEmailAsync(invite.Email) != null)
            return Conflict(new { message = "An account with this email already exists. Log in and accept the invite instead." });

        var user = new User { Email = invite.Email, Name = dto.Name, OrganizationId = org.Id };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);

        org.Members.Add(new OrganizationMember
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = OrganizationRole.Member,
        });

        _db.AcceptInviteForNewUser(user, org, invite);

        var sessionToken = _sessions.Mint(user, org, OrganizationRole.Member);
        return Ok(ToAuthResponse(user, org, OrganizationRole.Member, sessionToken));
    }

    // POST /api/v1/invites/{token}/accept
    [HttpPost("{token}/accept")]
    [Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
    public async Task<ActionResult<AuthResponseDto>> Accept(string token)
    {
        var (invite, org) = await ResolveAsync(token);
        if (invite == null || org == null) return NotFound();
        if (invite.ExpiresAt < DateTime.UtcNow) return StatusCode(410, new { message = "This invite has expired." });

        var user = await _users.GetByIdAsync(SessionContext.UserId(User));
        if (user == null) return NotFound();

        if (!string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        // v1: exactly one org per user (multi-org-per-user is an explicit scope cut, enforced
        // here as a 409 rather than left unimplemented) - every persisted user already has one.
        if (user.OrganizationId != null)
            return Conflict(new { message = "This account already belongs to an organization." });

        user.OrganizationId = org.Id;
        org.Members.Add(new OrganizationMember
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = OrganizationRole.Member,
        });

        _db.AcceptInviteForExistingUser(user, org, invite);

        var sessionToken = _sessions.Mint(user, org, OrganizationRole.Member);
        return Ok(ToAuthResponse(user, org, OrganizationRole.Member, sessionToken));
    }

    private async Task<(PendingInvite? Invite, Organization? Org)> ResolveAsync(string token)
    {
        var invite = await _invites.GetByTokenHashAsync(_tokens.Hash(token));
        if (invite == null) return (null, null);

        var org = await _orgs.GetByIdAsync(invite.OrganizationId);
        return (invite, org);
    }

    private static AuthResponseDto ToAuthResponse(User user, Organization org, OrganizationRole role, SessionTokenResult token) => new()
    {
        Token = token.Token,
        ExpiresAt = token.ExpiresAt,
        UserId = user.Id,
        Email = user.Email,
        Name = user.Name,
        OrganizationId = org.Id,
        OrganizationName = org.Name,
        Role = role,
    };
}
