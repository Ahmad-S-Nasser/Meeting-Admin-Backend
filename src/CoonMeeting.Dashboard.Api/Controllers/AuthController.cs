using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _orgs;
    private readonly LiteDbContext _db;
    private readonly ICoonMeetingClient _coonMeeting;
    private readonly ISessionTokenService _sessions;
    private readonly IPasswordHasher<User> _hasher;
    private readonly FrontendSettings _frontend;

    public AuthController(
        IUserRepository users,
        IOrganizationRepository orgs,
        LiteDbContext db,
        ICoonMeetingClient coonMeeting,
        ISessionTokenService sessions,
        IPasswordHasher<User> hasher,
        FrontendSettings frontend)
    {
        _users = users;
        _orgs = orgs;
        _db = db;
        _coonMeeting = coonMeeting;
        _sessions = sessions;
        _hasher = hasher;
        _frontend = frontend;
    }

    // POST /api/v1/auth/signup
    [HttpPost("signup")]
    public async Task<ActionResult<AuthResponseDto>> Signup([FromBody] SignupDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _users.GetByEmailAsync(email) != null)
            return Conflict(new { message = "An account with this email already exists." });

        // External call first, before any local write: it can't be rolled back, so a failure
        // here must not leave an orphaned local user/org behind.
        var tenant = await _coonMeeting.CreateTenantAsync(dto.OrganizationName, new List<string> { _frontend.BaseUrl });

        var user = new User { Email = email, Name = dto.Name };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);

        var org = new Organization
        {
            Name = dto.OrganizationName,
            CoonMeetingTenantId = tenant.TenantId,
            CoonMeetingApiKey = tenant.ApiKey,
        };
        org.Members.Add(new OrganizationMember
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = OrganizationRole.Owner,
        });
        user.OrganizationId = org.Id;

        _db.CreateUserAndOrganization(user, org);

        var token = _sessions.Mint(user, org, OrganizationRole.Owner);
        return Created(string.Empty, ToAuthResponse(user, org, OrganizationRole.Owner, token));
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email);

        // Same generic message either way - never tell a caller which field was wrong.
        if (user == null) return Unauthorized(new { message = "Invalid email or password." });

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verify == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        var org = user.OrganizationId != null ? await _orgs.GetByIdAsync(user.OrganizationId) : null;
        if (org == null)
        {
            // Every persisted user is created together with an org (CreateUserAndOrganization) -
            // reaching here means the data is inconsistent, not a normal auth failure.
            return StatusCode(500, new { message = "This account has no organization." });
        }

        var role = org.Members.FirstOrDefault(m => m.UserId == user.Id)?.Role ?? OrganizationRole.Member;

        var token = _sessions.Mint(user, org, role);
        return Ok(ToAuthResponse(user, org, role, token));
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
