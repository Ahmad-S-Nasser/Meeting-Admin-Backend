namespace CoonMeeting.Dashboard.Api.Services;

public interface IInviteTokenService
{
    /// <summary>Generates a new raw invite token. Never persisted raw - only its hash is stored.</summary>
    string GenerateToken();

    /// <summary>SHA-256 hex of the raw token, the only form ever stored.</summary>
    string Hash(string rawToken);
}
