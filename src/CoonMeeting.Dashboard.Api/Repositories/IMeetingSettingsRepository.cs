using CoonMeeting.Dashboard.Api.Models;

namespace CoonMeeting.Dashboard.Api.Repositories;

public interface IMeetingSettingsRepository
{
    /// <summary>Null when the organizer never configured this meeting - callers treat that as
    /// OrganizerOnly for every capability.</summary>
    Task<MeetingSettings?> GetAsync(string meetingId);

    Task UpsertAsync(MeetingSettings settings);
}
