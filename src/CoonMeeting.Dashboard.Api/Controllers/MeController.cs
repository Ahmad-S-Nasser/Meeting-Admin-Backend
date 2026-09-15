using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Models.Dtos;
using CoonMeeting.Dashboard.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoonMeeting.Dashboard.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize(AuthenticationSchemes = DashboardSessionScheme.SchemeName)]
public class MeController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _orgs;

    public MeController(IUserRepository users, IOrganizationRepository orgs)
    {
        _users = users;
        _orgs = orgs;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponseDto>> Get()
    {
        var user = await _users.GetByIdAsync(SessionContext.UserId(User));
        if (user == null) return NotFound();

        var org = await _orgs.GetByIdAsync(SessionContext.OrganizationId(User));
        if (org == null) return NotFound();

        return Ok(new MeResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            OrganizationId = org.Id,
            OrganizationName = org.Name,
            Role = SessionContext.Role(User),
        });
    }
}
