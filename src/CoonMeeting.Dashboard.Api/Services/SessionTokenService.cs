using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace CoonMeeting.Dashboard.Api.Services;

public class SessionTokenService : ISessionTokenService
{
    private readonly SessionSettings _settings;

    public SessionTokenService(SessionSettings settings)
    {
        _settings = settings;
    }

    public SessionTokenResult Mint(User user, Organization org, OrganizationRole role)
    {
        var now = DateTime.UtcNow;
        var expiry = now.AddHours(_settings.TtlHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim("email", user.Email),
            new Claim("name", user.Name),
            new Claim("organizationId", org.Id),
            new Claim("role", role.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiry,
            signingCredentials: creds);

        return new SessionTokenResult
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiry,
        };
    }
}
