using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public class MeetingSettingsRepository : IMeetingSettingsRepository
{
    private readonly LiteDbContext _db;

    public MeetingSettingsRepository(LiteDbContext db)
    {
        _db = db;
    }

    public Task<MeetingSettings?> GetAsync(string meetingId) =>
        Task.FromResult((MeetingSettings?)_db.MeetingSettings.FindById(meetingId));

    public Task UpsertAsync(MeetingSettings settings)
    {
        _db.MeetingSettings.Upsert(settings);
        return Task.CompletedTask;
    }
}
