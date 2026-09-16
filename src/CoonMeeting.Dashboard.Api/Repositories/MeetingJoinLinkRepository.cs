using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public class MeetingJoinLinkRepository : IMeetingJoinLinkRepository
{
    private readonly LiteDbContext _db;

    public MeetingJoinLinkRepository(LiteDbContext db)
    {
        _db = db;
    }

    public Task<MeetingJoinLink?> GetByTokenHashAsync(string tokenHash) =>
        Task.FromResult((MeetingJoinLink?)_db.MeetingJoinLinks.FindOne(l => l.TokenHash == tokenHash));

    public Task<MeetingJoinLink?> GetActiveAnyLinkAsync(string meetingId) =>
        Task.FromResult((MeetingJoinLink?)_db.MeetingJoinLinks.FindOne(
            l => l.MeetingId == meetingId && l.Scope == MeetingJoinLinkScope.Any && !l.Revoked));

    public Task InsertAsync(MeetingJoinLink link)
    {
        _db.MeetingJoinLinks.Insert(link);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MeetingJoinLink link)
    {
        _db.MeetingJoinLinks.Update(link);
        return Task.CompletedTask;
    }
}
