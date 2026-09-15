using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Services;

public class SessionTokenResult
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public interface ISessionTokenService
{
    SessionTokenResult Mint(User user, Organization org, OrganizationRole role);
}
