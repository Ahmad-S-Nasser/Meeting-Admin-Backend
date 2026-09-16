using System.Security.Cryptography;
using System.Text;

namespace CoonMeeting.Dashboard.Api.Services;

/// <summary>Same generate/hash shape as Coon.Meeting's own ApiKeyService, minus the live/test prefix.</summary>
public class InviteTokenService : IInviteTokenService
{
    private const int RandomBytes = 24;

    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RandomBytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
    }

    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
