using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public interface IPendingInviteRepository
{
    Task<PendingInvite?> GetByTokenHashAsync(string tokenHash);
    Task InsertAsync(PendingInvite invite);
}
