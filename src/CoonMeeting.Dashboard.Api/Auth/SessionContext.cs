using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Auth;

public static class SessionContext
{
    public static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("Request has no sub claim.");

    public static string OrganizationId(ClaimsPrincipal user) =>
        user.FindFirstValue("organizationId")
        ?? throw new InvalidOperationException("Request has no organizationId claim.");

    public static OrganizationRole Role(ClaimsPrincipal user) =>
        Enum.Parse<OrganizationRole>(
            user.FindFirstValue("role")
            ?? throw new InvalidOperationException("Request has no role claim."));
}
