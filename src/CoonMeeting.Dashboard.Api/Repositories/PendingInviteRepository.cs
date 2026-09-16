using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public class PendingInviteRepository : IPendingInviteRepository
{
    private readonly LiteDbContext _db;

    public PendingInviteRepository(LiteDbContext db)
    {
        _db = db;
    }

    public Task<PendingInvite?> GetByTokenHashAsync(string tokenHash) =>
        Task.FromResult((PendingInvite?)_db.PendingInvites.FindOne(i => i.TokenHash == tokenHash));

    public Task InsertAsync(PendingInvite invite)
    {
        _db.PendingInvites.Insert(invite);
        return Task.CompletedTask;
    }
}
